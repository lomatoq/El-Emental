using Elemental.Runtime.World;
using Elemental.Simulation.Fields;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class AirFieldVisualizer : MonoBehaviour
    {
        [SerializeField] private FieldWorldBehaviour fieldWorld;
        [SerializeField] private LineRenderer[] traces;
        [SerializeField] private ParticleSystem smoke;

        public void Configure(FieldWorldBehaviour configuredFieldWorld, LineRenderer[] configuredTraces, ParticleSystem configuredSmoke)
        {
            fieldWorld = configuredFieldWorld;
            traces = configuredTraces;
            smoke = configuredSmoke;
        }

        private void LateUpdate()
        {
            int count = fieldWorld?.World?.Count ?? 0;
            if (traces != null)
            {
                for (int index = 0; index < traces.Length; index++)
                {
                    LineRenderer trace = traces[index];
                    if (trace == null)
                    {
                        continue;
                    }

                    if (index >= count)
                    {
                        trace.positionCount = 0;
                        continue;
                    }

                    Draw(fieldWorld.World.GetRegion(index), trace);
                }
            }

            if (smoke != null)
            {
                ParticleSystem.EmissionModule emission = smoke.emission;
                emission.rateOverTime = count * 8f;
            }
        }

        private static void Draw(in FieldRegion region, LineRenderer trace)
        {
            Vector3 center = ToVector3(region.Center);
            Vector3 axis = ToVector3(region.Axis);
            int points = region.Kind == AirFieldKind.Vortex ? 18 : 4;
            trace.positionCount = points;
            trace.startColor = ColorFor(region.Kind);
            trace.endColor = new Color(trace.startColor.r, trace.startColor.g, trace.startColor.b, 0.08f);
            if (region.Kind == AirFieldKind.Vortex)
            {
                Vector3 basis = Vector3.Cross(axis, Vector3.right);
                if (basis.sqrMagnitude < 0.001f)
                {
                    basis = Vector3.Cross(axis, Vector3.forward);
                }
                basis.Normalize();
                Vector3 second = Vector3.Cross(axis, basis).normalized;
                for (int index = 0; index < points; index++)
                {
                    float t = index / (float)(points - 1);
                    float angle = t * Mathf.PI * 3f;
                    float radius = region.Radius * (0.2f + (0.7f * t));
                    trace.SetPosition(index, center + (axis * ((t - 0.5f) * region.Length)) +
                        ((basis * Mathf.Cos(angle) + second * Mathf.Sin(angle)) * radius));
                }
            }
            else
            {
                Vector3 end = center + (axis * Mathf.Max(region.Length, region.Radius));
                Vector3 side = Vector3.Cross(axis, Vector3.right).normalized * region.Radius;
                trace.SetPosition(0, center - side);
                trace.SetPosition(1, center);
                trace.SetPosition(2, end);
                trace.SetPosition(3, center + side);
            }
        }

        private static Color ColorFor(AirFieldKind kind)
        {
            switch (kind)
            {
                case AirFieldKind.GustCorridor: return new Color(0.15f, 0.85f, 1f, 0.85f);
                case AirFieldKind.Vortex: return new Color(0.7f, 0.35f, 1f, 0.85f);
                case AirFieldKind.LiftColumn: return new Color(0.2f, 1f, 0.5f, 0.85f);
                default: return new Color(1f, 0.9f, 0.2f, 0.85f);
            }
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
