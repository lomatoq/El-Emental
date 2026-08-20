using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWebArmorRuntimeTests
    {
        [UnityTest]
        public IEnumerator ArmorAssemblesAndConfirmedOverscrollReleasesPhysicalPieces()
        {
            GameObject casterObject = new GameObject("Armor Test Caster");
            casterObject.transform.position = Vector3.up * 24f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            caster.isKinematic = true;
            EarthArmorController armor = casterObject.AddComponent<EarthArmorController>();
            armor.Configure(caster, null, null, null);

            Assert.That(armor.Begin(), Is.True);
            for (int tick = 0; tick < 18; tick++) yield return new WaitForFixedUpdate();
            Assert.That(armor.IsActive, Is.True);
            Assert.That(armor.ActivePieceCount, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            var registered = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            Assert.That(armor.CopyActivePiecesNonAlloc(registered),
                Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            for (int index = 0; index < registered.Length; index++)
            {
                Assert.That(registered[index].MatterIdentity, Is.Not.Null);
                Assert.That(registered[index].MatterIdentity.TryRead(out EarthMatterRecord record), Is.True);
                Assert.That(record.Shape, Is.EqualTo(EarthShapeSemantic.ArmorPlate));
                Assert.That(record.Source.Kind, Is.EqualTo(EarthSourceKind.TerrainEdit));
            }
            for (int step = 0; step < 8; step++) armor.ApplyWheel(120f, Time.unscaledTime);
            Assert.That(armor.Phase01, Is.EqualTo(1f).Within(0.001f));
            Assert.That(armor.ApplyWheel(120f, Time.unscaledTime),
                Is.EqualTo(EarthArmorInputResult.OverscrollArmed));
            Assert.That(armor.ApplyWheel(120f, Time.unscaledTime + 0.1f),
                Is.EqualTo(EarthArmorInputResult.RadialRelease));
            armor.ReleaseRadially();
            yield return new WaitForFixedUpdate();
            Assert.That(armor.IsActive, Is.False);

            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArmorConformsToBodyCollidersThenExpandsAsHemisphereWithoutKickingCaster()
        {
            GameObject casterObject = new GameObject("Armor Body Shell Caster");
            casterObject.transform.position = Vector3.up * 24f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            caster.mass = 12f;
            CapsuleCollider root = casterObject.AddComponent<CapsuleCollider>();
            root.height = 1.8f;
            root.radius = 0.34f;
            Collider[] bodyColliders =
            {
                AddBodyCollider(casterObject.transform, "Chest", new Vector3(0f, 0.35f, 0f), new Vector3(0.72f, 0.52f, 0.56f)),
                AddBodyCollider(casterObject.transform, "Head", new Vector3(0f, 1.05f, 0f), new Vector3(0.58f, 0.58f, 0.58f)),
                AddBodyCollider(casterObject.transform, "Arm L", new Vector3(-0.48f, 0.35f, 0f), new Vector3(0.20f, 0.65f, 0.20f)),
                AddBodyCollider(casterObject.transform, "Arm R", new Vector3(0.48f, 0.35f, 0f), new Vector3(0.20f, 0.65f, 0.20f)),
                AddBodyCollider(casterObject.transform, "Leg L", new Vector3(-0.22f, -0.65f, 0f), new Vector3(0.24f, 0.84f, 0.26f)),
                AddBodyCollider(casterObject.transform, "Leg R", new Vector3(0.22f, -0.65f, 0f), new Vector3(0.24f, 0.84f, 0.26f))
            };
            EarthArmorController armor = casterObject.AddComponent<EarthArmorController>();
            armor.Configure(caster, null, null, null);
            Assert.That(armor.Begin(), Is.True);
            for (int tick = 0; tick < 22; tick++) yield return new WaitForFixedUpdate();

            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int count = armor.CopyActivePiecesNonAlloc(pieces);
            Assert.That(count, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            int bodyConforming = 0;
            for (int index = 0; index < count; index++)
            {
                EarthArmorPiece piece = pieces[index];
                if (index == 0)
                {
                    var heightBands = new HashSet<int>();
                    Vector3[] vertices = piece.OwnedMesh.vertices;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                        heightBands.Add(Mathf.RoundToInt(vertices[vertex].y * 100f));
                    Assert.That(heightBands.Count, Is.GreaterThanOrEqualTo(5),
                        "A geological armor stone needs a bevel/belly/top profile, not one extruded box side.");
                }
                AssertMeshFacesOutward(piece.OwnedMesh);
                Assert.That(piece.transform.localScale.y,
                    Is.LessThan(Mathf.Min(piece.transform.localScale.x, piece.transform.localScale.z)));
                float closestGap = float.PositiveInfinity;
                for (int colliderIndex = 0; colliderIndex < bodyColliders.Length; colliderIndex++)
                {
                    Collider bodyCollider = bodyColliders[colliderIndex];
                    closestGap = Mathf.Min(
                        closestGap,
                        Vector3.Distance(piece.transform.position, bodyCollider.ClosestPoint(piece.transform.position)));
                    Assert.That(UnityEngine.Physics.GetIgnoreCollision(piece.PieceCollider, bodyCollider), Is.True);
                }
                if (closestGap <= 0.14f) bodyConforming++;
            }
            Assert.That(bodyConforming, Is.GreaterThanOrEqualTo(count - 6),
                "Armor stones must follow the actual head/torso/limb surfaces, not a root cylinder.");

            for (int step = 0; step < 8; step++) armor.ApplyWheel(120f, Time.unscaledTime);
            // Formation changes are intentionally physical-looking flights rather
            // than transform snaps. Give the critically damped transition time to
            // settle before judging the final hemisphere topology.
            for (int tick = 0; tick < 36; tick++) yield return new WaitForFixedUpdate();
            Vector3 hemisphereCenter = caster.worldCenterOfMass - Vector3.up * 0.29f;
            float minimumHeight = float.PositiveInfinity;
            float minimumHorizontal = float.PositiveInfinity;
            float maximumHorizontal = 0f;
            for (int index = 0; index < count; index++)
            {
                Vector3 offset = pieces[index].transform.position - hemisphereCenter;
                minimumHeight = Mathf.Min(minimumHeight, Vector3.Dot(offset, Vector3.up));
                float horizontal = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
                minimumHorizontal = Mathf.Min(minimumHorizontal, horizontal);
                maximumHorizontal = Mathf.Max(maximumHorizontal, horizontal);
            }
            Assert.That(minimumHeight, Is.GreaterThan(-0.08f));
            Assert.That(maximumHorizontal - minimumHorizontal, Is.GreaterThan(0.75f),
                "A hemisphere needs latitude-dependent radius; a cylinder keeps every horizontal radius equal.");

            armor.ReleaseRadially();
            for (int tick = 0; tick < 4; tick++) yield return new WaitForFixedUpdate();
            Assert.That(caster.linearVelocity.magnitude, Is.LessThan(0.05f),
                "Owned armor projectiles must never impart their launch impulse to the caster.");

            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArmorKeepsAnimatedBodyAliveAndFormsUniformNonIntersectingSkin()
        {
            GameObject casterObject = new GameObject("Visible Armor Caster");
            casterObject.transform.position = Vector3.up * 24f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            caster.isKinematic = true;
            casterObject.AddComponent<Animator>();

            GameObject visibleBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visibleBody.name = "Visible Character Body";
            visibleBody.transform.SetParent(casterObject.transform, false);
            visibleBody.transform.localPosition = Vector3.up * 0.15f;
            visibleBody.transform.localScale = new Vector3(0.72f, 0.92f, 0.58f);
            Renderer bodyRenderer = visibleBody.GetComponent<Renderer>();
            Material originalBodyMaterial = bodyRenderer.sharedMaterial;

            EarthArmorController armor = casterObject.AddComponent<EarthArmorController>();
            armor.Configure(caster, null, null, null);
            Assert.That(armor.Begin(), Is.True);
            for (int tick = 0; tick < 22; tick++) yield return new WaitForFixedUpdate();

            Assert.That(bodyRenderer.enabled, Is.True,
                "The armor is an external shell; it must never replace or hide the animated hero.");
            Assert.That(bodyRenderer.sharedMaterial, Is.SameAs(originalBodyMaterial),
                "Compact armor must not repaint the animated character with the stone material.");
            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int count = armor.CopyActivePiecesNonAlloc(pieces);
            Assert.That(count, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            int frontPieces = 0;
            int rearPieces = 0;
            int severeIntersections = 0;
            var meshVertexCounts = new HashSet<int>();
            var footprintSignatures = new HashSet<string>();
            string intersectionDetails = string.Empty;
            Bounds shellBounds = new Bounds(pieces[0].transform.position, Vector3.zero);
            Vector3 bodyCenter = bodyRenderer.bounds.center;
            for (int index = 0; index < count; index++)
            {
                Vector3 scale = pieces[index].transform.localScale;
                Mesh armorMesh = pieces[index].OwnedMesh;
                meshVertexCounts.Add(armorMesh.vertexCount);
                Vector3 firstRimVertex = armorMesh.vertices[1];
                footprintSignatures.Add(
                    $"{armorMesh.vertexCount}:{firstRimVertex.x:F2}:{firstRimVertex.z:F2}");
                Assert.That(Mathf.Max(scale.x, scale.z), Is.InRange(0.14f, 0.52f),
                    "Uniform coverage needs many fitted tiles, not a few giant crossed slabs.");
                Vector3 relative = pieces[index].transform.position - bodyCenter;
                if (relative.z > 0.12f) frontPieces++;
                if (relative.z < -0.12f) rearPieces++;
                shellBounds.Encapsulate(pieces[index].transform.position);
                for (int other = index + 1; other < count; other++)
                {
                    if (!UnityEngine.Physics.ComputePenetration(
                            pieces[index].PieceCollider,
                            pieces[index].transform.position,
                            pieces[index].transform.rotation,
                            pieces[other].PieceCollider,
                            pieces[other].transform.position,
                            pieces[other].transform.rotation,
                            out _,
                            out float depth)) continue;
                    if (depth > 0.035f)
                    {
                        severeIntersections++;
                        intersectionDetails += $" [{index},{other}]={depth:F3}";
                    }
                }
            }
            Assert.That(frontPieces, Is.GreaterThanOrEqualTo(18));
            Assert.That(rearPieces, Is.GreaterThanOrEqualTo(18));
            Assert.That(severeIntersections, Is.LessThanOrEqualTo(2),
                "Armor plates may kiss at chipped seams but must not pass through one another." +
                intersectionDetails);
            Assert.That(meshVertexCounts.Count, Is.GreaterThanOrEqualTo(4),
                "The shell needs visibly different geological silhouettes, not one repeated plate mesh.");
            Assert.That(footprintSignatures.Count, Is.GreaterThanOrEqualTo(6),
                "Armor plate outlines must vary independently across the body.");
            Assert.That(shellBounds.size.x, Is.GreaterThan(bodyRenderer.bounds.size.x * 0.82f));
            Assert.That(shellBounds.size.y, Is.GreaterThan(bodyRenderer.bounds.size.y * 0.82f),
                $"Armor did not span the animated body height. shell={shellBounds}, body={bodyRenderer.bounds}.");
            Assert.That(shellBounds.size.z, Is.GreaterThan(bodyRenderer.bounds.size.z * 0.82f),
                "Armor must wrap the visible body in depth instead of reading as a few side pebbles.");

            armor.ReleaseAsDebris();
            Assert.That(bodyRenderer.enabled, Is.True);
            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpandedArmorFiresSelectedPlateThenRemainingDirectedVolleyWithoutCasterRecoil()
        {
            GameObject casterObject = new GameObject("Armor Projectile Caster");
            casterObject.transform.position = Vector3.up * 24f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            CapsuleCollider casterCollider = casterObject.AddComponent<CapsuleCollider>();
            EarthArmorController armor = casterObject.AddComponent<EarthArmorController>();
            armor.Configure(caster, null, null, null);

            Assert.That(armor.Begin(), Is.True);
            for (int step = 0; step < 5; step++) armor.ApplyWheel(120f, Time.unscaledTime);
            for (int tick = 0; tick < 18; tick++) yield return new WaitForFixedUpdate();
            Assert.That(armor.Phase01, Is.GreaterThan(0.30f));
            Assert.That(armor.ControllablePieceCount, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));

            Vector3 casterVelocity = caster.linearVelocity;
            Assert.That(armor.FireNearest(Vector3.forward), Is.True,
                "LMB needs a production action on expanded armor, not only a visual dome.");
            Assert.That(armor.ControllablePieceCount,
                Is.EqualTo(EarthArmorProfile.MaximumPieceCount - 1));
            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            armor.CopyActivePiecesNonAlloc(pieces);
            int released = 0;
            for (int index = 0; index < pieces.Length; index++)
            {
                if (pieces[index] == null || !pieces[index].IsReleased) continue;
                released++;
                Assert.That(pieces[index].gameObject.layer, Is.EqualTo(2),
                    "Released armor stays physical and targetable but must remain invisible to camera collision.");
                Assert.That(Vector3.Dot(pieces[index].Body.linearVelocity, Vector3.forward), Is.GreaterThan(25f));
            }
            Assert.That(released, Is.EqualTo(1));

            Assert.That(armor.FireAll(Vector3.forward),
                Is.EqualTo(EarthArmorProfile.MaximumPieceCount - 1),
                "RMB must launch the remaining shell as a compact aimed fan.");
            yield return new WaitForFixedUpdate();
            Assert.That(armor.IsActive, Is.False);
            Assert.That(Vector3.Distance(caster.linearVelocity, casterVelocity), Is.LessThan(0.01f));

            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WheelDownRecallsOnlyTheSurvivingReleasedArmorPlate()
        {
            GameObject casterObject = new GameObject("Armor Recall Caster");
            casterObject.transform.position = Vector3.up * 24f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            casterObject.AddComponent<CapsuleCollider>();
            EarthArmorController armor = casterObject.AddComponent<EarthArmorController>();
            armor.Configure(caster, null, null, null);

            Assert.That(armor.Begin(), Is.True);
            for (int step = 0; step < 5; step++) armor.ApplyWheel(120f, Time.unscaledTime);
            for (int tick = 0; tick < 18; tick++) yield return new WaitForFixedUpdate();
            Assert.That(armor.FireNearest(Vector3.forward), Is.True);
            Assert.That(armor.ControllablePieceCount,
                Is.EqualTo(EarthArmorProfile.MaximumPieceCount - 1));

            EarthArmorInputResult result = armor.ApplyWheel(-120f, Time.unscaledTime);
            Assert.That(result, Is.EqualTo(EarthArmorInputResult.PhaseChanged));
            Assert.That(armor.ControllablePieceCount, Is.EqualTo(EarthArmorProfile.MaximumPieceCount),
                "Wheel-down should recall the surviving physical plate into its persistent slot.");
            for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();

            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int count = armor.CopyActivePiecesNonAlloc(pieces);
            int released = 0;
            for (int index = 0; index < count; index++)
                if (pieces[index] != null && pieces[index].IsReleased) released++;
            Assert.That(released, Is.Zero,
                "Recall may not respawn a replacement while leaving the original plate in debris state.");

            armor.ReleaseAsDebris();
            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FastConvexRockCannotTunnelThroughPlatform()
        {
            EarthPlatformProfile platformProfile = ScriptableObject.CreateInstance<EarthPlatformProfile>();
            GameObject root = new GameObject("Platform Sweep Runtime");
            root.SetActive(false);
            EarthPlatformPool platformPool = root.AddComponent<EarthPlatformPool>();
            platformPool.Configure(1, null, platformProfile);
            EarthFragmentPool fragmentPool = root.AddComponent<EarthFragmentPool>();
            fragmentPool.Configure(2, null, null);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            root.SetActive(true);

            var path = new List<float3>
            {
                new float3(-3f, 10f, -3f), new float3(3f, 10f, -3f),
                new float3(3f, 10f, 3f), new float3(-3f, 10f, 3f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(path, float3.zero);
            EarthPlatform platform = platformPool.Acquire(in geometry, 1f, 0.25f);
            for (int tick = 0; tick < 45; tick++) yield return new WaitForFixedUpdate();
            int impacts = 0;
            executor.Events.ImpactOccurred += _ => impacts++;
            Vector3 up = platform.SurfaceUp;
            EarthFragment fragment = fragmentPool.Acquire(
                executor,
                platform.SurfaceTopPoint + up * 4f,
                0.35f,
                12f);
            fragment.StopBendControl();
            fragment.Body.linearVelocity = -up * 38f;
            for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();

            float side = Vector3.Dot(fragment.Body.worldCenterOfMass - platform.SurfaceTopPoint, up);
            Assert.That(side, Is.GreaterThan(-0.5f), "The rock crossed the complete platform volume.");
            Assert.That(impacts, Is.EqualTo(1), "The sweep must emit exactly one impact without callback duplication.");

            Object.Destroy(root);
            Object.Destroy(platformProfile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlatformVoronoiPiecesReassembleIntoSolidCollider()
        {
            EarthPlatformProfile profile = ScriptableObject.CreateInstance<EarthPlatformProfile>();
            GameObject root = new GameObject("Platform Repair Runtime");
            root.SetActive(false);
            EarthPlatformPool pool = root.AddComponent<EarthPlatformPool>();
            pool.Configure(1, null, profile);
            root.SetActive(true);
            var path = new List<float3>
            {
                new float3(-3f, 24f, -2f), new float3(3f, 24f, -2f),
                new float3(3.5f, 24f, 1f), new float3(0f, 24f, 2.8f),
                new float3(-3.2f, 24f, 1.4f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(path, float3.zero);
            EarthPlatform platform = pool.Acquire(in geometry, 1.2f, 0.25f);
            for (int tick = 0; tick < 45; tick++) yield return new WaitForFixedUpdate();
            Assert.That(platform.ApplyStructureImpact(platform.SurfaceTopPoint, Vector3.forward, 2400f), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(platform.ActivePieceCount, Is.InRange(28, 48));
            var activeTargets = new IEarthPhysicalTarget[48];
            int activeCount = platform.CopyActiveTargetsNonAlloc(activeTargets);
            Assert.That(activeCount, Is.EqualTo(platform.ActivePieceCount));
            Mesh firstMesh = ((EarthPlatformPiece)activeTargets[0]).GetComponent<MeshFilter>().sharedMesh;
            Mesh secondMesh = ((EarthPlatformPiece)activeTargets[1]).GetComponent<MeshFilter>().sharedMesh;
            Assert.That(firstMesh, Is.Not.SameAs(secondMesh));
            Assert.That(platform.TryBeginRepair(10u, 1f), Is.True);
            for (int tick = 0; tick < 240 && platform.IsFractured; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(platform.IsFractured, Is.False);
            Assert.That(platform.GetComponent<MeshCollider>().enabled, Is.True);
            Assert.That(platform.ActivePieceCount, Is.Zero);

            Object.Destroy(root);
            Object.Destroy(profile);
            yield return null;
        }

        private static Collider AddBodyCollider(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            BoxCollider collider = part.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private static void AssertMeshFacesOutward(Mesh mesh)
        {
            Assert.That(mesh, Is.Not.Null);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            Vector3 center = mesh.bounds.center;
            for (int index = 0; index < normals.Length; index++)
            {
                Assert.That(float.IsFinite(normals[index].x) && float.IsFinite(normals[index].y) &&
                            float.IsFinite(normals[index].z), Is.True, "Armor normals must stay finite.");
                Assert.That(normals[index].sqrMagnitude, Is.GreaterThan(0.5f));
            }
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                Vector3 faceCenter = (a + b + c) / 3f;
                Assert.That(Vector3.Dot(faceNormal, faceCenter - center), Is.GreaterThan(0.000001f),
                    "Armor mesh contains an inward-facing triangle (broken winding/normals).");
            }
        }
    }
}
