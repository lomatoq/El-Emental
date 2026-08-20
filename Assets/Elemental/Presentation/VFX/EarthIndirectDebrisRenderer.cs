using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    /// <summary>
    /// Bounded Tier-C debris renderer. It draws visual chips with one indirect draw,
    /// has no colliders or gameplay authority, and reuses fixed CPU/GPU storage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthIndirectDebrisRenderer : MonoBehaviour
    {
        private const int Capacity = 512;
        private static readonly int TransformBufferId = Shader.PropertyToID("_EarthDebrisTransforms");

        private struct DebrisState
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Quaternion Rotation;
            public Vector3 AngularVelocity;
            public float Scale;
            public float Remaining;
            public float Lifetime;
        }

        [SerializeField] private MagicExecutor executor;
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField, Range(0.2f, 3f)] private float maximumLifetime = 1.6f;
        [SerializeField, Range(0f, 30f)] private float visualGravity = 10f;

        private readonly DebrisState[] _states = new DebrisState[Capacity];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[Capacity];
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _commands =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private GraphicsBuffer _transformBuffer;
        private GraphicsBuffer _commandBuffer;
        private RenderParams _renderParams;
        private int _activeCount;
        private uint _sequence = 1u;

        public int ActiveVisualCount => _activeCount;
        public bool HasGameplayAuthority => false;
        public bool IsIndirectAvailable => SystemInfo.supportsComputeShaders && SystemInfo.supportsInstancing;

        public void Configure(MagicExecutor configuredExecutor, Mesh configuredMesh, Material configuredMaterial)
        {
            if (isActiveAndEnabled) Unsubscribe();
            executor = configuredExecutor;
            mesh = configuredMesh;
            material = configuredMaterial;
            if (isActiveAndEnabled)
            {
                EnsureBuffers();
                Subscribe();
            }
        }

        private void OnEnable()
        {
            EnsureBuffers();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseBuffers();
        }

        private void Update()
        {
            if (_activeCount == 0 || mesh == null || material == null || _transformBuffer == null)
                return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int output = 0;
            for (int index = 0; index < _activeCount; index++)
            {
                DebrisState state = _states[index];
                state.Remaining -= dt;
                if (state.Remaining <= 0f) continue;
                Vector3 up = state.Position.sqrMagnitude > 0.01f ? state.Position.normalized : Vector3.up;
                state.Velocity -= up * visualGravity * dt;
                state.Position += state.Velocity * dt;
                state.Rotation = NormalizeSafe(
                    Quaternion.Euler(state.AngularVelocity * dt) * state.Rotation);
                float life01 = Mathf.Clamp01(state.Remaining / Mathf.Max(0.001f, state.Lifetime));
                float scale = state.Scale * Mathf.SmoothStep(0f, 1f, life01);
                if (!IsFinite(state.Position) || !float.IsFinite(scale)) continue;
                _states[output] = state;
                _matrices[output] = Matrix4x4.TRS(state.Position, state.Rotation, Vector3.one * scale);
                output++;
            }
            _activeCount = output;
            if (output == 0) return;
            _transformBuffer.SetData(_matrices, 0, 0, output);
            _commands[0].instanceCount = (uint)output;
            _commandBuffer.SetData(_commands);
            Graphics.RenderMeshIndirect(_renderParams, mesh, _commandBuffer, 1);
        }

        private void EnsureBuffers()
        {
            if (!IsIndirectAvailable || mesh == null || material == null || _transformBuffer != null) return;
            _transformBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Capacity, 64);
            _commandBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _commands[0].indexCountPerInstance = mesh.GetIndexCount(0);
            _commands[0].instanceCount = 0;
            _commands[0].startIndex = mesh.GetIndexStart(0);
            _commands[0].baseVertexIndex = mesh.GetBaseVertex(0);
            _commands[0].startInstance = 0;
            _commandBuffer.SetData(_commands);
            material.SetBuffer(TransformBufferId, _transformBuffer);
            _renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 220f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer
            };
        }

        private void ReleaseBuffers()
        {
            _transformBuffer?.Release();
            _commandBuffer?.Release();
            _transformBuffer = null;
            _commandBuffer = null;
            _activeCount = 0;
        }

        private void Subscribe()
        {
            if (executor == null) return;
            executor.Events.EarthImpactOccurred += OnImpact;
            executor.Events.WallRaised += OnWallRaised;
            executor.Events.EarthReturnOccurred += OnReturn;
        }

        private void Unsubscribe()
        {
            if (executor == null) return;
            executor.Events.EarthImpactOccurred -= OnImpact;
            executor.Events.WallRaised -= OnWallRaised;
            executor.Events.EarthReturnOccurred -= OnReturn;
        }

        private void OnImpact(EarthImpactEvent value)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log10(1f + value.KineticEnergy) * 5f), 3, 24);
            Spawn(new Vector3(value.Point.x, value.Point.y, value.Point.z),
                new Vector3(value.Normal.x, value.Normal.y, value.Normal.z), count, value.SourceId);
        }

        private void OnWallRaised(WallRaisedEvent value)
        {
            Vector3 start = new Vector3(value.Start.x, value.Start.y, value.Start.z);
            Vector3 end = new Vector3(value.End.x, value.End.y, value.End.z);
            Vector3 point = (start + end) * 0.5f;
            Spawn(point, point.sqrMagnitude > 0.01f ? point.normalized : Vector3.up,
                Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(start, end) * 2f), 5, 20), value.WallId);
        }

        private void OnReturn(EarthReturnEvent value)
        {
            if (value.Stage != EarthReturnEventStage.Subsurface && value.Stage != EarthReturnEventStage.Completed)
                return;
            Vector3 point = new Vector3(value.Position.x, value.Position.y, value.Position.z);
            Spawn(point, point.sqrMagnitude > 0.01f ? point.normalized : Vector3.up,
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Log10(1f + value.Mass) * 3f), 2, 10), value.MatterId);
        }

        private void Spawn(Vector3 point, Vector3 normal, int count, uint seed)
        {
            if (_transformBuffer == null) return;
            normal = normal.sqrMagnitude > 0.5f ? normal.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            for (int emitted = 0; emitted < count && _activeCount < Capacity; emitted++)
            {
                uint hash = Hash(seed ^ _sequence++ ^ ((uint)emitted * 0x9E3779B9u));
                float angle = Hash01(hash) * Mathf.PI * 2f;
                float radial = Mathf.Lerp(0.35f, 2.5f, Hash01(hash ^ 0xA341316Cu));
                float upward = Mathf.Lerp(0.8f, 3.6f, Hash01(hash ^ 0xC8013EA4u));
                Vector3 lateral = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                float lifetime = Mathf.Lerp(0.55f, maximumLifetime, Hash01(hash ^ 0xAD90777Du));
                _states[_activeCount++] = new DebrisState
                {
                    Position = point + normal * 0.025f,
                    Velocity = lateral * radial + normal * upward,
                    Rotation = Quaternion.Euler(Hash01(hash ^ 11u) * 180f, Hash01(hash ^ 23u) * 180f, Hash01(hash ^ 37u) * 180f),
                    AngularVelocity = new Vector3(Hash01(hash ^ 41u), Hash01(hash ^ 43u), Hash01(hash ^ 47u)) * 540f,
                    Scale = Mathf.Lerp(0.035f, 0.16f, Hash01(hash ^ 0xD1B54A35u)),
                    Remaining = lifetime,
                    Lifetime = lifetime
                };
            }
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float Hash01(uint value) => (Hash(value) & 0x00FFFFFFu) / 16777215f;

        public static Quaternion NormalizeSafe(Quaternion value)
        {
            float magnitudeSquared = value.x * value.x + value.y * value.y +
                                     value.z * value.z + value.w * value.w;
            if (!float.IsFinite(magnitudeSquared) || magnitudeSquared < 0.000001f)
                return Quaternion.identity;
            float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
