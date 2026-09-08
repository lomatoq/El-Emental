using System;
using System.Collections;
using System.IO;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallRiseContinuityTests
    {
        [UnityTest]
        public IEnumerator TiltedWallRisesRigidlyAlongUpAndEmitsBoundedFoundationDebris()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production particle assets require Editor Play Mode.");
            yield break;
#else
            var effects = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(
                "Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            Assert.That(effects, Is.Not.Null);
            var root = new GameObject("Wall rise continuity and contact particles");
            root.SetActive(false);
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.SetActive(false);
            try
            {
                var hub = root.AddComponent<EarthMaterialFeedbackHub>();
                hub.Configure(effects, root.transform);
                var presenter = root.AddComponent<EarthMaterialFeedbackPresenter>();
                var dust = Particles(root, "Impact dust");
                var chips = Particles(root, "Wall base chips");
                var broad = Particles(root, "Wall base smoke");
                presenter.Configure(hub, effects, null, dust, chips,
                    shape.GetComponent<MeshFilter>().sharedMesh, broad);
                var pool = root.AddComponent<EarthWallPool>();
                pool.Configure(1, null, null);
                pool.ConfigureMaterialFeedback(hub);
                Vector3 up = new Vector3(.3f, 1f, .2f).normalized;
                Vector3 tangent = Vector3.Cross(Vector3.forward, up).normalized;
                Vector3 foundation = Vector3.up * 65f;
                int emittedDust = 0, emittedChips = 0, contacts = 0;
                float contactPlaneError = 0f;
                hub.Presented += cue =>
                {
                    if (cue.Kind != EarthMaterialFeedbackKind.Emerge) return;
                    contacts++;
                    emittedDust += cue.DustCount;
                    emittedChips += cue.ChipCount;
                    contactPlaneError = Mathf.Max(contactPlaneError,
                        Mathf.Abs(Vector3.Dot((Vector3)cue.Point - foundation, up)));
                };
                root.SetActive(true);
                EarthWall wall = pool.Acquire(foundation - tangent * 4f, foundation + tangent * 4f,
                    Vector3.zero, 2.5f, .35f, supportNormal: up, foundationEmbed: .3f);
                Assert.That(wall, Is.Not.Null);
                Vector3 anchored = wall.transform.position;
                float maxLateral = 0f, maxRotation = 0f, maxScaleError = 0f;
                int samples = 0, peakSmoke = 0, peakChips = 0;
                float deadline = Time.realtimeSinceStartup + 3f;
                // Include several settled frames, detecting the formerly nonzero
                // lateral/rotational offsets that were abruptly reset on the last frame.
                int settledFrames = 0;
                while (Time.realtimeSinceStartup < deadline && settledFrames < 5)
                {
                    yield return null;
                    samples++;
                    Vector3 displacement = wall.VisualEmergenceRoot.position - anchored;
                    maxLateral = Mathf.Max(maxLateral, Vector3.ProjectOnPlane(displacement, up).magnitude);
                    maxRotation = Mathf.Max(maxRotation,
                        Quaternion.Angle(wall.VisualEmergenceRoot.localRotation, Quaternion.identity));
                    maxScaleError = Mathf.Max(maxScaleError,
                        Vector3.Distance(wall.VisualEmergenceRoot.localScale, Vector3.one));
                    peakSmoke = Mathf.Max(peakSmoke, broad.particleCount);
                    peakChips = Mathf.Max(peakChips, chips.particleCount);
                    if (wall.IsEmergenceComplete) settledFrames++;
                }
                Assert.That(wall.IsEmergenceComplete, Is.True);
                Assert.That(samples, Is.GreaterThan(5));
                Assert.That(maxLateral, Is.LessThan(.0001f), "The stone must never slide across its rise axis.");
                Assert.That(maxRotation, Is.LessThan(.001f));
                Assert.That(maxScaleError, Is.LessThan(.00001f), "The wall rises as rigid stone, without squash.");
                Assert.That(Vector3.Distance(wall.transform.position, anchored), Is.LessThan(.0001f));
                Assert.That(wall.VisualEmergenceRoot.localPosition.magnitude, Is.LessThan(.00001f));
                Assert.That(wall.PeakEmergenceTremorMeters, Is.InRange(.00001f, .00251f));
                Assert.That(contactPlaneError, Is.LessThan(.0001f), "Dust must originate at the foundation, not the buried mesh centre.");
                Assert.That(contacts, Is.GreaterThanOrEqualTo(5));
                Assert.That(peakSmoke, Is.GreaterThanOrEqualTo(100));
                Assert.That(peakChips, Is.GreaterThanOrEqualTo(20));
                Assert.That(emittedDust, Is.LessThanOrEqualTo(960));
                Assert.That(emittedChips, Is.LessThanOrEqualTo(240));
                Directory.CreateDirectory("BuildReports/WallRise");
                File.WriteAllText("BuildReports/WallRise/continuity.json", JsonUtility.ToJson(new Evidence
                {
                    utc = DateTime.UtcNow.ToString("O"), samples = samples,
                    maxLateralMeters = maxLateral, maxRotationDegrees = maxRotation,
                    maxScaleError = maxScaleError, peakTremorMeters = wall.PeakEmergenceTremorMeters,
                    contactPlaneErrorMeters = contactPlaneError, contacts = contacts,
                    emittedDust = emittedDust, emittedChips = emittedChips,
                    peakLiveSmoke = peakSmoke, peakLiveChips = peakChips
                }, true));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(shape);
            }
#endif
        }

        private static ParticleSystem Particles(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child.AddComponent<ParticleSystem>();
        }

        [Serializable] private sealed class Evidence
        {
            public string utc;
            public int samples, contacts, emittedDust, emittedChips, peakLiveSmoke, peakLiveChips;
            public float maxLateralMeters, maxRotationDegrees, maxScaleError, peakTremorMeters, contactPlaneErrorMeters;
        }
    }
}
