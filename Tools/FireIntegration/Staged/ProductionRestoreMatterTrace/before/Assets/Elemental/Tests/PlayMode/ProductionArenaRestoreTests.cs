using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class ProductionArenaRestoreTests
    {
        private Scene _prior, _arena, _foreign;
        private EarthDestructibleDecorRock _canonicalDecor;
        private EarthMatterIdentity _canonicalIdentity;
        private EarthMatterRecord _canonicalBaseline;
        private const string Output = "BuildReports/ProductionArenaRestore";
        [Serializable] private sealed class Evidence
        {
            public string utc;
            public int completedCycles, arenaKernelCount, capturedForeignRecords;
            public List<int> resetCounts = new();
            public List<int> persistentReleasedPieces = new();
            public List<int> restoredTerrainEdits = new();
            public bool foreignMatterUntouched, livesKeptDamage, authoredMatterPreserved;
            public string authoredSource;
            public float authoredMass, authoredVolume;
            public List<string> restoredMatterIds = new();
            public List<int> registeredArmorPieces = new();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= RegisterAuthoredDecorBeforeFrontendStarts;
            Time.timeScale = 1;
            if (_prior.IsValid() && _prior.isLoaded) SceneManager.SetActiveScene(_prior);
            if (_arena.IsValid() && _arena.isLoaded) yield return SceneManager.UnloadSceneAsync(_arena);
            if (_foreign.IsValid() && _foreign.isLoaded) yield return SceneManager.UnloadSceneAsync(_foreign);
        }
        [UnityTest] public IEnumerator ExistingArenaEndMatchMainBeginBotTwiceRestoresOnlyItsOwnWorld()
        {
            _prior = SceneManager.GetActiveScene();
            _foreign = SceneManager.CreateScene("Other loaded world must survive arena reset");
            SceneManager.SetActiveScene(_foreign);
            var sentinel = new GameObject("Foreign authored matter sentinel");
            var foreignKernel = sentinel.AddComponent<EarthMatterKernelBehaviour>();
            var foreignIdentity = sentinel.AddComponent<EarthMatterIdentity>();
            var foreignRecord = new EarthMatterRecord { Phase = EarthMatterPhase.FreeDynamic,
                Representation = EarthRepresentationTier.HeroPhysical, Material = EarthMaterialKind.Stone,
                Volume = 1, Mass = 100, Integrity = 1, RestPose = EarthMatterPose.Identity, CurrentPose = EarthMatterPose.Identity };
            Assert.That(foreignIdentity.Configure(foreignKernel, foreignRecord), Is.True);
            EarthMatterId sentinelId = foreignIdentity.MatterId;
            int sentinelCount = foreignKernel.ActiveRecordCount;
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False);
            // SceneLoaded runs after the saved objects' Awake, before their Start/
            // frontend captures the initial arena. Bind one REAL authored boulder
            // using its existing collider/mass; do not create a fake test stone.
            SceneManager.sceneLoaded += RegisterAuthoredDecorBeforeFrontendStarts;
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            SceneManager.sceneLoaded -= RegisterAuthoredDecorBeforeFrontendStarts;
            _arena = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(_arena);
            var gate = Find<EarthSceneReadinessGate>();
            float deadline = Time.realtimeSinceStartup + 130;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            var flow = Find<FrontendFlowController>();
            var duel = Find<EarthMvpDuelController>();
            var planet = Find<VoxelPlanetBehaviour>();
            yield return WaitForMain(flow, duel);
            ulong terrainBaseline = VoxelPlanetBehaviour.ArenaSnapshotHash(planet.CaptureArenaSnapshot());
            int baselineEdits = planet.State.EditCount;
            EarthArenaStructure source = null;
            foreach (var structure in Components<EarthArenaStructure>())
                if (structure.OrdinaryDamageEnabled && structure.name.Contains("Column")) { source = structure; break; }
            Assert.That(source, Is.Not.Null, "The shipping arena needs an actual destructible column for this proof.");
            var evidence = new Evidence { utc = DateTime.UtcNow.ToString("O"), capturedForeignRecords = sentinelCount,
                arenaKernelCount = Components<EarthMatterKernelBehaviour>().Length };
            Assert.That(evidence.arenaKernelCount, Is.GreaterThan(0));
            Assert.That(_canonicalIdentity, Is.Not.Null);
            evidence.authoredSource = _canonicalDecor.name;
            evidence.authoredMass = _canonicalBaseline.Mass; evidence.authoredVolume = _canonicalBaseline.Volume;
            AssertAuthoredMatter();
            Assert.That(EarthArenaRoundSnapshot.SceneComponents<EarthArmorPiece>(_foreign), Is.Empty,
                "Prewarmed armor must not leak into the other active scene.");
            foreach (var surf in Components<EarthSurfController>())
                if (surf.BoardTransform != null) Assert.That(surf.BoardTransform.gameObject.scene, Is.EqualTo(_arena));
            AssertSceneOwnership();
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_arena);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                DisableBots();
                Assert.That(duel.ArenaResetError, Is.Null);
                Assert.That(source.ReleasedPieceCount, Is.Zero);
                int resetBefore = duel.ArenaResetCount;
                EarthMatterId authoredBeforeReset = _canonicalIdentity.MatterId;
                Assert.That(source.TryPluckCell(source.transform.position, out var physicalPiece), Is.True);
                Assert.That(physicalPiece.IsEarthTargetValid, Is.True);
                Vector3 carvePoint = planet.transform.TransformPoint(new Vector3(.55f, .8f, .2f).normalized * (planet.State.Radius - .1f));
                planet.ApplySphereEdit(carvePoint, .45f, false);
                Assert.That(planet.State.EditCount, Is.GreaterThan(baselineEdits));
                int damagedEdits = planet.State.EditCount;
                int released = source.ReleasedPieceCount;
                Assert.That(released, Is.GreaterThan(0));
                duel.RequestKnockout(EarthDuelFighterId.Bot, RagdollHandoff.Uniform(Vector3.zero));
                Assert.That(duel.BotHealth, Is.LessThan(100));
                deadline = Time.realtimeSinceStartup + 15;
                while (duel.BotHealth < 100 && Time.realtimeSinceStartup < deadline)
                { DisableBots(); yield return null; }
                DisableBots();
                Assert.That(duel.BotHealth, Is.EqualTo(100));
                Assert.That(duel.ArenaResetCount, Is.EqualTo(resetBefore));
                Assert.That(duel.ArenaResetInProgress, Is.False);
                Assert.That(source.ReleasedPieceCount, Is.GreaterThanOrEqualTo(released));
                Assert.That(planet.State.EditCount, Is.EqualTo(damagedEdits));
                evidence.persistentReleasedPieces.Add(source.ReleasedPieceCount);
                Assert.That(_canonicalIdentity.MatterId, Is.EqualTo(authoredBeforeReset));
                AssertAuthoredMatter();
                // Exercise real armor preparation and launch: matter registration
                // occurs only when a prepared plate becomes a physical projectile.
                EarthArmorController armor = null;
                foreach (var candidate in Components<EarthArmorController>())
                    if (candidate.name == "Planet Character") { armor = candidate; break; }
                Assert.That(armor, Is.Not.Null);
                Assert.That(armor.Begin(), Is.True);
                deadline = Time.realtimeSinceStartup + 3f;
                while (armor.ActivePieceCount == 0 && Time.realtimeSinceStartup < deadline) { DisableBots(); yield return null; }
                Assert.That(armor.ActivePieceCount, Is.GreaterThan(0));
                for (int scroll = 0; scroll < 4; scroll++) armor.ApplyWheel(120f, Time.unscaledTime);
                Assert.That(armor.Phase01, Is.GreaterThan(.30f));
                Assert.That(armor.FireNearestAtPoint(armor.transform.position + armor.transform.up * 50f), Is.True);
                int registeredArmor = 0;
                foreach (var plate in Components<EarthArmorPiece>())
                {
                    Assert.That(plate.gameObject.scene, Is.EqualTo(_arena));
                    if (plate.MatterIdentity == null || !plate.MatterIdentity.TryRead(out var plateRecord) || plateRecord.Phase == EarthMatterPhase.Consumed) continue;
                    Assert.That(plate.MatterIdentity.Kernel.gameObject.scene, Is.EqualTo(_arena));
                    Assert.That(plateRecord.Shape, Is.EqualTo(EarthShapeSemantic.ArmorPlate));
                    registeredArmor++;
                }
                Assert.That(registeredArmor, Is.GreaterThan(0)); evidence.registeredArmorPieces.Add(registeredArmor);
                // Normal disabled/KO cleanup leaves no flying plate to hit a fighter
                // during the unrelated menu lifecycle assertions below.
                armor.EndArmor(EarthArmorEndReason.Disabled);
                Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
                Directory.CreateDirectory(Output);
                ScreenCapture.CaptureScreenshot(Output + "/cycle-" + cycle + "-damaged.png");
                yield return new WaitForEndOfFrame();
                flow.EndMatch(); // The same frontend action as Pause > Main Menu.
                yield return WaitForMain(flow, duel);
                Assert.That(duel.ArenaResetCount, Is.EqualTo(resetBefore + 1));
                Assert.That(source.ReleasedPieceCount, Is.Zero);
                Assert.That(planet.State.EditCount, Is.EqualTo(baselineEdits));
                Assert.That(VoxelPlanetBehaviour.ArenaSnapshotHash(planet.CaptureArenaSnapshot()), Is.EqualTo(terrainBaseline));
                AssertSceneOwnership();
                AssertAuthoredMatter();
                Assert.That(_canonicalIdentity.MatterId, Is.Not.EqualTo(authoredBeforeReset));
                bool stillOld = _canonicalIdentity.Kernel.TryGet(authoredBeforeReset, out var retired);
                Assert.That(!stillOld || retired.Phase == EarthMatterPhase.Consumed, Is.True);
                evidence.restoredMatterIds.Add(_canonicalIdentity.MatterId.ToString());
                Assert.That(foreignIdentity.MatterId, Is.EqualTo(sentinelId));
                Assert.That(foreignIdentity.TryRead(out var retained), Is.True);
                Assert.That(retained.Phase, Is.EqualTo(EarthMatterPhase.FreeDynamic));
                Assert.That(retained.Mass, Is.EqualTo(100));
                Assert.That(foreignKernel.ActiveRecordCount, Is.EqualTo(sentinelCount));
                evidence.resetCounts.Add(duel.ArenaResetCount); evidence.restoredTerrainEdits.Add(planet.State.EditCount);
                yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_arena);
                DisableBots();
                Assert.That(duel.CombatAllowed, Is.True); Assert.That(duel.ArenaResetError, Is.Null);
                Assert.That(duel.PlayerHealth, Is.EqualTo(100)); Assert.That(duel.BotHealth, Is.EqualTo(100));
                Assert.That(duel.PlayerScore, Is.Zero); Assert.That(duel.BotScore, Is.Zero);
                AssertAuthoredMatter();
                var executor = armor.GetComponent<MagicExecutor>();
                Assert.That(executor, Is.Not.Null);
                var authoredCollider = _canonicalDecor.GetComponent<Collider>();
                Assert.That(executor.TryBeginVectorField(authoredCollider, _canonicalDecor.Body,
                    authoredCollider.bounds.center, Vector3.up), Is.True, "The restored canonical rock must remain usable by the real grip path.");
                executor.CancelVectorField();
                AssertAuthoredMatter();
                ScreenCapture.CaptureScreenshot(Output + "/cycle-" + cycle + "-new-game.png");
                yield return new WaitForEndOfFrame();
                evidence.completedCycles++;
            }
            evidence.foreignMatterUntouched = evidence.livesKeptDamage = evidence.authoredMatterPreserved = true;
            File.WriteAllText(Output + "/evidence.json", JsonUtility.ToJson(evidence, true));
        }
        [UnityTest] public IEnumerator LazyRegistrySurvivesItsHostAwakening()
        {
            var root = new GameObject("Inactive registry host"); root.SetActive(false);
            try
            {
                var kernel = root.AddComponent<EarthMatterKernelBehaviour>();
                var identity = root.AddComponent<EarthMatterIdentity>();
                var record = new EarthMatterRecord { Phase = EarthMatterPhase.FreeDynamic,
                    Representation = EarthRepresentationTier.HeroPhysical, Material = EarthMaterialKind.Stone,
                    Volume = 1, Mass = 1, Integrity = 1, RestPose = EarthMatterPose.Identity, CurrentPose = EarthMatterPose.Identity };
                Assert.That(identity.Configure(kernel, record), Is.True);
                var registry = kernel.Registry; EarthMatterId id = identity.MatterId;
                root.SetActive(true); yield return null;
                Assert.That(kernel.Registry, Is.SameAs(registry));
                Assert.That(identity.MatterId, Is.EqualTo(id)); Assert.That(identity.IsRegistered, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private void RegisterAuthoredDecorBeforeFrontendStarts(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity") return;
            foreach (var decor in EarthArenaRoundSnapshot.SceneComponents<EarthDestructibleDecorRock>(scene))
                if (decor.name == "Light Push Boulder") { _canonicalDecor = decor; break; }
            Assert.That(_canonicalDecor, Is.Not.Null, "Use the saved authored Light Push Boulder, not a synthetic replacement.");
            var body = _canonicalDecor.Body;
            var shape = _canonicalDecor.GetComponent<Collider>();
            var kernel = EarthMatterKernelBehaviour.FindOrCreate(_canonicalDecor);
            _canonicalIdentity = _canonicalDecor.GetComponent<EarthMatterIdentity>();
            if (_canonicalIdentity == null || !_canonicalIdentity.TryRead(out _canonicalBaseline) || _canonicalBaseline.Phase == EarthMatterPhase.Consumed)
            {
                _canonicalIdentity?.ReleaseRetiredRepresentation();
                float volume = EarthMatterMassRuntime.EstimateColliderVolume(shape);
                var source = new EarthSourceProvenance(EarthSourceKind.StructureCell, _canonicalDecor.StableEarthId,
                    (ushort)_canonicalDecor.TargetHandle.Generation, -1, 0, float3.zero, volume, EarthProvenanceFlags.VolumeReserved);
                _canonicalIdentity = EarthMatterRuntimeBridge.EnsureIdentity(_canonicalDecor, kernel, body,
                    _canonicalDecor.IsAnchored ? EarthMatterPhase.TerrainAttached : EarthMatterPhase.FreeDynamic,
                    EarthRepresentationTier.HeroPhysical, EarthMaterialKind.Stone, EarthShapeSemantic.NaturalRock,
                    volume, body.mass, source);
            }
            Assert.That(_canonicalIdentity.TryRead(out _canonicalBaseline), Is.True);
            Assert.That(_canonicalBaseline.Phase, Is.Not.EqualTo(EarthMatterPhase.Consumed));
        }
        private void AssertAuthoredMatter()
        {
            Assert.That(_canonicalIdentity.IsRegistered, Is.True, "Filtering out all baseline matter must fail this proof.");
            Assert.That(_canonicalIdentity.TryRead(out var record), Is.True);
            Assert.That(record.Phase, Is.Not.EqualTo(EarthMatterPhase.Consumed));
            Assert.That(record.Representation, Is.EqualTo(_canonicalBaseline.Representation));
            Assert.That(record.Mass, Is.EqualTo(_canonicalBaseline.Mass).Within(.001f));
            Assert.That(record.Volume, Is.EqualTo(_canonicalBaseline.Volume).Within(.000001f));
            Assert.That(record.Source, Is.EqualTo(_canonicalBaseline.Source));
            Assert.That(record.Material, Is.EqualTo(_canonicalBaseline.Material));
            Assert.That(record.Shape, Is.EqualTo(_canonicalBaseline.Shape));
            Assert.That(_canonicalDecor.Body.mass, Is.EqualTo(_canonicalBaseline.Mass).Within(.001f));
            Assert.That(_canonicalDecor.IsEarthTargetValid, Is.True);
            Assert.That(_canonicalDecor.GetComponent<Collider>().enabled, Is.True);
            Assert.That(_canonicalIdentity.Kernel.gameObject.scene, Is.EqualTo(_arena));
        }
        private IEnumerator WaitForMain(FrontendFlowController flow, EarthMvpDuelController duel)
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while ((flow.State != FrontendState.Main || !flow.IsWorldReady || duel.ArenaResetInProgress) &&
                duel.ArenaResetError == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(duel.ArenaResetError, Is.Null);
            Assert.That(duel.ArenaResetInProgress, Is.False);
            Assert.That(flow.State, Is.EqualTo(FrontendState.Main)); Assert.That(flow.IsWorldReady, Is.True);
        }
        private void AssertSceneOwnership()
        {
            foreach (var identity in Components<EarthMatterIdentity>())
                if (identity.Kernel != null) Assert.That(identity.Kernel.gameObject.scene, Is.EqualTo(_arena), identity.name);
        }
        private void DisableBots() { foreach (var bot in Components<EarthMvpBotController>()) bot.enabled = false; }
        private T[] Components<T>() where T : Component => EarthArenaRoundSnapshot.SceneComponents<T>(_arena);
        private T Find<T>() where T : Component
        {
            var values = Components<T>(); Assert.That(values, Is.Not.Empty, typeof(T).Name); return values[0];
        }
    }
}
