using System;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.VFX;

namespace Elemental.Presentation.Fire
{
    [RequireComponent(typeof(VisualEffect))]
    public sealed class FirePresentationController : MonoBehaviour
    {
        private static readonly ProfilerMarker PublishMarker = new ProfilerMarker("Fire.Presentation.Publish");
        [SerializeField] private FireVisualProfile profile;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField] private FireLightPool lightPool;
        private VisualEffect effect;
        private FireVfxBuffers buffers;
        private FireCpuMeshBackend cpu;
        private readonly FireGpuNode[] nodes = new FireGpuNode[6];
        private readonly FireGpuContact[] contacts = new FireGpuContact[8];
        private FireGroupHandle currentGroup;
        private bool hasGroup;
        private float lastTime;
        public bool ForceHighDetail { get; set; }
        public bool IsReady => buffers != null || cpu != null;
        public FireVisualBackendSelection ActiveBackend { get; private set; }
        public FireCpuMeshBackend CpuDiagnostics => cpu;
        public string BackendFailure { get; private set; }
        public int AliveParticles => cpu != null ? cpu.AliveCount : effect == null ? 0 : effect.aliveParticleCount;
        public void Configure(FireVisualProfile settings, UnityEngine.Camera camera, FireLightPool lights)
        { profile = settings; viewCamera = camera; lightPool = lights; }
        private void OnEnable()
        {
            effect = GetComponent<VisualEffect>();
            if (profile == null) return; // Configure is allowed before the first Publish.
            EnsureReady();
        }
        private bool EnsureReady()
        {
            if (buffers != null || cpu != null) return true;
            if (BackendFailure != null) return false;
            if (profile == null) return false;
            if (!profile.IsValid) { Fail("Fire profile needs a generated graph and valid lifetime/speed limits."); return false; }
            effect = GetComponent<VisualEffect>();
            bool nativeSupported = profile.NativeBackendEnabled && profile.Graph != null && QualitySettings.activeColorSpace == ColorSpace.Linear && SystemInfo.supportsComputeShaders && SystemInfo.maxComputeBufferInputsVertex > 0;
            bool useCpu = profile.Backend == FireVisualBackendSelection.CpuMesh || (profile.Backend == FireVisualBackendSelection.Automatic && !nativeSupported);
            if (useCpu)
            {
                effect.enabled = false;
                try { cpu = new FireCpuMeshBackend(transform, profile); ActiveBackend = FireVisualBackendSelection.CpuMesh; }
                catch (Exception error) { Fail(error.Message); return false; }
                return true;
            }
            ActiveBackend = FireVisualBackendSelection.GpuVfx;
            effect.visualEffectAsset = profile.Graph;
            effect.pause = true; // Explicit simulation uses the canonical scaled clock once per published step.
            effect.enabled = true;
            try { buffers = new FireVfxBuffers(effect); }
            catch (Exception error) { Fail(error.Message); return false; }
            effect.SetFloat("FireMinLifetime", profile.MinLifetime);
            effect.SetFloat("FireMaxLifetime", profile.MaxLifetime);
            effect.SetFloat("FireParticleRadius", profile.ParticleRadius);
            effect.SetFloat("FireFreeDrag", profile.FreeDrag);
            effect.SetFloat("FireFreeLift", profile.FreeLift);
            effect.SetFloat("FireMaxSpeed", profile.MaximumSpeed);
            effect.SetUInt("FireSubsteps", (uint)profile.Substeps);
            return true;
        }
        private void Fail(string message)
        {
            if (BackendFailure != message) Debug.LogError("Fire backend disabled: " + message, this);
            BackendFailure = message;
            if (effect != null) effect.enabled = false;
        }
        public void Publish(FirePresentationSnapshot snapshot)
        {
            using (PublishMarker.Auto())
            {
                if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                if (!EnsureReady()) return;
                if (snapshot.Lifecycle == FireLifecycle.Retired) { Retire(); return; }
                if (hasGroup && currentGroup != snapshot.Group)
                    throw new InvalidOperationException("Retire the old Fire group before assigning a new generation.");
                if (!hasGroup)
                {
                    hasGroup = true; currentGroup = snapshot.Group; lastTime = snapshot.Time;
                    if (cpu != null) cpu.Begin(snapshot.Seed);
                    else { effect.startSeed = snapshot.Seed; effect.resetSeedOnPlay = false; effect.Reinit(); effect.pause = true; buffers.Rebind(); }
                }
                float delta = snapshot.Time - lastTime;
                if (delta < 0) throw new InvalidOperationException("Fire presentation clock moved backwards.");
                var origin = snapshot.Origin;
                float lod = !ForceHighDetail && viewCamera != null && (viewCamera.transform.position - Vec(origin)).sqrMagnitude > profile.LowDetailDistance * profile.LowDetailDistance ? profile.LowDetailRate : 1;
                float emission = snapshot.Emits ? profile.SpawnRate * lod * Mathf.Clamp01(snapshot.Energy) : 0;
                if (cpu != null)
                {
                    cpu.Step(snapshot, delta, emission, viewCamera); lastTime = snapshot.Time;
                    PublishLight(snapshot, lod); return;
                }
                for (int i = 0; i < snapshot.NodeCount; i++)
                {
                    var n = snapshot.Nodes[i];
                    nodes[i] = new FireGpuNode
                    {
                        A_Radius = Row(n.A - origin, n.Radius), B_ShellHalfThickness = Row(n.B - origin, n.ShellHalfThickness),
                        Flow_Response = Row(n.Flow, n.Response), Up_Lift = Row(n.Up, n.Lift),
                        Dynamics = new Vector4(n.Swirl, n.NoiseSpeed, n.NoiseFrequency, (float)n.Shape),
                        State = new Vector4(n.Density, n.Active ? 1 : 0, n.Phase, n.MaxTargetSpeed)
                    };
                }
                for (int i = 0; i < snapshot.ContactCount; i++)
                {
                    var c = snapshot.Contacts[i];
                    contacts[i] = new FireGpuContact
                    {
                        Point_Radius = Row(c.Point - c.SurfaceVelocity * delta - origin, c.Radius), Normal_FrontDepth = Row(c.Normal, c.FrontDepth),
                        Tangent_RecoveryDepth = Row(c.Tangent, c.RecoveryDepth), Velocity_SpreadFraction = Row(c.SurfaceVelocity, c.SpreadFraction),
                        AngularVelocity_Response = Row(c.AngularVelocity, c.ResponseRate), Settings = new Vector4(c.Skin, c.Active ? 1 : 0, 0, 0)
                    };
                }
                buffers.Publish(nodes, snapshot.NodeCount, contacts, snapshot.ContactCount, Vec(origin), Vec(snapshot.FreeUp), snapshot.Time);
                effect.SetFloat("FireSpawnRate", emission);
                var min = Vec(snapshot.BoundsMin); var max = Vec(snapshot.BoundsMax);
                effect.SetVector3("FireBoundsCenter", (min + max) * 0.5f);
                effect.SetVector3("FireBoundsSize", Vector3.Max(max - min, Vector3.one));
                if (delta > 0) effect.Simulate(delta, 1);
                lastTime = snapshot.Time;
                PublishLight(snapshot, lod);
            }
        }
        private void PublishLight(FirePresentationSnapshot snapshot, float lod)
        {
            if (lightPool == null) return;
            if (lod < 1 || !snapshot.Emits) lightPool.Release(this);
            else lightPool.Publish(this, Vec(snapshot.Origin), profile.LightIntensity * Mathf.Clamp01(snapshot.Energy), profile.LightRange);
        }
        public void Retire()
        {
            hasGroup = false;
            if (cpu != null) cpu.Clear();
            else if (effect != null) { effect.Stop(); effect.Reinit(); effect.pause = true; }
            if (lightPool != null) lightPool.Release(this);
        }
        private void OnDisable()
        {
            if (lightPool != null) lightPool.Release(this);
            buffers?.Dispose(); buffers = null; cpu?.Dispose(); cpu = null; hasGroup = false;
        }
        private static Vector4 Row(float3 p, float w) => new Vector4(p.x,p.y,p.z,w);
        private static Vector3 Vec(float3 p) => new Vector3(p.x,p.y,p.z);
    }
}




