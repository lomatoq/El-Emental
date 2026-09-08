using System;
using Elemental.Presentation.UI;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Rendering;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    /// <summary>One bounded cosmetic layer: continuous ground drift and denser settled-stone wakes.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem))]
    public sealed class EarthSurfaceWindDust : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.VFX.SurfaceWindDust");
        [SerializeField] private EarthSurfaceWindDustProfile profile;
        [SerializeField] private Transform arenaAnchor, planet;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private FrontendFlowController frontend;
        private readonly Vector3[] stoneBases=new Vector3[96];
        private readonly UnityEngine.Object[] stoneOwners=new UnityEngine.Object[96];
        private readonly int[] neighbours=new int[96],nearest=new int[96];
        private readonly float[] densityWeights=new float[96];
        public int ClusteredStoneCandidates { get; private set; }
        public int GapEmitted { get; private set; }
        public float MeanStoneNeighbours { get; private set; }
        public float EffectiveStoneRate { get; private set; }
        private bool Reduced => profile.clusteredWisps && frontend!=null && frontend.Preferences.ReducedMotion;
        private ParticleSystem particles;
        private MaterialPropertyBlock previousRendererProperties;
        private bool shortenedDepthFade;
        private readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[256];
        private readonly Vector3[] ground = new Vector3[256], normals = new Vector3[256];
        private readonly bool[] occupied = new bool[256];
        private readonly Collider[] overlaps = new Collider[256], stones = new Collider[96];
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private int stoneCount, scanCursor, probeCursor, appliedCapacity;
        private float nextScan, groundBudget, stoneBudget;
        private uint random = 0x6A09E667u;
        public int LiveParticles => particles != null ? particles.particleCount : 0;
        public int SettledStoneCandidates => stoneCount;
        public int GroundEmitted { get; private set; }
        public int StoneEmitted { get; private set; }
        public EarthSurfaceWindDustProfile Profile => profile;
        public Transform ArenaAnchor => arenaAnchor;
        public ParticleSystem System => particles;

        public void Configure(EarthSurfaceWindDustProfile tuning, Transform anchor, Transform planetCenter, LayerMask mask, Material material, FrontendFlowController flow=null)
        { profile = tuning; arenaAnchor = anchor; planet = planetCenter; surfaceMask = mask; dustMaterial = material; frontend=flow; if (Application.isPlaying) Initialize(); }
        private void Awake() => Initialize();
        private void Initialize()
        {
            particles = GetComponent<ParticleSystem>();
            if (profile == null || arenaAnchor == null || planet == null || dustMaterial == null)
            { Debug.LogError("Surface wind dust needs its profile, arena anchor, planet and existing dust material. Run Elemental > VFX > Install Surface Wind Dust.", this); enabled = false; return; }
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f; main.maxParticles = Mathf.Clamp(profile.maximumParticles, 32, 256); appliedCapacity = main.maxParticles;
            main.startSize3D=profile.clusteredWisps;main.startRotation3D=profile.clusteredWisps;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var collision = particles.collision; collision.enabled = false;
            var noise = particles.noise; noise.enabled = false;
            var trails = particles.trails; trails.enabled = false;
            var lights = particles.lights; lights.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.18f), new GradientAlphaKey(.65f,.7f), new GradientAlphaKey(0,1) });
            color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0,.65f,1,1.3f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial; renderer.renderMode = ParticleSystemRenderMode.Stretch;
            if(profile.clusteredWisps){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.alignment=ParticleSystemRenderSpace.World;renderer.mesh=Resources.GetBuiltinResource<Mesh>("Quad.fbx");}
            renderer.lengthScale = profile.clusteredWisps ? 2.1f : 1.7f; renderer.velocityScale = profile.clusteredWisps ? .08f : .18f; renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            EarthParticleSystemTuningApplier.UseMaterialDustColor(particles);
            if(profile.clusteredWisps)
            {
                // This low ambient renderer needs a shorter depth fade than metre-scale fracture smoke.
                if(!shortenedDepthFade){previousRendererProperties=new MaterialPropertyBlock();renderer.GetPropertyBlock(previousRendererProperties);}
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                properties.SetVector("_SoftParticleFadeParams",new Vector4(.015f,8f,0,0));
                // LightDustMote uses these scalar properties; the legacy URP vector alone is ignored.
                properties.SetFloat("_SoftParticleNearDistance",.015f);
                properties.SetFloat("_SoftParticleInvDistance",8f);
                properties.SetColor("_BaseColor",new Color(.95f,.73f,.48f,.8f));
                renderer.SetPropertyBlock(properties);shortenedDepthFade=true;
            }
            else if(shortenedDepthFade){renderer.SetPropertyBlock(previousRendererProperties);shortenedDepthFade=false;}
            groundBudget=stoneBudget=0;GroundEmitted=StoneEmitted=GapEmitted=0;
            Array.Clear(occupied, 0, occupied.Length); nextScan = 0f;
            if (gameObject.activeInHierarchy && enabled) particles.Play(true);
        }
        private void OnEnable() { if (particles != null && profile != null) particles.Play(true); }
        private void OnDisable() { if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        private void LateUpdate()
        {
            if (profile == null || particles == null || arenaAnchor == null || planet == null) return;
            if (!profile.enabledEffect) { if (particles.particleCount > 0) particles.Clear(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); if (dt <= 0f) return;
            using (Marker.Auto())
            {
                int capacity = Mathf.Clamp(profile.maximumParticles, 32, 256);
                if (capacity != appliedCapacity) { var main = particles.main; main.maxParticles = capacity; appliedCapacity = capacity; }
                if (Time.time >= nextScan) { nextScan = Time.time + Mathf.Max(.2f, profile.stoneScanSeconds); ScanStones(); }
                int count = particles.GetParticles(buffer);
                Array.Clear(occupied, 0, occupied.Length);
                int probes = Mathf.Min(4, count);
                for (int i = 0; i < count; i++)
                {
                    int slot = (int)buffer[i].randomSeed - 1;
                    if (slot < 0 || slot >= 256) { buffer[i].remainingLifetime = 0f; continue; }
                    occupied[slot] = true;
                    int relative = (i - probeCursor + count) % Mathf.Max(1, count);
                    if (relative < probes)
                    {
                        Vector3 radialUp = (buffer[i].position - planet.position).normalized;
                        if (TryGround(buffer[i].position + radialUp * .75f, radialUp, 1.6f, null, out RaycastHit support))
                        { ground[slot] = support.point; normals[slot] = support.normal; }
                        else buffer[i].remainingLifetime = Mathf.Min(buffer[i].remainingLifetime, .25f);
                    }
                    Vector3 normal = normals[slot];
                    float height = Vector3.Dot(buffer[i].position - ground[slot], normal);
                    buffer[i].position += normal * (Mathf.Clamp(profile.hoverHeight, .02f, .4f) - height);
                    float gust = Reduced ? 1 : .9f + .1f * Mathf.Sin(Time.time * .8f + slot * .41f);
                    buffer[i].velocity = EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind, normal, profile.speed * gust * (Reduced ? Mathf.Clamp01(profile.reducedMotionSpeed) : 1));
                    if(profile.clusteredWisps)buffer[i].rotation3D=Quaternion.LookRotation(normal,buffer[i].velocity.normalized).eulerAngles;
                }
                probeCursor = count > 0 ? (probeCursor + probes) % count : 0;
                particles.SetParticles(buffer, count);
                float rateScale=Reduced?Mathf.Clamp01(profile.reducedMotionRate):1;
                float densityScale=profile.clusteredWisps?Mathf.Lerp(profile.isolatedStoneWeight,profile.clusteredRateMultiplier,Mathf.Clamp01(MeanStoneNeighbours/Mathf.Max(1,profile.saturatedNeighbours))):1;
                EffectiveStoneRate=stoneCount>0?Mathf.Clamp(profile.stoneRate*densityScale,0,48)*rateScale:0;
                groundBudget = Mathf.Min(4f, groundBudget + Mathf.Clamp(profile.groundRate,0,48)*rateScale * dt);
                stoneBudget = Mathf.Min(4f, stoneBudget + EffectiveStoneRate * dt);
                int remaining = capacity - count;
                for (int i = 0; i < 4 && remaining > 0; i++)
                {
                    bool nearStone = stoneBudget >= 1f && (i % 2 == 0 || groundBudget < 1f);
                    if (!nearStone && groundBudget < 1f) break;
                    if (nearStone) stoneBudget -= 1f; else groundBudget -= 1f;
                    if (Emit(nearStone)) remaining--;
                }
            }
        }
        private void ScanStones()
        {
            stoneCount = 0;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(arenaAnchor.position, Mathf.Clamp(profile.areaRadius,2,30), overlaps, surfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && stoneCount < stones.Length; i++)
            {
                Collider value = overlaps[i]; if (value == null || !value.enabled) continue;
                Rigidbody body = value.attachedRigidbody;
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > profile.maximumSettledSpeed * profile.maximumSettledSpeed) continue;
                UnityEngine.Object owner=value.GetComponentInParent<EarthDestructibleDecorRock>();
                if(owner==null)owner=value.GetComponentInParent<EarthRockDebris>();
                if(owner==null)owner=value.GetComponentInParent<EarthArenaPiece>();
                if(owner==null)owner=value.GetComponentInParent<EarthFragment>();
                if(owner==null)owner=value.GetComponentInParent<EarthWallPiece>();
                if(owner==null)continue;
                bool duplicate=false;if(profile.clusteredWisps)for(int j=0;j<stoneCount;j++)if(stoneOwners[j]==owner){duplicate=true;break;}
                if(duplicate)continue;
                stones[stoneCount]=value;stoneOwners[stoneCount]=owner;
                var bounds=value.bounds;var up=(bounds.center-planet.position).normalized;
                float verticalExtent=Mathf.Abs(up.x)*bounds.extents.x+Mathf.Abs(up.y)*bounds.extents.y+Mathf.Abs(up.z)*bounds.extents.z;
                stoneBases[stoneCount]=bounds.center-up*verticalExtent;stoneCount++;
            }
            MeanStoneNeighbours=0;ClusteredStoneCandidates=0;
            if(profile.clusteredWisps)
            {
                EarthGroundDustDensity.Evaluate(stoneBases,stoneCount,(arenaAnchor.position-planet.position).normalized,profile.clusterRadius,profile.isolatedStoneWeight,profile.saturatedNeighbours,neighbours,nearest,densityWeights,out float mean);
                MeanStoneNeighbours=mean;for(int i=0;i<stoneCount;i++)if(neighbours[i]>0)ClusteredStoneCandidates++;
            }
        }
        private bool Emit(bool nearStone)
        {
            Vector3 point = arenaAnchor.position;
            Vector3 up = (point - planet.position).normalized;
            Vector3 wind = EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind, up, 1f);
            Vector3 side = Vector3.Cross(up, wind);
            Collider ignore = null;bool gap=false;
            if (nearStone)
            {
                if (stoneCount == 0) return false;
                int selected=profile.clusteredWisps?EarthGroundDustDensity.Select(densityWeights,stoneCount,Next()):(scanCursor++)%stoneCount;
                if(selected<0)return false;ignore = stones[selected]; if (ignore == null || !ignore.enabled || !ignore.gameObject.activeInHierarchy) return false;
                Rigidbody body = ignore.attachedRigidbody;
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > profile.maximumSettledSpeed * profile.maximumSettledSpeed) return false;
                Bounds bounds = ignore.bounds;
                float extent = Mathf.Abs(wind.x) * bounds.extents.x + Mathf.Abs(wind.y) * bounds.extents.y + Mathf.Abs(wind.z) * bounds.extents.z;
                point = bounds.center + wind * (extent + profile.stoneMargin) + side * ((Next()-.5f) * .8f);
                if(profile.clusteredWisps)
                {
                    point=stoneBases[selected]+wind*(extent+profile.stoneMargin)+side*((Next()-.5f)*.8f);
                    int partner=nearest[selected];
                    if(partner>=0 && Next()<profile.gapEmissionChance && stones[partner]!=null && stones[partner].enabled)
                    {
                        var other=stones[partner];var otherBody=other.attachedRigidbody;
                        if(otherBody==null || otherBody.isKinematic || otherBody.linearVelocity.sqrMagnitude<=profile.maximumSettledSpeed*profile.maximumSettledSpeed)
                        {
                            Vector3 a=bounds.ClosestPoint(stoneBases[partner]),b=other.bounds.ClosestPoint(stoneBases[selected]);
                            if((b-a).sqrMagnitude>.04f){point=Vector3.Lerp(a,b,.3f+.4f*Next())+side*((Next()-.5f)*.3f);gap=true;}
                        }
                    }
                }
            }
            else
            {
                float angle = Next() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Next()) * Mathf.Clamp(profile.areaRadius,2,30);
                point += (wind * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * radius;
            }
            up = (point - planet.position).normalized;
            if (!TryGround(point + up * 3f, up, 7f, ignore, out RaycastHit hit)) return false;
            int slot = 0; while (slot < appliedCapacity && occupied[slot]) slot++;
            if (slot >= appliedCapacity) return false;
            occupied[slot] = true; ground[slot] = hit.point; normals[slot] = hit.normal;
            float size = Mathf.Clamp(Mathf.Lerp(profile.sizeMetres.x, profile.sizeMetres.y, profile.clusteredWisps?Next()*Next():Next()),.1f,2.5f);
            var emit = new ParticleSystem.EmitParams
            {
                position = hit.point + hit.normal * Mathf.Clamp(profile.hoverHeight,.02f,.4f),
                velocity = EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind, hit.normal, profile.speed * (Reduced?Mathf.Clamp01(profile.reducedMotionSpeed):1)),
                startSize = size, startLifetime = Mathf.Clamp(Mathf.Lerp(profile.lifetimeSeconds.x,profile.lifetimeSeconds.y,Next()),.5f,6f),
                startColor = new Color(1,1,1,Mathf.Clamp(profile.opacity,.03f,.6f)), randomSeed = (uint)slot + 1,
                rotation3D = Vector3.zero
            };
            if(profile.clusteredWisps)
            {
                // A broad ground-parallel wisp stays above the surface. Tall camera-facing cards bury most alpha below the floor.
                emit.startSize3D=new Vector3(size*.85f,size*1.8f,1);
                emit.rotation3D=Quaternion.LookRotation(hit.normal,EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind,hit.normal,1)).eulerAngles;
            }
            particles.Emit(emit,1); if(gap)GapEmitted++; if (nearStone) StoneEmitted++; else GroundEmitted++;
            return true;
        }
        private bool TryGround(Vector3 origin, Vector3 up, float distance, Collider ignore, out RaycastHit support)
        {
            support = default; float nearest = float.PositiveInfinity;
            int count = UnityEngine.Physics.RaycastNonAlloc(origin, -up, hits, distance, surfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i]; if (hit.collider == null || hit.collider == ignore || hit.distance >= nearest || Vector3.Dot(hit.normal,up) < .6f) continue;
                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
                nearest = hit.distance; support = hit;
            }
            return nearest < float.PositiveInfinity;
        }
        private float Next() { random = random * 1664525u + 1013904223u; return (random & 0xffffffu) / 16777216f; }
    }
}
