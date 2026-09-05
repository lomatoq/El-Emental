using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class OuterStoneRingRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private static readonly ProfilerMarker CycleMarker = new ProfilerMarker("Elemental.QA.OuterStoneRing.PieceRepairCycle");
        private Scene _scene;
        private bool _opened;
        private GameObject _ring;
        private readonly Dictionary<EarthMvpBotController, bool> _bots = new();
        private readonly List<RockSnapshot> _rocks = new();

        [UnitySetUp]
        public IEnumerator LoadProductionScene()
        {
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _opened = !_scene.IsValid() || !_scene.isLoaded;
            if (_opened) yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            // The production loading gate restores its controls on Ready. Wait
            // before isolating bots, otherwise it undoes the test's suppression.
            double deadline = Time.realtimeSinceStartupAsDouble + 150;
            foreach (var root in _scene.GetRootGameObjects())
            foreach (var gate in root.GetComponentsInChildren<EarthSceneReadinessGate>(true))
            {
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
            }
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == "Outer Stone Ring") _ring = root;
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>(true))
                {
                    _bots.Add(bot, bot.enabled);
                    bot.enabled = false;
                }
            }
            Assert.That(_ring, Is.Not.Null, "Import and place the authored columns in EarthCoreSlice first.");
            foreach (var rock in _ring.GetComponentsInChildren<EarthDestructibleDecorRock>(true))
                _rocks.Add(new RockSnapshot(rock));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreProductionScene()
        {
            if (_ring != null)
                foreach (var structure in _ring.GetComponentsInChildren<EarthArenaStructure>(true))
                    if (structure.IsFractured) structure.SetMagicRepairProgress(1f);
            foreach (var rock in _rocks) rock.Restore();
            _rocks.Clear();
            foreach (var entry in _bots) if (entry.Key != null) entry.Key.enabled = entry.Value;
            _bots.Clear();
            if (_opened && _scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            _ring = null;
        }

        [UnityTest]
        public IEnumerator EveryColumnSupportsImpactEveryPieceGripAndExactRepair()
        {
            var structures = _ring.GetComponentsInChildren<EarthArenaStructure>(true);
            Assert.That(structures, Has.Length.EqualTo(7));
            int verifiedPieces = 0;
            double maximumCycleMs = 0;
            var stableIds = new HashSet<uint>();
            foreach (var structure in structures)
            {
                Assert.That(structure.IsFractured, Is.False, structure.name);
                Assert.That(structure.HasMaterialFeedback, Is.True, structure.name);
                var pieces = Field<Transform[]>(structure, "pieces");
                var positions = new Vector3[pieces.Length];
                var rotations = new Quaternion[pieces.Length];
                var scales = new Vector3[pieces.Length];
                for (int i = 0; i < pieces.Length; i++)
                {
                    positions[i] = pieces[i].localPosition;
                    rotations[i] = pieces[i].localRotation;
                    scales[i] = pieces[i].localScale;
                }
                var impact = new EarthStructureImpact(pieces[pieces.Length - 1].position,
                    Vector3.up, 220f, EarthStructureImpactKind.Projectile, structure.StructureId ^ 0xFF000000u);
                Assert.That(structure.ApplyEarthImpact(in impact), Is.True, structure.name);
                Assert.That(structure.ReleasedPieceCount, Is.InRange(1, pieces.Length),
                    "Direct impact may also release cells whose support was removed.");
                Assert.That(structure.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(structure.GetComponent<Collider>().enabled, Is.False);
                Assert.That(structure.SetMagicRepairProgress(1f), Is.True);

                for (int i = 0; i < pieces.Length; i++)
                {
                    // Blender component separation preserves shared pivots. The
                    // convex vertex mean is inside this actual cell's geometry.
                    Assert.That(structure.TryPluckCell(CellInterior(pieces[i]), out IEarthPhysicalTarget target), Is.True);
                    var pieceTarget = target as EarthArenaPiece;
                    Assert.That(pieceTarget, Is.Not.Null);
                    Assert.That(pieceTarget.PieceIndex, Is.EqualTo(i), pieces[i].name);
                    Assert.That(stableIds.Add(target.StableEarthId), Is.True, pieces[i].name);
                    Assert.That(target.IsEarthTargetValid, Is.True);
                    target.OnEarthMagicGrabbed(EarthMagicGripKind.GravityWell);
                    Assert.That(target.Body.isKinematic, Is.False);
                    Assert.That(target.Body.useGravity, Is.False);
                    Assert.That(pieceTarget.GetComponent<GravityBody>().enabled, Is.True);
                    Assert.That(pieceTarget.GetComponent<Collider>().enabled, Is.True);
                    target.Body.position += new Vector3(.23f, .17f, -.11f);
                    target.Body.rotation = Quaternion.Euler(13f, 19f, 7f) * target.Body.rotation;
                    target.OnEarthMagicReleased(EarthMagicGripKind.GravityWell);
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    bool repaired;
                    using (CycleMarker.Auto()) repaired = structure.SetMagicRepairProgress(1f);
                    maximumCycleMs = System.Math.Max(maximumCycleMs,
                        (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                    Assert.That(repaired, Is.True);
                    Assert.That(structure.IsFractured, Is.False);
                    Assert.That(structure.ReleasedPieceCount, Is.Zero);
                    Assert.That(target.IsEarthTargetValid, Is.False);
                    Assert.That(Vector3.Distance(pieces[i].localPosition, positions[i]), Is.LessThan(.0001f), pieces[i].name);
                    Assert.That(Quaternion.Angle(pieces[i].localRotation, rotations[i]), Is.LessThan(.01f), pieces[i].name);
                    Assert.That(Vector3.Distance(pieces[i].localScale, scales[i]), Is.LessThan(.0001f));
                    Assert.That(structure.GetComponent<Renderer>().enabled, Is.True);
                    Assert.That(structure.GetComponent<Collider>().enabled, Is.True);
                    verifiedPieces++;
                }
            }
            Assert.That(verifiedPieces, Is.EqualTo(85));
            Debug.Log($"[Outer Stone Ring QA] Seven impact/repair cycles and {verifiedPieces} exact cell grab/release/repair cycles passed; maximum repair {maximumCycleMs:F4} ms in Editor, marker Elemental.QA.OuterStoneRing.PieceRepairCycle. Excludes rendering and physics-step cost.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionColumnFracturePresentsAuthoredCloudAndSharedStoneChips()
        {
            EarthArenaStructure structure = _ring.GetComponentsInChildren<EarthArenaStructure>(true)[0];
            EarthArenaFractureDustPresenter dedicated =
                _ring.GetComponentInChildren<EarthArenaFractureDustPresenter>(true);
            Assert.That(dedicated, Is.Not.Null, "Outer Stone Ring requires its authored broad fracture cloud.");

            EarthMaterialFeedbackPresenter shared = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
                if (shared == null) shared = root.GetComponentInChildren<EarthMaterialFeedbackPresenter>(true);
            Assert.That(shared, Is.Not.Null, "Production scene requires shared material feedback for stone chips.");

            ParticleSystem dedicatedDust = dedicated.GetComponent<ParticleSystem>();
            ParticleSystem sharedDust = Field<ParticleSystem>(shared, "fractureDust");
            ParticleSystem sharedChips = Field<ParticleSystem>(shared, "chips");
            EarthEffectsTuningProfile profile = Field<EarthEffectsTuningProfile>(dedicated, "effectsProfile");
            EarthMaterialFeedbackHub hub = Field<EarthMaterialFeedbackHub>(structure, "materialFeedback");
            Assert.That(dedicatedDust, Is.Not.Null);
            Assert.That(sharedDust, Is.Not.Null);
            Assert.That(sharedChips, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(hub, Is.Not.Null);

            Material expectedDust = profile.Materials.FractureDust;
            Assert.That(expectedDust, Is.Not.Null);
            Assert.That(expectedDust.shader.name, Is.EqualTo("Elemental/Light Dust Mote"),
                "Fracture dust must remain sun/ambient lit instead of returning to an unlit night glow.");
            Assert.That(dedicatedDust.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                Is.SameAs(expectedDust));
            Assert.That(sharedDust.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                Is.SameAs(expectedDust));

            dedicatedDust.Clear(true);
            sharedDust.Clear(true);
            sharedChips.Clear(true);
            hub.FlushPending();

            int presentedDust = 0;
            int presentedChips = 0;
            void Capture(EarthMaterialFeedbackCue cue)
            {
                if (cue.Kind != EarthMaterialFeedbackKind.Fracture) return;
                presentedDust += cue.DustCount;
                presentedChips += cue.ChipCount;
            }

            hub.Presented += Capture;
            try
            {
                Transform[] pieces = Field<Transform[]>(structure, "pieces");
                var impact = new EarthStructureImpact(
                    CellInterior(pieces[pieces.Length - 1]), Vector3.up, 220f,
                    EarthStructureImpactKind.Projectile, structure.StructureId ^ 0xD0570001u);
                Assert.That(structure.ApplyEarthImpact(in impact), Is.True, structure.name);
                hub.FlushPending();

                Assert.That(presentedDust, Is.GreaterThanOrEqualTo(80),
                    "The shared route must retain broad contact dust for released cells.");
                Assert.That(presentedChips, Is.GreaterThanOrEqualTo(18),
                    "The shared route must retain the small mesh-stone burst.");
                Assert.That(dedicatedDust.particleCount, Is.GreaterThanOrEqualTo(profile.Fracture.MinimumCount),
                    "The authored 120-260 particle proxy-swap cloud must not be silently disabled.");
                Assert.That(sharedDust.particleCount, Is.GreaterThanOrEqualTo(presentedDust));
                Assert.That(sharedChips.particleCount, Is.GreaterThanOrEqualTo(presentedChips));
            }
            finally
            {
                hub.Presented -= Capture;
                if (structure.IsFractured) structure.SetMagicRepairProgress(1f);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingFoundationsReleasesEveryUnsupportedCell()
        {
            int cascaded = 0;
            double maximumMs = 0;
            foreach (var structure in _ring.GetComponentsInChildren<EarthArenaStructure>(true))
            {
                var definitions = Field<EarthPieceDefinition[]>(structure, "_pieceDefinitions");
                var bonds = Field<EarthBondDefinition[]>(structure, "_bondDefinitions");
                var pieces = Field<Transform[]>(structure, "pieces");
                int direct = 0;
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < definitions.Length; i++)
                {
                    bool anchoredToWorld = (definitions[i].Flags & EarthPieceFlags.Foundation) != 0;
                    for (int b = 0; b < bonds.Length; b++)
                        anchoredToWorld |= bonds[b].PieceA == i && bonds[b].PieceB == EarthBondGraph.WorldPieceIndex;
                    if (!anchoredToWorld) continue;
                    if (!structure.IsPieceReleased(i))
                    {
                        Assert.That(structure.TryAcquirePiece(i), Is.True);
                        direct++;
                    }
                }
                maximumMs = System.Math.Max(maximumMs,
                    (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                Assert.That(direct, Is.GreaterThan(0), structure.name);
                Assert.That(structure.ReleasedPieceCount, Is.EqualTo(pieces.Length), structure.name);
                cascaded += pieces.Length-direct;
                foreach (var piece in pieces)
                {
                    Assert.That(piece.GetComponent<Rigidbody>().isKinematic, Is.False, piece.name);
                    Assert.That(piece.GetComponent<GravityBody>().enabled, Is.True, piece.name);
                }
                Assert.That(structure.SetMagicRepairProgress(1f), Is.True);
                Assert.That(structure.IsFractured, Is.False);
            }
            Assert.That(cascaded, Is.GreaterThan(0));
            Debug.Log($"[Outer Stone Ring QA] {cascaded} unsupported cells automatically released; maximum complete foundation removal {maximumMs:F4} ms. Marker Elemental.Earth.ArenaFracture.ReleaseUnsupported.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PartialRepairSeatsOnlyFoundationConnectedCellsAndNeverWeldsLooseBodies()
        {
            foreach (var structure in _ring.GetComponentsInChildren<EarthArenaStructure>(true))
            {
                Assert.That(structure.SetMagicDisassemblyProgress(1f, structure.transform.position, Vector3.zero), Is.True);
                var definitions = Field<EarthPieceDefinition[]>(structure, "_pieceDefinitions");
                var bonds = Field<EarthBondDefinition[]>(structure, "_bondDefinitions");
                var bondStates = Field<EarthBondState[]>(structure, "_bondStates");
                var pieces = Field<Transform[]>(structure, "pieces");
                for (int step = 1; step < 10; step++)
                {
                    Assert.That(structure.SetMagicRepairProgress(step / 10f), Is.True);
                    var reached = new HashSet<int>();
                    for (int i = 0; i < definitions.Length; i++)
                        if (!structure.IsPieceReleased(i) && (definitions[i].Flags & EarthPieceFlags.Foundation) != 0)
                            reached.Add(i);
                    for (int pass = 0; pass < pieces.Length; pass++)
                    for (int b = 0; b < bonds.Length; b++)
                    {
                        if (!EarthBondGraph.IsStructuralConnection(bondStates[b].Phase)) continue;
                        EarthBondDefinition bond = bonds[b];
                        Assert.That(structure.IsPieceReleased(bond.PieceA), Is.False, "A repaired bond cannot terminate in a free cell.");
                        if (bond.PieceB < 0) { reached.Add(bond.PieceA); continue; }
                        Assert.That(structure.IsPieceReleased(bond.PieceB), Is.False, "A free neighbour has not been welded back yet.");
                        if (reached.Contains(bond.PieceA) || reached.Contains(bond.PieceB))
                        { reached.Add(bond.PieceA); reached.Add(bond.PieceB); }
                    }
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        bool released = structure.IsPieceReleased(i);
                        Assert.That(pieces[i].GetComponent<Rigidbody>().isKinematic, Is.EqualTo(!released));
                        if (!released) Assert.That(reached.Contains(i), Is.True, $"{pieces[i].name} must be connected to an attached foundation during partial repair.");
                    }
                }
                Assert.That(structure.SetMagicRepairProgress(1f), Is.True);
                Assert.That(structure.IsFractured, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredLooseChunksWakeUnderGravityAndCanBeReacquired()
        {
            Assert.That(_rocks, Has.Count.EqualTo(8));
            var ids = new HashSet<uint>();
            foreach (var snapshot in _rocks)
            {
                var rock = snapshot.Rock;
                Assert.That(ids.Add(rock.StableEarthId), Is.True);
                Assert.That(rock.IsEarthTargetValid, Is.True);
                rock.OnEarthMagicGrabbed(EarthMagicGripKind.GravityWell);
                Assert.That(rock.IsAnchored, Is.False);
                Assert.That(rock.Body.isKinematic, Is.False);
                Assert.That(rock.Body.constraints, Is.EqualTo(RigidbodyConstraints.None));
                Assert.That(rock.GetComponent<GravityBody>().enabled, Is.True);
                Assert.That(rock.Body.IsSleeping(), Is.False);
                rock.OnEarthMagicReleased(EarthMagicGripKind.GravityWell);
            }
            yield return new WaitForFixedUpdate();
            foreach (var snapshot in _rocks)
            {
                var rock = snapshot.Rock;
                var lastCollider = rock.LastCollisionCollider;
                var currentShape = rock.GetComponent<Collider>();
                Debug.Log($"[Outer Stone Ring Contact QA] {rock.name}: valid={rock.IsEarthTargetValid}, shattered={rock.IsShattered}, active={rock.gameObject.activeInHierarchy}, shapeEnabled={currentShape.enabled}, initialCaptured={rock.CapturedInitialOverlapCount}, initialRemaining={rock.InitialOverlapProtectionCount}, collisions={rock.ObservedCollisionCount}, protectedCollisions={rock.ProtectedInitialCollisionCount}, lastCollider={(lastCollider != null ? lastCollider.name : "none")}, lastColliderId={(lastCollider != null ? lastCollider.GetEntityId().ToString() : "none")}, lastColliderType={(lastCollider != null ? lastCollider.GetType().Name : "none")}, lastBody={(lastCollider != null && lastCollider.attachedRigidbody != null ? lastCollider.attachedRigidbody.name : "static")}, impulse={rock.LastCollisionImpulse:F5}, approach={rock.LastCollisionApproach:F5}, relativeVelocity={rock.LastCollisionRelativeVelocity}, normal={rock.LastCollisionNormal}, separation={rock.LastCollisionSeparation:F6}, lastProtected={rock.LastCollisionInitialProtected}, lastHeld={rock.LastCollisionHadMagicOwner}, mass={rock.EarthMass:F5}, velocity={rock.Body.linearVelocity}, position={rock.Body.position}.");
                Assert.That(rock.GetComponent<GravityBody>().LastAcceleration.sqrMagnitude, Is.GreaterThan(.01f), rock.name);
                Assert.That(rock.IsEarthTargetValid, Is.True, rock.name);
                Debug.Log($"[Outer Stone Ring QA] Authored-pose release {rock.name}: protected contacts={rock.ProtectedInitialCollisionCount}, last solver impulse={rock.LastProtectedCollisionImpulse:F4}, separation={rock.LastProtectedCollisionSeparation:F5}.");
                rock.OnEarthMagicGrabbed(EarthMagicGripKind.GravityWell);
                Assert.That(rock.Body.IsSleeping(), Is.False);
                rock.OnEarthMagicReleased(EarthMagicGripKind.GravityWell);
            }
        }

        [UnityTest]
        public IEnumerator InitialOverlapProtectionExpiresOnSeparationAndAllowsExplicitImpacts()
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rockObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                obstacle.transform.position = new Vector3(200, 200, 200);
                rockObject.transform.position = obstacle.transform.position + Vector3.up * .75f;
                var body = rockObject.AddComponent<Rigidbody>();
                var rock = rockObject.AddComponent<EarthDestructibleDecorRock>();
                EarthRockDebrisPool pool = null;
                foreach (var root in _scene.GetRootGameObjects())
                    if (pool == null) pool = root.GetComponentInChildren<EarthRockDebrisPool>(true);
                rock.Configure(0xE3910021u, body, rockObject.GetComponent<Collider>(), null, pool, .5f, 720f);
                UnityEngine.Physics.SyncTransforms();
                rock.OnEarthMagicGrabbed(EarthMagicGripKind.GravityWell);
                Assert.That(rock.InitialOverlapProtectionCount, Is.EqualTo(1));
                rock.OnEarthMagicReleased(EarthMagicGripKind.GravityWell);
                rock.ApplyImpact(body.position, Vector3.up, 1f);
                Assert.That(Field<float>(rock, "integrity"), Is.EqualTo(719f), "Explicit impacts are never protected.");
                yield return new WaitForFixedUpdate();
                Assert.That(rock.IsEarthTargetValid, Is.True, "Extraction contact must not shatter the rock.");
                Assert.That(rock.ProtectedInitialCollisionCount, Is.GreaterThan(0), "Exercise a real PhysX overlap contact.");
                body.position += Vector3.up * 3f;
                UnityEngine.Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(rock.InitialOverlapProtectionCount, Is.Zero, "A separated collider must lose its protection.");
            }
            finally
            {
                Object.Destroy(rockObject);
                Object.Destroy(obstacle);
            }
        }

        [UnityTest]
        public IEnumerator FastReleasedColumnCellShattersOnItsFirstPostReleaseCollision()
        {
            GameObject obstacle = null;
            try
            {
                var structure = _ring.GetComponentsInChildren<EarthArenaStructure>(true)[0];
                var pieces = Field<Transform[]>(structure, "pieces");
                Assert.That(structure.TryPluckCell(CellInterior(pieces[pieces.Length - 1]),
                    out IEarthPhysicalTarget target), Is.True);
                var cell = target as EarthArenaPiece;
                Assert.That(cell, Is.Not.Null);

                target.OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);
                Rigidbody body = target.Body;
                cell.GetComponent<GravityBody>().enabled = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = new Vector3(240f, 240f, 240f);
                UnityEngine.Physics.SyncTransforms();

                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "QA Fast Released Column Cell Obstacle";
                obstacle.transform.localScale = new Vector3(.5f, 20f, 20f);
                obstacle.transform.position = body.position + Vector3.right *
                    (cell.GetComponent<Collider>().bounds.extents.x + .75f);
                UnityEngine.Physics.SyncTransforms();

                float releasedAt = Time.time;
                target.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
                body.linearVelocity = Vector3.right * 28f;
                for (int tick = 0; tick < 8 && cell.gameObject.activeSelf; tick++)
                    yield return new WaitForFixedUpdate();

                Assert.That(Time.time - releasedAt, Is.LessThan(.20f),
                    "The impact must exercise the former detachment grace window.");
                Assert.That(cell.gameObject.activeSelf, Is.False,
                    "A deliberate throw must arm its released column cell before the first collision.");
                Assert.That(structure.ShatteredPieceCount, Is.EqualTo(1));
            }
            finally
            {
                if (obstacle != null) Object.Destroy(obstacle);
            }
        }

        [UnityTest]
        public IEnumerator FastThrownRockStillShattersAgainstNewNonoverlappingCollider()
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rockObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                obstacle.name = "QA Real Throw Obstacle";
                obstacle.transform.position = new Vector3(220, 220, 220);
                obstacle.transform.localScale = new Vector3(1, 4, 4);
                rockObject.transform.position = obstacle.transform.position - Vector3.right * 4f;
                var body = rockObject.AddComponent<Rigidbody>();
                var rock = rockObject.AddComponent<EarthDestructibleDecorRock>();
                EarthRockDebrisPool pool = null;
                foreach (var root in _scene.GetRootGameObjects())
                    if (pool == null) pool = root.GetComponentInChildren<EarthRockDebrisPool>(true);
                Assert.That(pool, Is.Not.Null);
                rock.Configure(0xE3910022u, body, rockObject.GetComponent<Collider>(), null, pool, .5f, 720f);
                UnityEngine.Physics.SyncTransforms();
                rock.OnEarthMagicGrabbed(EarthMagicGripKind.GravityWell);
                Assert.That(rock.CapturedInitialOverlapCount, Is.Zero, "Throw starts in free space.");
                rock.OnEarthMagicReleased(EarthMagicGripKind.GravityWell);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = Vector3.right * 20f;
                for (int step = 0; step < 45 && !rock.IsShattered; step++)
                    yield return new WaitForFixedUpdate();
                Debug.Log($"[Outer Stone Ring Contact QA] Real thrown collision: shattered={rock.IsShattered}, collisions={rock.ObservedCollisionCount}, approach={rock.LastCollisionApproach:F4}, relativeVelocity={rock.LastCollisionRelativeVelocity}, normal={rock.LastCollisionNormal}, impulse={rock.LastCollisionImpulse:F4}, protected={rock.LastCollisionInitialProtected}.");
                Assert.That(rock.ObservedCollisionCount, Is.GreaterThan(0), "The test must exercise a real PhysX contact.");
                Assert.That(rock.LastCollisionCollider, Is.EqualTo(obstacle.GetComponent<Collider>()));
                Assert.That(rock.LastCollisionApproach, Is.GreaterThan(1f), "Verify the closing-speed sign using an actual incoming collision.");
                Assert.That(rock.LastCollisionInitialProtected, Is.False);
                Assert.That(rock.IsShattered, Is.True, "A genuine fast throw must retain collision damage.");
            }
            finally
            {
                Object.Destroy(rockObject);
                Object.Destroy(obstacle);
            }
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static Vector3 CellInterior(Transform piece)
        {
            Vector3[] vertices = piece.GetComponent<MeshCollider>().sharedMesh.vertices;
            Assert.That(vertices.Length, Is.GreaterThan(0), piece.name);
            Vector3 center = Vector3.zero;
            foreach (Vector3 vertex in vertices) center += vertex;
            return piece.TransformPoint(center / vertices.Length);
        }

        private sealed class RockSnapshot
        {
            public readonly EarthDestructibleDecorRock Rock;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly float _integrity;
            public RockSnapshot(EarthDestructibleDecorRock rock)
            {
                Rock = rock; _position = rock.transform.position; _rotation = rock.transform.rotation;
                _integrity = Field<float>(rock, "integrity");
            }
            public void Restore()
            {
                if (Rock == null) return;
                if (!Rock.Body.isKinematic)
                {
                    Rock.Body.linearVelocity = Vector3.zero;
                    Rock.Body.angularVelocity = Vector3.zero;
                }
                Rock.transform.SetPositionAndRotation(_position, _rotation);
                Rock.Configure(Rock.StableEarthId, Rock.Body, Rock.GetComponent<Collider>(),
                    Rock.GetComponent<GravityBody>(), Field<EarthRockDebrisPool>(Rock, "debrisPool"),
                    Field<float>(Rock, "visualRadius"), _integrity);
            }
        }
    }
}
