using System;
using System.Collections.Generic;
using System.Text;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public readonly struct EarthMotionCatalogBuildSummary
    {
        public EarthMotionCatalogBuildSummary(
            int clipCount,
            int copiedCurveClipCount,
            int derivedCurveClipCount,
            string identityHash)
        {
            ClipCount = clipCount;
            CopiedCurveClipCount = copiedCurveClipCount;
            DerivedCurveClipCount = derivedCurveClipCount;
            IdentityHash = identityHash;
        }

        public int ClipCount { get; }
        public int CopiedCurveClipCount { get; }
        public int DerivedCurveClipCount { get; }
        public string IdentityHash { get; }
    }

    /// <summary>
    /// Deterministically unions controller references with the curated import set.
    /// It writes only the EarthMotionCatalog asset; FBX import settings are untouched.
    /// </summary>
    public static class EarthMotionCatalogBuilder
    {
        public const string DefaultCatalogPath =
            "Assets/Elemental/Content/Animation/EarthMotionCatalog.asset";
        public const string CatalogSemanticClipPath =
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx";
        public const string CatalogSemanticClipName = "Ranged_Magic_Spellcasting";
        private const int CatalogCurveKeyCount = 17;
        private static readonly string[] CatalogLibraryPaths =
        {
            EarthHumanoidMotionSetup.KayKitDirectionalDodgePath,
            EarthHumanoidMotionSetup.KayKitMovementBasicPath
        };

        [MenuItem("Elemental Suite/Character/Rebuild Earth Motion Catalog")]
        public static void BuildOrUpdateDefaultCatalog()
        {
            EarthMotionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<EarthMotionCatalog>(DefaultCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
            }

            EarthMotionCatalogBuildSummary summary = Rebuild(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log(
                $"[Elemental] Earth motion catalog rebuilt: {summary.ClipCount} clips, " +
                $"copied curves={summary.CopiedCurveClipCount}, " +
                $"derived catalog-local curves={summary.DerivedCurveClipCount}, " +
                $"identity={summary.IdentityHash}.",
                catalog);
        }

        public static EarthMotionCatalogBuildSummary Rebuild(EarthMotionCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            List<ClipCandidate> candidates = CollectCandidates(out string inventory);
            if (candidates.Count != EarthMotionCatalog.ExpectedCuratedClipCount)
            {
                throw new InvalidOperationException(
                    $"Earth motion catalog expected exactly " +
                    $"{EarthMotionCatalog.ExpectedCuratedClipCount} existing clips from the " +
                    $"controller/curated GUID+localFileId union, but observed {candidates.Count}. " +
                    "Do not invent or download replacement clips.\n" + inventory);
            }

            var previous = new Dictionary<ClipIdentity, EarthMotionClipProfile>(
                catalog.ClipCount);
            for (int index = 0; index < catalog.ClipCount; index++)
            {
                EarthMotionClipProfile profile = catalog.ClipAt(index);
                if (profile == null || string.IsNullOrWhiteSpace(profile.AssetGuid) ||
                    profile.LocalFileId == 0)
                    continue;
                var identity = new ClipIdentity(profile.AssetGuid, profile.LocalFileId);
                if (previous.ContainsKey(identity))
                    throw new InvalidOperationException(
                        $"Existing EarthMotionCatalog has duplicate provenance " +
                        $"{profile.AssetGuid}:{profile.LocalFileId}.");
                previous.Add(identity, profile);
            }

            var profiles = new EarthMotionClipProfile[candidates.Count];
            int copiedCurves = 0;
            int derivedCurves = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                ClipCandidate candidate = candidates[index];
                AnimationCurve[] curves = ReadOrDeriveCurves(
                    candidate.Clip,
                    out bool derived);
                if (derived) derivedCurves++;
                else copiedCurves++;
                EarthMotionClipProfile profile = CreateProfile(candidate, curves);
                if (previous.TryGetValue(candidate.Identity, out EarthMotionClipProfile old))
                    profile.ApplyManualCorrectionsFrom(old);
                profiles[index] = profile;
            }

            string identityHash = ComputeIdentityHash(candidates);
            catalog.ReplaceProfiles(profiles, identityHash);
            return new EarthMotionCatalogBuildSummary(
                profiles.Length,
                copiedCurves,
                derivedCurves,
                identityHash);
        }

        public static int CollectCuratedClipCount() => CollectCandidates(out _).Count;

        public static string DescribeCuratedInventory()
        {
            CollectCandidates(out string inventory);
            return inventory;
        }

        private static List<ClipCandidate> CollectCandidates(out string inventory)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null)
                throw new InvalidOperationException(
                    $"AnimatorController is missing: {EarthHumanoidMotionSetup.ControllerPath}");

            var byIdentity = new Dictionary<ClipIdentity, ClipCandidate>(64);
            var inventoryBuilder = new StringBuilder(2048);
            inventoryBuilder.AppendLine("Earth motion source inventory:");
            AnimationClip[] controllerClips = controller.animationClips;
            for (int index = 0; index < controllerClips.Length; index++)
                AddCandidate(controllerClips[index], byIdentity, "controller");
            AppendInventorySource(
                inventoryBuilder,
                "controller reachable clips",
                controllerClips,
                controllerClips.Length,
                byIdentity.Count);

            var visitedPaths = new HashSet<string>(StringComparer.Ordinal);
            var missingPaths = new List<string>();
            for (int pathIndex = 0;
                 pathIndex < EarthHumanoidMotionSetup.CuratedPaths.Length;
                 pathIndex++)
            {
                CollectPath(
                    EarthHumanoidMotionSetup.CuratedPaths[pathIndex],
                    byIdentity,
                    visitedPaths,
                    missingPaths,
                    inventoryBuilder);
            }
            for (int pathIndex = 0; pathIndex < CatalogLibraryPaths.Length; pathIndex++)
                CollectPath(
                    CatalogLibraryPaths[pathIndex],
                    byIdentity,
                    visitedPaths,
                    missingPaths,
                    inventoryBuilder);
            CollectExactClip(
                CatalogSemanticClipPath,
                CatalogSemanticClipName,
                byIdentity,
                inventoryBuilder);

            inventoryBuilder.Append("unique GUID+localFileId total=")
                .Append(byIdentity.Count);
            inventory = inventoryBuilder.ToString();
            if (missingPaths.Count > 0)
                throw new InvalidOperationException(
                    "Earth motion catalog source paths have no imported AnimationClip " +
                    "subassets: " + string.Join(", ", missingPaths) + "\n" + inventory);

            var candidates = new List<ClipCandidate>(byIdentity.Values);
            candidates.Sort(CompareCandidates);
            return candidates;
        }

        private static void CollectPath(
            string path,
            IDictionary<ClipIdentity, ClipCandidate> candidates,
            ISet<string> visitedPaths,
            ICollection<string> missingPaths,
            StringBuilder inventory)
        {
            if (!visitedPaths.Add(path)) return;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var clips = new List<AnimationClip>(assets.Length);
            int uniqueBefore = candidates.Count;
            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                if (assets[assetIndex] is not AnimationClip clip || IsPreviewClip(clip))
                    continue;
                clips.Add(clip);
                AddCandidate(clip, candidates, path);
            }

            AppendInventorySource(
                inventory,
                path,
                clips,
                clips.Count,
                candidates.Count - uniqueBefore);
            if (clips.Count == 0) missingPaths.Add(path);
        }

        private static void CollectExactClip(
            string path,
            string clipName,
            IDictionary<ClipIdentity, ClipCandidate> candidates,
            StringBuilder inventory)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var availableClips = new List<AnimationClip>(assets.Length);
            AnimationClip selected = null;
            int matchCount = 0;
            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                if (assets[assetIndex] is not AnimationClip clip || IsPreviewClip(clip))
                    continue;
                availableClips.Add(clip);
                if (!string.Equals(clip.name, clipName, StringComparison.Ordinal)) continue;
                selected = clip;
                matchCount++;
            }

            if (matchCount != 1)
            {
                AppendInventorySource(
                    inventory,
                    $"{path} exact-name '{clipName}'",
                    availableClips,
                    availableClips.Count,
                    0);
                throw new InvalidOperationException(
                    $"Earth motion catalog exact selector expected one existing " +
                    $"AnimationClip named '{clipName}' at '{path}', but observed " +
                    $"{matchCount}.\n{inventory}");
            }

            int uniqueBefore = candidates.Count;
            AddCandidate(selected, candidates, $"{path}#{clipName}");
            AppendInventorySource(
                inventory,
                $"{path} exact-name '{clipName}'",
                new[] { selected },
                1,
                candidates.Count - uniqueBefore);
        }

        private static void AppendInventorySource(
            StringBuilder inventory,
            string source,
            IReadOnlyList<AnimationClip> clips,
            int importedCount,
            int uniqueAdded)
        {
            inventory.Append("- ").Append(source)
                .Append(": imported=").Append(importedCount)
                .Append(", unique-added=").Append(uniqueAdded)
                .Append(", subassets=[");
            for (int index = 0; index < clips.Count; index++)
            {
                if (index > 0) inventory.Append(", ");
                inventory.Append(clips[index] != null ? clips[index].name : "<null>");
            }
            inventory.AppendLine("]");
        }

        private static void AddCandidate(
            AnimationClip clip,
            IDictionary<ClipIdentity, ClipCandidate> candidates,
            string source)
        {
            if (clip == null || IsPreviewClip(clip)) return;
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrWhiteSpace(path) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    clip,
                    out string guid,
                    out long localFileId) ||
                string.IsNullOrWhiteSpace(guid) || localFileId == 0)
            {
                throw new InvalidOperationException(
                    $"Motion clip '{clip.name}' from '{source}' has missing GUID/localFileId provenance.");
            }

            var identity = new ClipIdentity(guid, localFileId);
            if (candidates.TryGetValue(identity, out ClipCandidate existing))
            {
                if (existing.Clip != clip)
                    throw new InvalidOperationException(
                        $"Duplicate motion provenance {guid}:{localFileId} resolves to both " +
                        $"'{existing.Clip.name}' and '{clip.name}'.");
                return;
            }
            candidates.Add(identity, new ClipCandidate(clip, path, identity));
        }

        private static EarthMotionClipProfile CreateProfile(
            in ClipCandidate candidate,
            AnimationCurve[] curves)
        {
            string name = candidate.Clip.name;
            EarthMotionSemanticAction semantic = ClassifySemanticAction(name);
            EarthAuthoredActionId authoredAction = ClassifyAuthoredAction(name, semantic);
            Vector3 averageSpeed = candidate.Clip.averageSpeed;
            Vector2 direction = new Vector2(averageSpeed.x, averageSpeed.z);
            if (direction.sqrMagnitude > 0.000001f) direction.Normalize();
            else direction = DirectionFromName(name);
            float speed = new Vector2(averageSpeed.x, averageSpeed.z).magnitude;
            float yaw = candidate.Clip.averageAngularSpeed * Mathf.Rad2Deg;
            EarthMotionPhaseWindow safeExit = FindWindow(curves[5], 0.5f);
            EarthMotionPhaseWindow cancel = safeExit;
            EarthMotionPhaseWindow recovery = safeExit;
            EarthAuthoredActionDefinition definition =
                EarthAuthoredActionCatalog.Resolve(authoredAction);
            if (authoredAction != EarthAuthoredActionId.None)
            {
                recovery = new EarthMotionPhaseWindow(
                    definition.RecoveryEnd01 > definition.ContactEnd01,
                    definition.ContactEnd01,
                    definition.RecoveryEnd01);
                cancel = new EarthMotionPhaseWindow(
                    definition.RecoveryEnd01 < 1f,
                    definition.RecoveryEnd01,
                    1f);
            }

            ResolveProvenance(
                candidate.Path,
                out EarthMotionProvenance provenance,
                out string provenanceLabel);
            return new EarthMotionClipProfile(
                candidate.Clip,
                candidate.Identity.Guid,
                candidate.Identity.LocalFileId,
                candidate.Path,
                provenance,
                provenanceLabel,
                semantic,
                authoredAction,
                speed,
                direction,
                yaw,
                ClassifyStance(semantic),
                ClassifyStyle(name, semantic),
                curves,
                FindPeakPhase(curves[4]),
                in safeExit,
                in cancel,
                in recovery,
                ClassifyHandOccupancy(name),
                SupportsMirroring(name, semantic),
                ClassifyEnvironmentTags(semantic),
                ClassifyActionTags(semantic));
        }

        private static AnimationCurve[] ReadOrDeriveCurves(
            AnimationClip clip,
            out bool derived)
        {
            var curves = new AnimationCurve[EarthAnimationClipMetadata.CurveCount];
            var counts = new int[EarthAnimationClipMetadata.CurveCount];
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                EditorCurveBinding binding = bindings[bindingIndex];
                for (int curveIndex = 0; curveIndex < curves.Length; curveIndex++)
                {
                    if (!string.Equals(
                            binding.propertyName,
                            EarthAnimationClipMetadata.CurveName(curveIndex),
                            StringComparison.Ordinal))
                        continue;
                    counts[curveIndex]++;
                    AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
                    curves[curveIndex] = CloneFiniteCurve(source);
                }
            }

            bool complete = true;
            for (int index = 0; index < curves.Length; index++)
            {
                if (counts[index] > 1)
                    throw new InvalidOperationException(
                        $"Animation clip '{clip.name}' has duplicate " +
                        $"'{EarthAnimationClipMetadata.CurveName(index)}' metadata curves.");
                complete &= counts[index] == 1 && curves[index] != null &&
                            curves[index].length > 0;
            }
            if (complete)
            {
                derived = false;
                return curves;
            }

            EarthAnimationMetadataSample[] samples =
                EarthAnimationClipMetadataPipeline.AnalyzeClipForCatalog(clip);
            if (samples.Length < 2)
                throw new InvalidOperationException(
                    $"Animation clip '{clip.name}' is missing canonical metadata and " +
                    "catalog-local analysis failed.");
            derived = true;
            return CurvesFromSamples(samples);
        }

        private static AnimationCurve[] CurvesFromSamples(
            IReadOnlyList<EarthAnimationMetadataSample> samples)
        {
            var curves = new AnimationCurve[EarthAnimationClipMetadata.CurveCount];
            for (int curveIndex = 0; curveIndex < curves.Length; curveIndex++)
            {
                int keyCount = Mathf.Min(CatalogCurveKeyCount, samples.Count);
                var keys = new Keyframe[keyCount];
                for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
                {
                    int sampleIndex = keyCount > 1
                        ? Mathf.RoundToInt(
                            keyIndex * (samples.Count - 1f) / (keyCount - 1f))
                        : 0;
                    EarthAnimationMetadataSample sample = samples[sampleIndex];
                    float time = sample.Time01;
                    float value = sample.CurveValue(curveIndex);
                    if (!float.IsFinite(time) || !float.IsFinite(value))
                        throw new InvalidOperationException(
                            $"Catalog-local metadata analysis produced a non-finite key " +
                            $"for '{EarthAnimationClipMetadata.CurveName(curveIndex)}' " +
                            $"at sample {sampleIndex}.");
                    keys[keyIndex] = new Keyframe(
                        Mathf.Clamp01(time),
                        Mathf.Clamp01(value));
                }
                curves[curveIndex] = new AnimationCurve(keys);
            }
            return curves;
        }

        private static AnimationCurve CloneFiniteCurve(AnimationCurve source)
        {
            if (source == null) return null;
            Keyframe[] keys = source.keys;
            if (keys.Length == 0) return null;
            for (int index = 0; index < keys.Length; index++)
            {
                Keyframe key = keys[index];
                if (!float.IsFinite(key.time) || !float.IsFinite(key.value))
                    return null;
            }
            var clone = new AnimationCurve(keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            return clone;
        }

        private static EarthMotionPhaseWindow FindWindow(
            AnimationCurve curve,
            float threshold)
        {
            if (curve == null || curve.length == 0) return default;
            const int sampleCount = 129;
            int first = -1;
            int last = -1;
            for (int index = 0; index < sampleCount; index++)
            {
                float phase = index / (sampleCount - 1f);
                if (EvaluateNormalized(curve, phase) < threshold) continue;
                if (first < 0) first = index;
                last = index;
            }
            return first >= 0
                ? new EarthMotionPhaseWindow(
                    true,
                    first / (sampleCount - 1f),
                    last / (sampleCount - 1f))
                : default;
        }

        private static float FindPeakPhase(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            const int sampleCount = 129;
            float bestValue = float.NegativeInfinity;
            float bestPhase = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float phase = index / (sampleCount - 1f);
                float value = EvaluateNormalized(curve, phase);
                if (value <= bestValue) continue;
                bestValue = value;
                bestPhase = phase;
            }
            return bestPhase;
        }

        private static float EvaluateNormalized(AnimationCurve curve, float phase01)
        {
            if (curve == null || curve.length == 0) return 0f;
            Keyframe[] keys = curve.keys;
            float duration = Mathf.Max(0.0001f, keys[keys.Length - 1].time);
            return curve.Evaluate(Mathf.Clamp01(phase01) * duration);
        }

        private static EarthMotionSemanticAction ClassifySemanticAction(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("wheelbarrow") || lower.Contains("recovery") ||
                lower.Contains("get up")) return EarthMotionSemanticAction.Recovery;
            if (lower.Contains("falling to roll") || lower.Contains("jump_land") ||
                lower.Contains("hard landing") || lower.Contains("land"))
                return EarthMotionSemanticAction.Landing;
            if (lower.Contains("fall")) return EarthMotionSemanticAction.Fall;
            if (lower.Contains("jump")) return EarthMotionSemanticAction.Jump;
            if (lower.Contains("dodge")) return EarthMotionSemanticAction.Dodge;
            if (lower.Contains("walk") || lower.Contains("run") || lower.Contains("sneak"))
                return EarthMotionSemanticAction.Locomotion;
            if (lower.Contains("turn")) return EarthMotionSemanticAction.Turn;
            if (lower.Contains("magic") || lower.Contains("cast spell"))
                return EarthMotionSemanticAction.Cast;
            if (lower.Contains("hit") || lower.Contains("receiving"))
                return EarthMotionSemanticAction.Impact;
            if (lower.Contains("punch") || lower.Contains("kick") ||
                lower.Contains("jab") || lower.Contains("attack"))
                return EarthMotionSemanticAction.Attack;
            if (lower.Contains("crouch") || lower.Contains("crawl"))
                return EarthMotionSemanticAction.Crouch;
            if (lower.Contains("idle") || lower.Contains("t-pose"))
                return EarthMotionSemanticAction.Idle;
            return EarthMotionSemanticAction.Utility;
        }

        private static EarthAuthoredActionId ClassifyAuthoredAction(
            string name,
            EarthMotionSemanticAction semantic)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("falling to roll"))
                return EarthAuthoredActionId.MovingLandingRoll;
            if (lower.Contains("hard landing"))
                return EarthAuthoredActionId.HardLandingBrace;
            if (semantic == EarthMotionSemanticAction.Landing)
                return EarthAuthoredActionId.SoftLanding;
            if (semantic == EarthMotionSemanticAction.Dodge)
                return EarthAuthoredActionId.DirectionalDodge;
            if (semantic == EarthMotionSemanticAction.Recovery)
                return EarthAuthoredActionId.RecoverableKnockdownRecovery;
            if (semantic == EarthMotionSemanticAction.Impact)
                return EarthAuthoredActionId.HitRecoil;
            if (semantic == EarthMotionSemanticAction.Cast)
                return EarthAuthoredActionId.MagicCast;
            if (semantic == EarthMotionSemanticAction.Jump)
                return EarthAuthoredActionId.Jump;
            if (semantic == EarthMotionSemanticAction.Fall)
                return EarthAuthoredActionId.Fall;
            if (semantic == EarthMotionSemanticAction.Locomotion)
                return EarthAuthoredActionId.Locomotion;
            return EarthAuthoredActionId.None;
        }

        private static EarthMotionStance ClassifyStance(EarthMotionSemanticAction action) =>
            action switch
            {
                EarthMotionSemanticAction.Crouch => EarthMotionStance.Crouched,
                EarthMotionSemanticAction.Jump => EarthMotionStance.Airborne,
                EarthMotionSemanticAction.Fall => EarthMotionStance.Airborne,
                EarthMotionSemanticAction.Recovery => EarthMotionStance.Knockdown,
                EarthMotionSemanticAction.Surf => EarthMotionStance.Surf,
                _ => EarthMotionStance.Standing
            };

        private static EarthMotionStyle ClassifyStyle(
            string name,
            EarthMotionSemanticAction action)
        {
            string lower = name.ToLowerInvariant();
            EarthMotionStyle style = EarthMotionStyle.Neutral;
            if (lower.Contains("injured")) style |= EarthMotionStyle.Injured;
            if (lower.Contains("magic") || lower.Contains("spell"))
                style |= EarthMotionStyle.Magic;
            if (action == EarthMotionSemanticAction.Attack ||
                action == EarthMotionSemanticAction.Impact)
                style |= EarthMotionStyle.Melee;
            if (lower.Contains("bow") || lower.Contains("rifle"))
                style |= EarthMotionStyle.Ranged;
            if (action == EarthMotionSemanticAction.Dodge)
                style |= EarthMotionStyle.Athletic | EarthMotionStyle.Defensive;
            if (action == EarthMotionSemanticAction.Recovery ||
                action == EarthMotionSemanticAction.Landing)
                style |= EarthMotionStyle.Recovery;
            return style;
        }

        private static EarthMotionHandOccupancy ClassifyHandOccupancy(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("2h") || lower.Contains("bow") ||
                lower.Contains("rifle")) return EarthMotionHandOccupancy.Both;
            if (lower.Contains("1h")) return EarthMotionHandOccupancy.Right;
            return EarthMotionHandOccupancy.None;
        }

        private static bool SupportsMirroring(
            string name,
            EarthMotionSemanticAction action)
        {
            string lower = name.ToLowerInvariant();
            return action == EarthMotionSemanticAction.Locomotion ||
                   action == EarthMotionSemanticAction.Dodge ||
                   lower.Contains("left") || lower.Contains("right");
        }

        private static EarthMotionEnvironmentTag ClassifyEnvironmentTags(
            EarthMotionSemanticAction action) =>
            action switch
            {
                EarthMotionSemanticAction.Jump => EarthMotionEnvironmentTag.Airborne,
                EarthMotionSemanticAction.Fall => EarthMotionEnvironmentTag.Airborne,
                EarthMotionSemanticAction.Landing =>
                    EarthMotionEnvironmentTag.Grounded | EarthMotionEnvironmentTag.Landing,
                EarthMotionSemanticAction.Recovery =>
                    EarthMotionEnvironmentTag.Grounded | EarthMotionEnvironmentTag.Recovery,
                EarthMotionSemanticAction.Attack =>
                    EarthMotionEnvironmentTag.Grounded | EarthMotionEnvironmentTag.Combat,
                EarthMotionSemanticAction.Impact =>
                    EarthMotionEnvironmentTag.Grounded | EarthMotionEnvironmentTag.Combat,
                EarthMotionSemanticAction.Cast =>
                    EarthMotionEnvironmentTag.Grounded | EarthMotionEnvironmentTag.Combat,
                EarthMotionSemanticAction.Surf => EarthMotionEnvironmentTag.Surf,
                _ => EarthMotionEnvironmentTag.Grounded
            };

        private static EarthMotionActionTag ClassifyActionTags(
            EarthMotionSemanticAction action) =>
            action switch
            {
                EarthMotionSemanticAction.Idle => EarthMotionActionTag.Idle,
                EarthMotionSemanticAction.Locomotion => EarthMotionActionTag.Locomotion,
                EarthMotionSemanticAction.Turn => EarthMotionActionTag.Turn,
                EarthMotionSemanticAction.Jump => EarthMotionActionTag.Jump,
                EarthMotionSemanticAction.Fall => EarthMotionActionTag.Fall,
                EarthMotionSemanticAction.Landing => EarthMotionActionTag.Land,
                EarthMotionSemanticAction.Dodge => EarthMotionActionTag.Dodge,
                EarthMotionSemanticAction.Cast => EarthMotionActionTag.Cast,
                EarthMotionSemanticAction.Impact => EarthMotionActionTag.Hit,
                EarthMotionSemanticAction.Recovery => EarthMotionActionTag.Recover,
                EarthMotionSemanticAction.Attack => EarthMotionActionTag.Attack,
                EarthMotionSemanticAction.Crouch => EarthMotionActionTag.Crouch,
                EarthMotionSemanticAction.Surf => EarthMotionActionTag.Surf,
                _ => EarthMotionActionTag.None
            };

        private static Vector2 DirectionFromName(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("backward")) return Vector2.down;
            if (lower.Contains("strafe_left") || lower.Contains("dodge_left"))
                return Vector2.left;
            if (lower.Contains("strafe_right") || lower.Contains("dodge_right"))
                return Vector2.right;
            if (lower.Contains("walk") || lower.Contains("run") ||
                lower.Contains("forward")) return Vector2.up;
            return Vector2.zero;
        }

        private static void ResolveProvenance(
            string path,
            out EarthMotionProvenance provenance,
            out string label)
        {
            if (path.IndexOf("/KayKit/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                provenance = EarthMotionProvenance.KayKitCc0;
                label = "KayKit Character Pack — CC0 1.0";
                return;
            }
            if (path.IndexOf("/Mixamo/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                provenance = EarthMotionProvenance.Mixamo;
                label = "Mixamo — Adobe Mixamo Content License";
                return;
            }
            provenance = EarthMotionProvenance.Unknown;
            label = string.Empty;
        }

        private static string ComputeIdentityHash(IReadOnlyList<ClipCandidate> candidates)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int index = 0; index < candidates.Count; index++)
            {
                ClipCandidate candidate = candidates[index];
                string identity = candidate.Identity.Guid + ":" +
                                  candidate.Identity.LocalFileId + ":" +
                                  candidate.Clip.name;
                for (int character = 0; character < identity.Length; character++)
                {
                    hash ^= identity[character];
                    hash *= prime;
                }
            }
            return hash.ToString("X16");
        }

        private static int CompareCandidates(ClipCandidate left, ClipCandidate right)
        {
            int guid = string.CompareOrdinal(left.Identity.Guid, right.Identity.Guid);
            if (guid != 0) return guid;
            int local = left.Identity.LocalFileId.CompareTo(right.Identity.LocalFileId);
            return local != 0
                ? local
                : string.CompareOrdinal(left.Clip.name, right.Clip.name);
        }

        private static bool IsPreviewClip(AnimationClip clip) =>
            clip == null || clip.name.StartsWith("__preview__", StringComparison.Ordinal);

        private readonly struct ClipIdentity : IEquatable<ClipIdentity>
        {
            public ClipIdentity(string guid, long localFileId)
            {
                Guid = guid;
                LocalFileId = localFileId;
            }

            public string Guid { get; }
            public long LocalFileId { get; }

            public bool Equals(ClipIdentity other) =>
                LocalFileId == other.LocalFileId &&
                string.Equals(Guid, other.Guid, StringComparison.Ordinal);

            public override bool Equals(object value) =>
                value is ClipIdentity other && Equals(other);

            public override int GetHashCode() =>
                unchecked(((Guid != null ? Guid.GetHashCode() : 0) * 397) ^
                          LocalFileId.GetHashCode());
        }

        private readonly struct ClipCandidate
        {
            public ClipCandidate(
                AnimationClip clip,
                string path,
                in ClipIdentity identity)
            {
                Clip = clip;
                Path = path;
                Identity = identity;
            }

            public AnimationClip Clip { get; }
            public string Path { get; }
            public ClipIdentity Identity { get; }
        }
    }
}
