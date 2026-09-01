using System;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    [Flags]
    public enum EarthMotionCatalogValidationIssue : ushort
    {
        None = 0,
        MissingCatalog = 1 << 0,
        SchemaMismatch = 1 << 1,
        ClipCountMismatch = 1 << 2,
        MissingIdentity = 1 << 3,
        DuplicateIdentity = 1 << 4,
        ProvenanceMismatch = 1 << 5,
        MissingCurve = 1 << 6,
        InvalidCurve = 1 << 7,
        InvalidKinematics = 1 << 8,
        InvalidWindow = 1 << 9,
        MissingSemanticMetadata = 1 << 10,
        NonDeterministicOrder = 1 << 11,
        MissingStateBinding = 1 << 12,
        InvalidStateBinding = 1 << 13,
        DuplicateStateBinding = 1 << 14
    }

    public static class EarthMotionCatalogValidator
    {
        [MenuItem("Elemental Suite/Validation/Validate Earth Motion Catalog")]
        public static void ValidateDefaultCatalogMenu()
        {
            EarthMotionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<EarthMotionCatalog>(
                    EarthMotionCatalogBuilder.DefaultCatalogPath);
            var errors = new List<string>();
            EarthMotionCatalogValidationIssue issues = Validate(catalog, errors);
            if (issues != EarthMotionCatalogValidationIssue.None)
                throw new InvalidOperationException(
                    "Earth motion catalog validation failed:\n- " +
                    string.Join("\n- ", errors));
            Debug.Log(
                $"[Elemental] Earth motion catalog valid: {catalog.ClipCount} clips, " +
                $"8 canonical curves each, identity {catalog.SourceIdentityHash}.",
                catalog);
        }

        public static EarthMotionCatalogValidationIssue Validate(
            EarthMotionCatalog catalog,
            List<string> errors)
        {
            if (catalog == null)
            {
                errors?.Add("EarthMotionCatalog is missing.");
                return EarthMotionCatalogValidationIssue.MissingCatalog;
            }

            EarthMotionCatalogValidationIssue issues = EarthMotionCatalogValidationIssue.None;
            if (catalog.SchemaVersion != EarthMotionCatalog.CurrentSchemaVersion)
            {
                issues |= EarthMotionCatalogValidationIssue.SchemaMismatch;
                errors?.Add(
                    $"Catalog schema {catalog.SchemaVersion} does not match " +
                    $"{EarthMotionCatalog.CurrentSchemaVersion}.");
            }
            if (catalog.ExpectedClipCount != EarthMotionCatalog.ExpectedCuratedClipCount ||
                catalog.ClipCount != EarthMotionCatalog.ExpectedCuratedClipCount)
            {
                issues |= EarthMotionCatalogValidationIssue.ClipCountMismatch;
                errors?.Add(
                    $"Catalog must contain exactly " +
                    $"{EarthMotionCatalog.ExpectedCuratedClipCount} curated clips; " +
                    $"observed {catalog.ClipCount}.");
            }
            if (string.IsNullOrWhiteSpace(catalog.SourceIdentityHash))
            {
                issues |= EarthMotionCatalogValidationIssue.MissingIdentity;
                errors?.Add("Catalog source identity hash is empty.");
            }

            var identities = new HashSet<string>(StringComparer.Ordinal);
            string previousGuid = null;
            long previousLocalId = long.MinValue;
            for (int index = 0; index < catalog.ClipCount; index++)
            {
                EarthMotionClipProfile profile = catalog.ClipAt(index);
                string label = profile?.Clip != null
                    ? profile.Clip.name
                    : $"entry {index}";
                if (profile == null || profile.Clip == null ||
                    string.IsNullOrWhiteSpace(profile.AssetGuid) ||
                    profile.LocalFileId == 0 ||
                    string.IsNullOrWhiteSpace(profile.SourceAssetPath))
                {
                    issues |= EarthMotionCatalogValidationIssue.MissingIdentity;
                    errors?.Add($"{label} has missing clip/GUID/localFileId/path identity.");
                    continue;
                }

                string identity = profile.AssetGuid + ":" + profile.LocalFileId;
                if (!identities.Add(identity))
                {
                    issues |= EarthMotionCatalogValidationIssue.DuplicateIdentity;
                    errors?.Add($"Duplicate catalog provenance {identity}.");
                }
                if (previousGuid != null)
                {
                    int guidOrder = string.CompareOrdinal(previousGuid, profile.AssetGuid);
                    if (guidOrder > 0 ||
                        (guidOrder == 0 && previousLocalId > profile.LocalFileId))
                    {
                        issues |= EarthMotionCatalogValidationIssue.NonDeterministicOrder;
                        errors?.Add($"Catalog order is not GUID/localFileId deterministic at {label}.");
                    }
                }
                previousGuid = profile.AssetGuid;
                previousLocalId = profile.LocalFileId;

                bool identityMatches =
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        profile.Clip,
                        out string actualGuid,
                        out long actualLocalId) &&
                    string.Equals(
                        actualGuid,
                        profile.AssetGuid,
                        StringComparison.Ordinal) &&
                    actualLocalId == profile.LocalFileId &&
                    string.Equals(
                        AssetDatabase.GetAssetPath(profile.Clip),
                        profile.SourceAssetPath,
                        StringComparison.Ordinal);
                if (!identityMatches || profile.Provenance == EarthMotionProvenance.Unknown ||
                    string.IsNullOrWhiteSpace(profile.ProvenanceLabel))
                {
                    issues |= EarthMotionCatalogValidationIssue.ProvenanceMismatch;
                    errors?.Add($"{label} provenance does not match its imported asset.");
                }

                for (int curveIndex = 0;
                     curveIndex < EarthAnimationClipMetadata.CurveCount;
                     curveIndex++)
                {
                    AnimationCurve curve = profile.Curve(curveIndex);
                    if (curve == null || curve.length == 0)
                    {
                        issues |= EarthMotionCatalogValidationIssue.MissingCurve;
                        errors?.Add(
                            $"{label} is missing catalog curve " +
                            $"'{EarthAnimationClipMetadata.CurveName(curveIndex)}'.");
                        continue;
                    }
                    if (!ValidateCurve(curve))
                    {
                        issues |= EarthMotionCatalogValidationIssue.InvalidCurve;
                        errors?.Add(
                            $"{label} has invalid catalog curve " +
                            $"'{EarthAnimationClipMetadata.CurveName(curveIndex)}'.");
                    }
                }

                Vector2 direction = profile.PlanarDirection;
                if (!float.IsFinite(profile.AverageSpeedMetersPerSecond) ||
                    profile.AverageSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                    direction.sqrMagnitude > 1.001f ||
                    !float.IsFinite(profile.AverageYawDegreesPerSecond))
                {
                    issues |= EarthMotionCatalogValidationIssue.InvalidKinematics;
                    errors?.Add($"{label} has invalid speed/direction/yaw metadata.");
                }
                if (!ValidateWindow(profile.SafeExitWindow) ||
                    !ValidateWindow(profile.CancelWindow) ||
                    !ValidateWindow(profile.RecoveryWindow) ||
                    !float.IsFinite(profile.LandingContactPhase01) ||
                    profile.LandingContactPhase01 < 0f ||
                    profile.LandingContactPhase01 > 1f)
                {
                    issues |= EarthMotionCatalogValidationIssue.InvalidWindow;
                    errors?.Add($"{label} has an invalid authored phase window.");
                }
                if (profile.SemanticAction == EarthMotionSemanticAction.Unknown ||
                    profile.EnvironmentTags == EarthMotionEnvironmentTag.None ||
                    (profile.SemanticAction != EarthMotionSemanticAction.Utility &&
                     profile.ActionTags == EarthMotionActionTag.None))
                {
                    issues |= EarthMotionCatalogValidationIssue.MissingSemanticMetadata;
                    errors?.Add($"{label} has incomplete semantic/environment/action tags.");
                }
            }
            ValidateStateBindings(catalog, errors, ref issues);
            return issues;
        }

        private static void ValidateStateBindings(
            EarthMotionCatalog catalog,
            List<string> errors,
            ref EarthMotionCatalogValidationIssue issues)
        {
            if (catalog.StateBindingCount == 0)
            {
                issues |= EarthMotionCatalogValidationIssue.MissingStateBinding;
                errors?.Add("Catalog has no controller-state bindings.");
                return;
            }

            var hashes = new HashSet<int>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            bool locomotion = false;
            bool jump = false;
            bool cast = false;
            bool impact = false;
            bool recovery = false;
            int previousLayer = -1;
            string previousPath = null;
            for (int bindingIndex = 0;
                 bindingIndex < catalog.StateBindingCount;
                 bindingIndex++)
            {
                EarthMotionStateBinding binding = catalog.StateBindingAt(bindingIndex);
                if (binding == null || binding.LayerIndex < 0 || binding.StateHash == 0 ||
                    string.IsNullOrWhiteSpace(binding.StatePath) ||
                    binding.ClipProfileCount == 0)
                {
                    issues |= EarthMotionCatalogValidationIssue.InvalidStateBinding;
                    errors?.Add($"Controller-state binding {bindingIndex} is incomplete.");
                    continue;
                }
                if (!hashes.Add(binding.StateHash) || !paths.Add(binding.StatePath))
                {
                    issues |= EarthMotionCatalogValidationIssue.DuplicateStateBinding;
                    errors?.Add(
                        $"Controller-state binding '{binding.StatePath}' duplicates a hash/path.");
                }
                if (previousLayer > binding.LayerIndex ||
                    (previousLayer == binding.LayerIndex && previousPath != null &&
                     string.CompareOrdinal(previousPath, binding.StatePath) > 0))
                {
                    issues |= EarthMotionCatalogValidationIssue.NonDeterministicOrder;
                    errors?.Add(
                        $"Controller-state bindings are not layer/path deterministic at " +
                        $"'{binding.StatePath}'.");
                }
                previousLayer = binding.LayerIndex;
                previousPath = binding.StatePath;

                for (int profileIndex = 0;
                     profileIndex < binding.ClipProfileCount;
                     profileIndex++)
                {
                    int catalogIndex = binding.ClipProfileIndexAt(profileIndex);
                    if (catalogIndex >= 0 && catalogIndex < catalog.ClipCount &&
                        catalog.ClipAt(catalogIndex)?.Clip != null)
                        continue;
                    issues |= EarthMotionCatalogValidationIssue.InvalidStateBinding;
                    errors?.Add(
                        $"Controller-state binding '{binding.StatePath}' references invalid " +
                        $"catalog index {catalogIndex}.");
                }

                locomotion |= binding.SemanticRole == EarthMotionSemanticAction.Locomotion;
                if (binding.SemanticRole == EarthMotionSemanticAction.Jump)
                {
                    jump = true;
                    ValidateExactJumpBinding(catalog, binding, errors, ref issues);
                }
                cast |= binding.SemanticRole == EarthMotionSemanticAction.Cast;
                impact |= binding.SemanticRole == EarthMotionSemanticAction.Impact;
                recovery |= binding.SemanticRole == EarthMotionSemanticAction.Recovery;
            }

            RequireRole(locomotion, "locomotion", errors, ref issues);
            RequireRole(jump, "jump", errors, ref issues);
            RequireRole(cast, "cast", errors, ref issues);
            RequireRole(impact, "hit/impact", errors, ref issues);
            RequireRole(recovery, "recovery", errors, ref issues);
        }

        private static void ValidateExactJumpBinding(
            EarthMotionCatalog catalog,
            EarthMotionStateBinding binding,
            List<string> errors,
            ref EarthMotionCatalogValidationIssue issues)
        {
            if (binding.ClipProfileCount == 1)
            {
                int profileIndex = binding.ClipProfileIndexAt(0);
                if (profileIndex >= 0 && profileIndex < catalog.ClipCount)
                {
                    EarthMotionClipProfile profile = catalog.ClipAt(profileIndex);
                    if (profile?.Clip != null &&
                        string.Equals(
                            profile.SourceAssetPath,
                            EarthHumanoidMotionSetup.KayKitMovementBasicPath,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            profile.Clip.name,
                            EarthHumanoidMotionSetup.JumpStartClipName,
                            StringComparison.Ordinal))
                        return;
                }
            }

            issues |= EarthMotionCatalogValidationIssue.InvalidStateBinding;
            errors?.Add(
                $"Controller-state binding '{binding.StatePath}' must resolve only the exact " +
                $"licensed clip '{EarthHumanoidMotionSetup.JumpStartClipName}' from " +
                $"'{EarthHumanoidMotionSetup.KayKitMovementBasicPath}'.");
        }

        private static void RequireRole(
            bool present,
            string role,
            List<string> errors,
            ref EarthMotionCatalogValidationIssue issues)
        {
            if (present) return;
            issues |= EarthMotionCatalogValidationIssue.MissingStateBinding;
            errors?.Add($"Catalog has no verified {role} controller-state binding.");
        }

        private static bool ValidateCurve(AnimationCurve curve)
        {
            Keyframe[] keys = curve.keys;
            float previousTime = float.NegativeInfinity;
            for (int index = 0; index < keys.Length; index++)
            {
                Keyframe key = keys[index];
                if (!float.IsFinite(key.time) || !float.IsFinite(key.value) ||
                    key.time < previousTime || key.value < -0.001f || key.value > 1.001f)
                    return false;
                previousTime = key.time;
            }
            return true;
        }

        private static bool ValidateWindow(EarthMotionPhaseWindow window) =>
            !window.Enabled ||
            (float.IsFinite(window.Start01) && float.IsFinite(window.End01) &&
             window.Start01 >= 0f && window.Start01 <= 1f &&
             window.End01 >= 0f && window.End01 <= 1f);
    }
}
