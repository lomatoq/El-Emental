using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthAbilityPreview : MonoBehaviour
    {
        [SerializeField] private MagicInputController input;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private Transform extractionVolume;
        [SerializeField] private LineRenderer trajectory;
        [SerializeField] private LineRenderer vectorFieldGuide;
        [SerializeField] private LineRenderer platformHeightGuide;

        public void Configure(
            MagicInputController configuredInput,
            MagicExecutor configuredExecutor,
            Transform configuredExtractionVolume,
            LineRenderer configuredTrajectory,
            LineRenderer configuredVectorFieldGuide = null,
            LineRenderer configuredPlatformHeightGuide = null)
        {
            if (isActiveAndEnabled && input != null) Unsubscribe();
            input = configuredInput;
            executor = configuredExecutor;
            extractionVolume = configuredExtractionVolume;
            trajectory = configuredTrajectory;
            vectorFieldGuide = configuredVectorFieldGuide;
            platformHeightGuide = configuredPlatformHeightGuide;
            Hide();
            if (isActiveAndEnabled && input != null) Subscribe();
        }

        private void OnEnable()
        {
            if (input != null) Subscribe();
        }

        private void OnDisable()
        {
            if (input != null) Unsubscribe();
        }

        private void Update()
        {
            if (vectorFieldGuide != null)
            {
                bool field = input != null && executor != null && input.IsVectorFieldActive;
                vectorFieldGuide.gameObject.SetActive(field);
                vectorFieldGuide.positionCount = field ? 2 : 0;
                if (field)
                {
                    Vector3 start = executor.VectorFieldPoint;
                    vectorFieldGuide.SetPosition(0, start);
                    vectorFieldGuide.SetPosition(1, start + (executor.VectorFieldDirection * 4f));
                }
            }
            if (input == null || executor == null || extractionVolume == null || !input.IsFormingEarth)
                return;
            if (!executor.TryGetPreviewMetrics(EarthAbilityIds.PullRock, out MagicPreviewMetrics metrics))
                return;
            float radius = EarthGeometryBuilder.ExtractionRadius(metrics.Radius, input.BendAmount01);
            Vector3 surface = input.FormingSourceWorld;
            Vector3 up = (surface - input.PlanetCenterWorld).normalized;
            extractionVolume.gameObject.SetActive(true);
            extractionVolume.position = surface - (up * radius * 0.62f);
            extractionVolume.rotation = Quaternion.FromToRotation(Vector3.up, up);
            extractionVolume.localScale = Vector3.one * (radius * 2f);
        }

        private void Subscribe()
        {
            input.PreviewChanged += Show;
            input.PreviewCleared += Hide;
        }

        private void Unsubscribe()
        {
            input.PreviewChanged -= Show;
            input.PreviewCleared -= Hide;
        }

        private void Show(IReadOnlyList<Vector3> points)
        {
            AbilityId ability = input.SelectedAbility;
            if (ability == EarthAbilityIds.PullRock && points != null && points.Count >= 2 &&
                executor.TryGetPreviewMetrics(ability, out MagicPreviewMetrics metrics))
            {
                Vector3 surface = points[0];
                Vector3 center = points[1];
                extractionVolume.gameObject.SetActive(true);
                extractionVolume.position = center;
                Vector3 up = (surface - center).normalized;
                extractionVolume.rotation = Quaternion.FromToRotation(Vector3.up, up);
                extractionVolume.localScale = Vector3.one * (metrics.Radius * 2f);
            }
            else
            {
                extractionVolume.gameObject.SetActive(false);
            }

            if (platformHeightGuide != null)
            {
                bool platform = ability == EarthAbilityIds.RaisePlatform && points != null && points.Count >= 3;
                platformHeightGuide.gameObject.SetActive(platform);
                platformHeightGuide.positionCount = platform ? points.Count : 0;
                if (platform)
                {
                    float height = Mathf.Lerp(0.6f, 3f, input.PlatformPreviewHeight01);
                    Vector3 center = input.PlanetCenterWorld;
                    for (int index = 0; index < points.Count; index++)
                    {
                        Vector3 up = (points[index] - center).normalized;
                        platformHeightGuide.SetPosition(index, points[index] + (up * height));
                    }
                }
            }

            if (trajectory != null)
            {
                bool showTrajectory = ability == EarthAbilityIds.FlickThrow && points != null && points.Count > 1;
                trajectory.gameObject.SetActive(showTrajectory);
                trajectory.positionCount = showTrajectory ? points.Count : 0;
                if (showTrajectory)
                    for (int index = 0; index < points.Count; index++) trajectory.SetPosition(index, points[index]);
            }
            if (vectorFieldGuide != null)
            {
                vectorFieldGuide.positionCount = 0;
                vectorFieldGuide.gameObject.SetActive(false);
            }
        }

        private void Hide()
        {
            if (extractionVolume != null) extractionVolume.gameObject.SetActive(false);
            if (trajectory != null)
            {
                trajectory.positionCount = 0;
                trajectory.gameObject.SetActive(false);
            }
            if (platformHeightGuide != null)
            {
                platformHeightGuide.positionCount = 0;
                platformHeightGuide.gameObject.SetActive(false);
            }
        }
    }
}
