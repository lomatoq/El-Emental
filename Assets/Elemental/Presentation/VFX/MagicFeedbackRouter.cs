using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class MagicFeedbackRouter : MonoBehaviour
    {
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private ParticleSystem earthDust;
        [SerializeField] private AudioSource impactAudio;

        public void Configure(MagicExecutor configuredExecutor)
        {
            if (isActiveAndEnabled && executor != null)
            {
                Unsubscribe();
            }

            executor = configuredExecutor;
            if (isActiveAndEnabled && executor != null)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (executor == null)
            {
                return;
            }

            Subscribe();
        }

        private void OnDisable()
        {
            if (executor == null)
            {
                return;
            }

            Unsubscribe();
        }

        private void Subscribe()
        {
            executor.Events.TerrainEdited += HandleTerrainEdited;
            executor.Events.WallRaised += HandleWallRaised;
            executor.Events.WallCollapsed += HandleWallCollapsed;
            executor.Events.FragmentSpawned += HandleFragmentSpawned;
            executor.Events.FragmentLaunched += HandleFragmentLaunched;
            executor.Events.ImpactOccurred += HandleImpact;
            executor.Events.EarthReturnOccurred += HandleEarthReturn;
        }

        private void Unsubscribe()
        {
            executor.Events.TerrainEdited -= HandleTerrainEdited;
            executor.Events.WallRaised -= HandleWallRaised;
            executor.Events.WallCollapsed -= HandleWallCollapsed;
            executor.Events.FragmentSpawned -= HandleFragmentSpawned;
            executor.Events.FragmentLaunched -= HandleFragmentLaunched;
            executor.Events.ImpactOccurred -= HandleImpact;
            executor.Events.EarthReturnOccurred -= HandleEarthReturn;
        }

        private void HandleTerrainEdited(TerrainEditedEvent value)
        {
            if (earthDust == null)
            {
                return;
            }

            earthDust.transform.position = new Vector3(value.Center.x, value.Center.y, value.Center.z);
            earthDust.Play();
        }

        private void HandleWallRaised(WallRaisedEvent value)
        {
            if (earthDust == null) return;
            Vector3 start = new Vector3(value.Start.x, value.Start.y, value.Start.z);
            Vector3 end = new Vector3(value.End.x, value.End.y, value.End.z);
            earthDust.transform.position = (start + end) * 0.5f;
            earthDust.Emit(10);
        }

        private void HandleWallCollapsed(WallCollapsedEvent value)
        {
            if (earthDust == null) return;
            Vector3 start = new Vector3(value.Start.x, value.Start.y, value.Start.z);
            Vector3 end = new Vector3(value.End.x, value.End.y, value.End.z);
            earthDust.transform.position = (start + end) * 0.5f;
            earthDust.Emit(18);
        }

        private void HandleFragmentSpawned(FragmentSpawnedEvent value)
        {
            if (earthDust == null)
            {
                return;
            }

            earthDust.transform.position = new Vector3(value.Position.x, value.Position.y, value.Position.z);
            earthDust.Emit(12);
        }

        private void HandleFragmentLaunched(FragmentLaunchedEvent value)
        {
            if (earthDust == null) return;
            earthDust.transform.position = new Vector3(value.Position.x, value.Position.y, value.Position.z);
            earthDust.Emit(8);
        }

        private void HandleImpact(ImpactEvent value)
        {
            if (impactAudio != null)
            {
                impactAudio.Play();
            }

            if (earthDust != null)
            {
                earthDust.transform.position = new Vector3(value.Point.x, value.Point.y, value.Point.z);
                earthDust.Emit(Mathf.Clamp(Mathf.RoundToInt(value.Impulse * 0.02f), 4, 40));
            }
        }

        private void HandleEarthReturn(EarthReturnEvent value)
        {
            if (earthDust != null)
            {
                earthDust.transform.position = new Vector3(value.Position.x, value.Position.y, value.Position.z);
                int count = value.Stage == EarthReturnEventStage.Completed ? 18 : 7;
                earthDust.Emit(count);
            }
            if (impactAudio == null ||
                (value.Stage != EarthReturnEventStage.Subsurface &&
                 value.Stage != EarthReturnEventStage.Completed)) return;
            impactAudio.pitch = value.Stage == EarthReturnEventStage.Subsurface ? 0.72f : 0.88f;
            impactAudio.volume = Mathf.Clamp01(0.22f + Mathf.Log10(1f + value.Mass) * 0.14f);
            impactAudio.Play();
        }
    }
}
