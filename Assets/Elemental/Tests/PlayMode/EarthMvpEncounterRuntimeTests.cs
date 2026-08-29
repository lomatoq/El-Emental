using System;
using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthMvpEncounterRuntimeTests
    {
        [UnityTest]
        public IEnumerator ZzzAcceptedMvpEvidenceCompletesWithProfilerAndCaptures()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            const string statusPath = "BuildReports/Mvp01RescueCurrent.json";
            if (File.Exists(statusPath)) File.Delete(statusPath);
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            VisualQaCaptureBehaviour qa = FindInScene<VisualQaCaptureBehaviour>(scene);
            Assert.That(qa, Is.Not.Null);
            Assert.That(qa.BeginMvpRescueEvidence(), Is.True);
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!File.Exists(statusPath) && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool statusExists = File.Exists(statusPath);
            string status = statusExists ? File.ReadAllText(statusPath) : string.Empty;
            bool profilerExists = File.Exists("BuildReports/Mvp01Profiler.json");
            bool captureExists = File.Exists("BuildReports/Mvp01RescueCurrent.png");

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
            Assert.That(statusExists, Is.True, "Accepted evidence did not finish within 45 seconds.");
            Assert.That(status, Does.Contain("\"success\": true"));
            Assert.That(profilerExists, Is.True);
            Assert.That(captureExists, Is.True);
        }

        [UnityTest]
        public IEnumerator ShippingSceneContainsLargeRumbleDuelCourtAndOneActiveLinebreaker()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            planet?.ResetQueueTimingTelemetry();
            float maximumFrameDeltaMilliseconds = 0f;
            var coldFrameDeltaMilliseconds = new float[481];
            int coldFrameCount = 0;
            yield return null;
            RecordFrameDelta(
                coldFrameDeltaMilliseconds,
                ref coldFrameCount,
                ref maximumFrameDeltaMilliseconds);
            EarthMvpBotController bot = FindInScene<EarthMvpBotController>(scene);
            EarthMvpBotPresenter presenter = FindInScene<EarthMvpBotPresenter>(scene);
            EarthFragmentPool fragmentPool = FindInScene<EarthFragmentPool>(scene);
            EarthRockDebrisPool debrisPool = FindInScene<EarthRockDebrisPool>(scene);
            GameObject arena = FindByName(scene, "Rumble Stone Amphitheatre");
            bool hasRumbleArenaMaterial = HasShader(arena, "Elemental/Graphics V5/Rumble Rock Lit");
            Animator botAnimator = bot != null ? bot.GetComponentInChildren<Animator>(true) : null;
            GameObject player = FindByName(scene, "Planet Character");
            Animator playerAnimator = player != null ? player.GetComponentInChildren<Animator>(true) : null;
            HumanoidSecondaryMotion playerSecondary = player != null
                ? player.GetComponentInChildren<HumanoidSecondaryMotion>(true)
                : null;
            HumanoidSecondaryMotion botSecondary = bot != null
                ? bot.GetComponentInChildren<HumanoidSecondaryMotion>(true)
                : null;
            bool playerUsesMappedRumbleLook = UsesMappedLinebreakerLook(player);
            bool botUsesMappedRumbleLook = bot != null && UsesMappedLinebreakerLook(bot.gameObject);
            bool hasHumanoidBot = botAnimator != null && botAnimator.avatar != null &&
                                  botAnimator.avatar.isValid && botAnimator.avatar.isHuman &&
                                  !botAnimator.applyRootMotion;
            bool sharesPlayerRig = hasHumanoidBot && playerAnimator != null &&
                                   botAnimator.avatar == playerAnimator.avatar &&
                                   botAnimator.runtimeAnimatorController == playerAnimator.runtimeAnimatorController;
            Debug.Log(
                $"[Elemental.Tests] Linebreaker refs: playerAvatar={playerAnimator?.avatar?.name ?? "null"}, " +
                $"botAvatar={botAnimator?.avatar?.name ?? "null"}, " +
                $"sameAvatar={botAnimator != null && playerAnimator != null && botAnimator.avatar == playerAnimator.avatar}, " +
                $"playerController={playerAnimator?.runtimeAnimatorController?.name ?? "null"}, " +
                $"botController={botAnimator?.runtimeAnimatorController?.name ?? "null"}, " +
                $"sameController={botAnimator != null && playerAnimator != null && botAnimator.runtimeAnimatorController == playerAnimator.runtimeAnimatorController}.");
            HumanoidCharacterPresentation botSharedPresentation = bot != null
                ? bot.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                : null;
            bool botHasNoPlayerOwnership = bot != null &&
                                           bot.GetComponentInChildren<MagicInputController>(true) == null &&
                                           bot.GetComponentInChildren<MagicExecutor>(true) == null &&
                                           bot.GetComponentInChildren<PlayerInput>(true) == null;
            HumanoidRagdollRig playerRagdoll = player != null
                ? player.GetComponentInChildren<HumanoidRagdollRig>(true)
                : null;
            HumanoidRagdollRig botRagdoll = bot != null
                ? bot.GetComponentInChildren<HumanoidRagdollRig>(true)
                : null;
            bool oldStoneBodyRemoved = bot != null &&
                                       FindChildByName(bot.transform, "Linebreaker Torso") == null &&
                                       FindChildByName(bot.transform, "Linebreaker Head") == null;
            Mesh heroShape = fragmentPool != null ? fragmentPool.ResolveShapeVariant(0) : null;
            Mesh debrisShape = debrisPool != null ? debrisPool.ResolveShapeVariant(0) : null;
            GameObject lightPushBoulder = FindByName(scene, "Light Push Boulder");
            Mesh pushShape = lightPushBoulder != null
                ? lightPushBoulder.GetComponent<MeshFilter>()?.sharedMesh
                : null;
            bool usesCenteredV5Physics = IsCenteredV5PhysicsMesh(heroShape, "V5_Physics_Boulder_") &&
                                         IsCenteredV5PhysicsMesh(debrisShape, "V5_Physics_Pebble_") &&
                                         IsCenteredV5PhysicsMesh(pushShape, "V5_Physics_Boulder_");
            int linebreakerCount = CountInScene<EarthMvpBotController>(scene);
            int liveDirectionalLights = CountLiveDirectionalLights(scene);
            bool hasPlanet = planet != null;
            float planetRadius = planet != null ? planet.Radius : 0f;
            bool hasArena = arena != null;
            bool hasBot = bot != null;
            bool hasPresenter = presenter != null;
            bool botUsesBlueTint = bot != null && UsesBlueTint(bot.gameObject);
            bool sampled = false;
            for (int frame = 0; frame < 180 && bot != null && !bot.HasSampled; frame++)
            {
                yield return null;
                RecordFrameDelta(
                    coldFrameDeltaMilliseconds,
                    ref coldFrameCount,
                    ref maximumFrameDeltaMilliseconds);
            }
            if (bot != null) sampled = bot.HasSampled;

            for (int frame = 0; frame < 300 && planet != null &&
                 (planet.PendingRenderCount > 0 || planet.PendingColliderCount > 0); frame++)
            {
                yield return null;
                RecordFrameDelta(
                    coldFrameDeltaMilliseconds,
                    ref coldFrameCount,
                    ref maximumFrameDeltaMilliseconds);
            }
            bool planetQueuesDrained = planet != null &&
                                       planet.PendingRenderCount == 0 &&
                                       planet.PendingColliderCount == 0;
            double renderQueuePeak = planet != null ? planet.PeakRenderQueueMilliseconds : double.MaxValue;
            double colliderQueuePeak = planet != null ? planet.PeakColliderQueueMilliseconds : double.MaxValue;
            float frameDeltaP95Milliseconds = ComputePercentile95(coldFrameDeltaMilliseconds, coldFrameCount);
            Debug.Log($"[Elemental.Tests] M11 radius-36 cold run: frames={coldFrameCount}, " +
                      $"frame p95={frameDeltaP95Milliseconds:0.00} ms, " +
                      $"max frame delta={maximumFrameDeltaMilliseconds:0.00} ms, " +
                      $"render queue peak={renderQueuePeak:0.00} ms, " +
                      $"collider queue peak={colliderQueuePeak:0.00} ms, " +
                      $"chunks={planet?.RuntimeChunkCount ?? 0}.");

            bool finiteBotPosition = bot != null &&
                                     float.IsFinite(bot.transform.position.x) &&
                                     float.IsFinite(bot.transform.position.y) &&
                                     float.IsFinite(bot.transform.position.z);
            bool botIsNotDirectMagicTarget = bot != null &&
                                             bot.GetComponent<PhysicalImpactTarget>() == null;
            bool isolatedFromLookdevDemo = FindInScene<RumbleLookdevSceneGuard>(scene) == null &&
                                           FindInScene<RumbleLensDirector>(scene) == null &&
                                           FindInScene<RumbleEarthVfxDemo>(scene) == null;
            bool obsoleteTargetsRemoved = FindByName(scene, "Earth Impact Dummy") == null &&
                                          FindByName(scene, "Earth Combat Scout") == null &&
                                          FindByName(scene, "Earth Combat Sentinel") == null &&
                                          FindByName(scene, "Earth Combat Trap") == null;
            int arenaPieceCount = arena != null ? arena.transform.childCount : 0;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(loaded, Is.True);
            Assert.That(hasPlanet, Is.True);
            Assert.That(planetRadius, Is.EqualTo(36f).Within(0.001f));
            Assert.That(hasArena, Is.True);
            Assert.That(arenaPieceCount, Is.InRange(20, 32));
            Assert.That(hasRumbleArenaMaterial, Is.True);
            Assert.That(hasBot, Is.True);
            Assert.That(hasPresenter, Is.True);
            Assert.That(botUsesBlueTint, Is.True,
                "The rival must enter the shipping scene with a persistent blue presentation tint.");
            Assert.That(linebreakerCount, Is.EqualTo(1));
            Assert.That(hasHumanoidBot, Is.True);
            Assert.That(sharesPlayerRig, Is.True,
                "The rival must use the same Humanoid Avatar and controller as the player.");
            Assert.That(playerUsesMappedRumbleLook, Is.True,
                "The player must keep the mapped Linebreaker texture under the shared Rumble character shader.");
            Assert.That(botUsesMappedRumbleLook, Is.True,
                "The rival must keep the mapped Linebreaker texture under the shared Rumble character shader.");
            Assert.That(playerSecondary, Is.Not.Null);
            Assert.That(botSecondary, Is.Not.Null);
            Assert.That(playerSecondary.IsConfigured && botSecondary.IsConfigured, Is.True);
            Assert.That(playerSecondary.TailBoneCount, Is.EqualTo(3));
            Assert.That(botSecondary.TailBoneCount, Is.EqualTo(3));
            Assert.That(playerSecondary.BeltBoneCount, Is.EqualTo(4));
            Assert.That(botSecondary.BeltBoneCount, Is.EqualTo(4));
            Assert.That(botHasNoPlayerOwnership, Is.True,
                "The rival visual must not clone player input, magic or presentation ownership.");
            Assert.That(botSharedPresentation, Is.Not.Null,
                "Player and rival must share the base animation presentation pipeline.");
            Assert.That(playerRagdoll, Is.Not.Null);
            Assert.That(botRagdoll, Is.Not.Null);
            Assert.That(oldStoneBodyRemoved, Is.True);
            Assert.That(usesCenteredV5Physics, Is.True,
                "Thrown, debris and push rocks must use centered unit copies of the approved V5 library.");
            Assert.That(botIsNotDirectMagicTarget, Is.True,
                "The enemy must be hit through physical counterplay, not grabbed as a rock.");
            Assert.That(sampled, Is.True);
            Assert.That(finiteBotPosition, Is.True);
            Assert.That(planetQueuesDrained, Is.True);
            Assert.That(renderQueuePeak, Is.LessThan(30.0),
                "Radius-36 startup meshing must stay inside the bounded rescue budget.");
            Assert.That(frameDeltaP95Milliseconds, Is.LessThan(33.3f),
                "The radius-36 Editor cold run must keep its total-frame P95 below one 30 Hz frame.");
            Assert.That(liveDirectionalLights, Is.EqualTo(1));
            Assert.That(isolatedFromLookdevDemo, Is.True);
            Assert.That(obsoleteTargetsRemoved, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerAndBotUseVisibleDynamicRagdollsAndResetAtomically()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthMvpDuelController duel = FindInScene<EarthMvpDuelController>(scene);
            EarthMvpBotController bot = FindInScene<EarthMvpBotController>(scene);
            HumanoidRagdollRig[] rigs = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                HumanoidRagdollRig[] found = root.GetComponentsInChildren<HumanoidRagdollRig>(true);
                if (found.Length == 0) continue;
                if (rigs == null) rigs = found;
                else
                {
                    var combined = new HumanoidRagdollRig[rigs.Length + found.Length];
                    Array.Copy(rigs, combined, rigs.Length);
                    Array.Copy(found, 0, combined, rigs.Length, found.Length);
                    rigs = combined;
                }
            }
            Assert.That(duel, Is.Not.Null);
            Assert.That(rigs, Is.Not.Null);
            Assert.That(rigs.Length, Is.EqualTo(2));

            duel.KnockoutPlayer(new Vector3(2.5f, 2f, 0.5f));
            duel.KnockoutBot();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            for (int index = 0; index < rigs.Length; index++)
            {
                Assert.That(rigs[index].IsRagdollActive, Is.True);
                Assert.That(rigs[index].DynamicBodyCount, Is.EqualTo(11));
                Assert.That(rigs[index].GetComponentInChildren<Animator>(true).enabled, Is.False);
            }

            yield return new WaitForSeconds(3.7f);
            yield return new WaitForFixedUpdate();
            for (int index = 0; index < rigs.Length; index++)
            {
                Assert.That(rigs[index].IsRagdollActive, Is.False);
                Assert.That(rigs[index].DynamicBodyCount, Is.EqualTo(0));
                Assert.That(rigs[index].GetComponentInChildren<Animator>(true).enabled, Is.True);
            }
            bool botStayedBlueAfterRespawn = bot != null && UsesBlueTint(bot.gameObject);

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
            Assert.That(botStayedBlueAfterRespawn, Is.True,
                "Ragdoll stone-fade/reset must restore the enemy's blue property block.");
        }

        [UnityTest]
        public IEnumerator SurfWaveAndBotProjectileUseTheSharedVisibleKnockoutPipeline()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthMvpDuelController duel = FindInScene<EarthMvpDuelController>(scene);
            EarthCharacterImpactTarget botImpact = FindImpactTarget(scene, EarthDuelFighterId.Bot);
            EarthCharacterImpactTarget playerImpact = FindImpactTarget(scene, EarthDuelFighterId.Player);
            HumanoidRagdollRig botRig = botImpact != null
                ? botImpact.GetComponentInChildren<HumanoidRagdollRig>(true)
                : null;
            HumanoidRagdollRig playerRig = playerImpact != null
                ? playerImpact.GetComponentInChildren<HumanoidRagdollRig>(true)
                : null;

            Assert.That(duel, Is.Not.Null);
            Assert.That(botImpact, Is.Not.Null);
            Assert.That(playerImpact, Is.Not.Null);
            Assert.That(botRig, Is.Not.Null);
            Assert.That(playerRig, Is.Not.Null);

            EarthCharacterImpactResponse surfResponse = botImpact.ApplyImpact(
                botImpact.transform.position,
                botImpact.transform.forward + botImpact.transform.up * 0.08f,
                botImpact.Body.mass * 8.1f,
                EarthCharacterImpactSourceKind.SurfNose,
                0x5F000101u,
                8.1f,
                1f,
                101u);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(surfResponse, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(duel.BotPhase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
            Assert.That(botRig.IsRagdollActive, Is.True);
            Assert.That(botRig.DynamicBodyCount, Is.EqualTo(11));

            yield return new WaitForSeconds(3.7f);
            yield return new WaitForFixedUpdate();
            Assert.That(duel.BotPhase, Is.EqualTo(EarthDuelFighterPhase.Active));
            Assert.That(botRig.IsRagdollActive, Is.False);
            yield return new WaitForSeconds(0.8f);

            EarthCharacterImpactResponse waveResponse = botImpact.ApplyImpact(
                botImpact.transform.position,
                botImpact.transform.up + botImpact.transform.right,
                1f,
                EarthCharacterImpactSourceKind.PillarWave,
                0xA7000102u,
                0f,
                0.9f,
                202u);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(waveResponse, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(duel.BotPhase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
            Assert.That(botRig.IsRagdollActive, Is.True);

            yield return new WaitForSeconds(3.7f);
            yield return new WaitForFixedUpdate();
            Assert.That(duel.BotPhase, Is.EqualTo(EarthDuelFighterPhase.Active));
            Assert.That(botRig.IsRagdollActive, Is.False);
            yield return new WaitForSeconds(0.8f);

            int localizedBefore = playerRig.LocalizedRagdollHitCount;
            EarthCharacterImpactResponse firstProjectileResponse = playerImpact.ApplyImpact(
                playerImpact.transform.position,
                playerImpact.transform.up + playerImpact.transform.right,
                1f,
                EarthCharacterImpactSourceKind.BotProjectile,
                0xB0700103u,
                0f,
                1f,
                303u);
            EarthCharacterImpactResponse secondProjectileResponse = playerImpact.ApplyImpact(
                playerImpact.transform.position + playerImpact.transform.right * 0.12f,
                playerImpact.transform.up + playerImpact.transform.right,
                1f,
                EarthCharacterImpactSourceKind.BotProjectile,
                0xB0700104u,
                0f,
                1f,
                304u);
            EarthCharacterImpactResponse thirdProjectileResponse = playerImpact.ApplyImpact(
                playerImpact.transform.position + playerImpact.transform.right * 0.18f,
                playerImpact.transform.up + playerImpact.transform.right,
                1f,
                EarthCharacterImpactSourceKind.BotProjectile,
                0xB0700105u,
                0f,
                1f,
                305u);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(firstProjectileResponse, Is.EqualTo(EarthCharacterImpactResponse.Stagger));
            Assert.That(secondProjectileResponse, Is.EqualTo(EarthCharacterImpactResponse.Stagger));
            Assert.That(thirdProjectileResponse, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(playerRig.LocalizedRagdollHitCount, Is.GreaterThanOrEqualTo(localizedBefore + 3));
            Assert.That(duel.PlayerPhase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
            Assert.That(playerRig.IsRagdollActive, Is.True);
            Assert.That(playerRig.DynamicBodyCount, Is.EqualTo(11));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator StompStoneHoversThenPunchesAlongTheCrosshairWithoutPoolGrowth()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthDualMouseAbilityController ability = FindInScene<EarthDualMouseAbilityController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            Camera camera = FindInScene<Camera>(scene);
            Assert.That(ability, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            EarthFragmentPool pool = executor.FragmentPool;
            int typedBefore = pool.GetComponentsInChildren<EarthTypedCombatProjectile>(true).Length;
            Assert.That(typedBefore, Is.GreaterThanOrEqualTo(1));
            Assert.That(ability.CastStompStone(), Is.True);
            EarthFragment launchedStone = null;
            EarthFragment[] fragments = pool.GetComponentsInChildren<EarthFragment>(true);
            for (int index = 0; index < fragments.Length; index++)
                if (fragments[index].gameObject.activeInHierarchy)
                {
                    launchedStone = fragments[index];
                    break;
                }
            Assert.That(launchedStone, Is.Not.Null);

            yield return new WaitForSeconds(0.31f);
            Assert.That(ability.IsStompStoneActive, Is.True,
                "The stone must visibly hover before punch contact.");
            Animator animator = ability.GetComponentInChildren<Animator>(true);
            Assert.That(animator.GetInteger("CastKind"), Is.EqualTo(3),
                "The hover phase must switch to the authored boxer punch slot.");
            Vector3 crosshairAim = camera.transform.forward.normalized;

            yield return new WaitForSeconds(0.27f);
            Assert.That(ability.IsStompStoneActive, Is.False);
            Assert.That(launchedStone.Body.linearVelocity.magnitude, Is.GreaterThan(20f));
            Assert.That(Vector3.Dot(launchedStone.Body.linearVelocity.normalized, crosshairAim),
                Is.GreaterThan(0.96f));
            int typedAfter = pool.GetComponentsInChildren<EarthTypedCombatProjectile>(true).Length;
            Assert.That(typedAfter, Is.EqualTo(typedBefore),
                "Punch cast must reuse its prewarmed typed projectile component.");

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator HighFallOntoLandingCushionDoesNotRagdollOrKillPlayer()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthCharacterImpactTarget player = FindImpactTarget(scene, EarthDuelFighterId.Player);
            EarthMvpDuelController duel = FindInScene<EarthMvpDuelController>(scene);
            EarthLandingCushion cushion = player != null ? player.GetComponent<EarthLandingCushion>() : null;
            PlanetMotor motor = player != null ? player.GetComponent<PlanetMotor>() : null;
            HumanoidRagdollRig rig = player != null
                ? player.GetComponentInChildren<HumanoidRagdollRig>(true)
                : null;
            Assert.That(player, Is.Not.Null);
            Assert.That(duel, Is.Not.Null);
            Assert.That(cushion, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);

            Rigidbody body = player.Body;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : body.position.normalized;
            body.position += up * 8f;
            body.linearVelocity = -up * 15f;
            motor.BeginExternalLaunch(8);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(cushion.BeginHold(), Is.True);

            for (int tick = 0; tick < 180; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (tick > 12 && motor.HasStableSupport && !cushion.IsHolding) break;
            }

            Assert.That(cushion.SuppressesHardLanding, Is.True);
            Assert.That(duel.PlayerPhase, Is.EqualTo(EarthDuelFighterPhase.Active));
            Assert.That(rig.IsRagdollActive, Is.False);

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        private static void RecordFrameDelta(
            float[] samples,
            ref int count,
            ref float maximumMilliseconds)
        {
            float milliseconds = Time.unscaledDeltaTime * 1000f;
            if (count < samples.Length) samples[count] = milliseconds;
            count++;
            maximumMilliseconds = Mathf.Max(maximumMilliseconds, milliseconds);
        }

        private static float ComputePercentile95(float[] samples, int count)
        {
            int boundedCount = Mathf.Clamp(count, 0, samples.Length);
            if (boundedCount == 0) return 0f;
            Array.Sort(samples, 0, boundedCount);
            int index = Mathf.Clamp(Mathf.CeilToInt(boundedCount * 0.95f) - 1, 0, boundedCount - 1);
            return samples[index];
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            return null;
        }

        private static EarthCharacterImpactTarget FindImpactTarget(
            Scene scene,
            EarthDuelFighterId fighter)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EarthCharacterImpactTarget[] targets =
                    root.GetComponentsInChildren<EarthCharacterImpactTarget>(true);
                for (int index = 0; index < targets.Length; index++)
                    if (targets[index].FighterId == fighter)
                        return targets[index];
            }
            return null;
        }

        private static int CountInScene<T>(Scene scene) where T : Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                count += root.GetComponentsInChildren<T>(true).Length;
            return count;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name == name) return transforms[index].gameObject;
                }
            }
            return null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name == name) return transforms[index];
            return null;
        }

        private static bool IsCenteredV5PhysicsMesh(Mesh mesh, string expectedPrefix)
        {
            if (mesh == null || !mesh.name.StartsWith(expectedPrefix, System.StringComparison.Ordinal))
                return false;
            Bounds bounds = mesh.bounds;
            float maximumAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            return bounds.center.sqrMagnitude <= 0.000001f && Mathf.Abs(maximumAxis - 1f) <= 0.0005f;
        }

        private static bool HasShader(GameObject root, string shaderName)
        {
            if (root == null) return false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = renderers[index] != null ? renderers[index].sharedMaterial : null;
                if (material != null && material.shader != null && material.shader.name == shaderName)
                    return true;
            }
            return false;
        }

        private static bool UsesBlueTint(GameObject root)
        {
            if (root == null) return false;
            int baseColorId = Shader.PropertyToID("_BaseColor");
            int legacyColorId = Shader.PropertyToID("_Color");
            var block = new MaterialPropertyBlock();
            bool sampled = false;
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                SkinnedMeshRenderer renderer = renderers[index];
                if (renderer == null) continue;
                Material material = renderer.sharedMaterial;
                if (material == null) continue;
                int propertyId = material.HasProperty(baseColorId)
                    ? baseColorId
                    : material.HasProperty(legacyColorId) ? legacyColorId : -1;
                if (propertyId < 0) continue;
                block.Clear();
                renderer.GetPropertyBlock(block);
                Color color = block.HasColor(propertyId)
                    ? block.GetColor(propertyId)
                    : material.GetColor(propertyId);
                sampled = true;
                if (color.b <= color.r + 0.15f || color.b <= color.g + 0.05f)
                    return false;
            }
            return sampled;
        }

        private static bool UsesMappedLinebreakerLook(GameObject root)
        {
            if (root == null) return false;
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = renderers[index] != null ? renderers[index].sharedMaterial : null;
                if (material == null || material.shader == null ||
                    material.shader.name != "Elemental/Graphics V5/Rumble Rock Lit" ||
                    !material.HasProperty("_BaseMap") ||
                    !material.HasProperty("_TextureStrength"))
                    continue;
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null && texture.name == "LinebreakerTexture" &&
                    material.GetFloat("_TextureStrength") >= 0.55f)
                    return true;
            }
            return false;
        }

        private static int CountLiveDirectionalLights(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Light[] lights = root.GetComponentsInChildren<Light>(true);
                for (int index = 0; index < lights.Length; index++)
                {
                    Light light = lights[index];
                    if (light != null && light.type == LightType.Directional && light.enabled &&
                        light.gameObject.activeInHierarchy && light.intensity > 0.001f)
                        count++;
                }
            }
            return count;
        }
    }
}
