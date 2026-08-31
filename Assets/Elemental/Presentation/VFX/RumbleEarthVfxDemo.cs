using System;
using System.Collections;
using Elemental.Input.Actions;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class RumbleEarthVfxDemo : MonoBehaviour
    {
        [SerializeField] private Transform[] wallStones = Array.Empty<Transform>();
        [SerializeField] private ParticleSystem pressureDust;
        [SerializeField] private ParticleSystem groundDust;
        [SerializeField] private ParticleSystem gravel;
        [SerializeField] private Transform impactPoint;
        [SerializeField] private Mesh[] debrisMeshes = Array.Empty<Mesh>();
        [SerializeField] private Material debrisMaterial;
        [SerializeField] private RumbleLensDirector lensDirector;
        [SerializeField] private EarthInputAdapter inputAdapter;
        [SerializeField] private float wallTravel = 3.2f;

        private Vector3[] _wallTargets = Array.Empty<Vector3>();
        private Coroutine _wallRoutine;
        private bool _wallRaised;

        public void Configure(
            Transform[] configuredWall,
            ParticleSystem configuredPressureDust,
            ParticleSystem configuredGroundDust,
            ParticleSystem configuredGravel,
            Transform configuredImpactPoint,
            Mesh[] configuredDebrisMeshes,
            Material configuredDebrisMaterial,
            RumbleLensDirector configuredLens,
            EarthInputAdapter configuredInput = null)
        {
            wallStones = configuredWall ?? Array.Empty<Transform>();
            pressureDust = configuredPressureDust;
            groundDust = configuredGroundDust;
            gravel = configuredGravel;
            impactPoint = configuredImpactPoint;
            debrisMeshes = configuredDebrisMeshes ?? Array.Empty<Mesh>();
            debrisMaterial = configuredDebrisMaterial;
            lensDirector = configuredLens;
            inputAdapter = configuredInput;
            CacheWallTargets(true);
        }

        private void Awake()
        {
            CacheWallTargets(true);
            if (inputAdapter == null)
                inputAdapter = FindFirstObjectByType<EarthInputAdapter>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (inputAdapter == null) return;
            if (inputAdapter.JumpPressed) RaiseWall();
            if (inputAdapter.DebugLookdevHeavyImpactPressed) HeavyImpact();
            if (inputAdapter.ElementWaterPressed) ResetWall();
        }

        public void RaiseWall()
        {
            if (_wallRaised || wallStones.Length == 0) return;
            if (_wallRoutine != null) StopCoroutine(_wallRoutine);
            _wallRoutine = StartCoroutine(RaiseWallRoutine());
        }

        public void ResetWall()
        {
            if (_wallRoutine != null) StopCoroutine(_wallRoutine);
            _wallRoutine = null;
            _wallRaised = false;
            for (int index = 0; index < wallStones.Length; index++)
            {
                Transform stone = wallStones[index];
                if (stone != null) stone.localPosition = _wallTargets[index] - Vector3.up * wallTravel;
            }
        }

        public void HeavyImpact()
        {
            Vector3 point = impactPoint != null ? impactPoint.position : transform.position;
            Quaternion rotation = Quaternion.identity;
            EmitAt(pressureDust, point + Vector3.up * 0.08f, rotation, 68);
            EmitAt(groundDust, point + Vector3.up * 0.03f, rotation, 42);
            EmitAt(gravel, point + Vector3.up * 0.10f, rotation, 28);
            SpawnPhysicalDebris(point);
            lensDirector?.AddImpulse(0.82f);
        }

        private IEnumerator RaiseWallRoutine()
        {
            const float duration = 1.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int index = 0; index < wallStones.Length; index++)
                {
                    Transform stone = wallStones[index];
                    if (stone == null) continue;
                    float delay = index * 0.055f;
                    float local = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.1f, duration - delay));
                    float eased = 1f - Mathf.Pow(1f - local, 3f);
                    stone.localPosition = Vector3.LerpUnclamped(
                        _wallTargets[index] - Vector3.up * wallTravel,
                        _wallTargets[index],
                        eased);
                }
                if (groundDust != null && UnityEngine.Random.value < Time.deltaTime * 18f)
                {
                    int index = UnityEngine.Random.Range(0, wallStones.Length);
                    Transform stone = wallStones[index];
                    if (stone != null) EmitAt(groundDust, stone.position, Quaternion.identity, 3);
                }
                yield return null;
            }
            for (int index = 0; index < wallStones.Length; index++)
            {
                if (wallStones[index] != null) wallStones[index].localPosition = _wallTargets[index];
            }
            Vector3 center = AverageWallPosition();
            EmitAt(pressureDust, center + Vector3.up * 0.08f, Quaternion.identity, 45);
            EmitAt(groundDust, center, Quaternion.identity, 34);
            EmitAt(gravel, center + Vector3.up * 0.08f, Quaternion.identity, 18);
            lensDirector?.AddImpulse(0.46f);
            _wallRaised = true;
            _wallRoutine = null;
        }

        private void CacheWallTargets(bool lowerWall)
        {
            if (wallStones == null) wallStones = Array.Empty<Transform>();
            _wallTargets = new Vector3[wallStones.Length];
            for (int index = 0; index < wallStones.Length; index++)
            {
                Transform stone = wallStones[index];
                if (stone == null) continue;
                _wallTargets[index] = stone.localPosition;
                if (lowerWall) stone.localPosition -= Vector3.up * wallTravel;
            }
            _wallRaised = !lowerWall;
        }

        private void SpawnPhysicalDebris(Vector3 origin)
        {
            if (debrisMeshes == null || debrisMeshes.Length == 0 || debrisMaterial == null) return;
            const int count = 12;
            for (int index = 0; index < count; index++)
            {
                Mesh mesh = debrisMeshes[index % debrisMeshes.Length];
                if (mesh == null) continue;
                var debris = new GameObject($"V5 Impact Debris {index:00}");
                debris.transform.position = origin + Vector3.up * 0.16f;
                debris.transform.rotation = UnityEngine.Random.rotation;
                float scale = UnityEngine.Random.Range(0.10f, 0.28f);
                debris.transform.localScale = Vector3.one * scale;
                MeshFilter filter = debris.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = debris.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = debrisMaterial;
                MeshCollider collider = debris.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
                Rigidbody body = debris.AddComponent<Rigidbody>();
                body.mass = Mathf.Lerp(0.35f, 2.1f, scale / 0.28f);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Vector3 radial = UnityEngine.Random.onUnitSphere;
                radial.y = Mathf.Abs(radial.y) * 0.8f + 0.25f;
                body.linearVelocity = radial.normalized * UnityEngine.Random.Range(2.2f, 6.5f);
                body.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(4f, 11f);
                debris.AddComponent<RumbleDebrisLifecycle>();
            }
        }

        private static void EmitAt(
            ParticleSystem system,
            Vector3 position,
            Quaternion rotation,
            int count)
        {
            if (system == null || count <= 0) return;
            system.transform.SetPositionAndRotation(position, rotation);
            system.Emit(count);
        }

        private Vector3 AverageWallPosition()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int index = 0; index < wallStones.Length; index++)
            {
                if (wallStones[index] == null) continue;
                sum += wallStones[index].position;
                count++;
            }
            return count > 0 ? sum / count : transform.position;
        }
    }
}
