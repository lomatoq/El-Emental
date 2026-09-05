using System.IO;
using UnityEditor;
using UnityEngine;

namespace Elemental.Experimental.SonicPrototype
{
    internal static class SonicRetargetProfileBaker
    {
        private const string ProfileFolder = "Assets/Experimental/SonicPrototype/Profiles";

        [MenuItem("Elemental/Experimental/SONIC/3 Bake Retarget Profile From Selected Humanoid")]
        private static void BakeSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before capturing a SONIC retarget profile.");
                return;
            }

            GameObject selected = Selection.activeGameObject;
            Animator animator = selected != null ? selected.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogError("Select a GameObject containing a valid Humanoid Animator.");
                return;
            }

            EnsureFolder(ProfileFolder);
            var profile = ScriptableObject.CreateInstance<SonicHumanoidRetargetProfile>();
            try
            {
                // Read the imported Humanoid Avatar skeleton. This avoids capturing whichever
                // controller pose happens to be visible in the Editor.
                profile.CaptureFromAvatarDefinition(animator);
                string safeAvatarName = SanitizeFileName(animator.avatar.name);
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{ProfileFolder}/SONIC_{safeAvatarName}_Retarget.asset");
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
                Debug.Log(
                    $"Captured SONIC G1 retarget profile at {path} from the imported Avatar T-pose. " +
                    "Calibrate per-bone basis/weights visually before treating it as a valid retarget.",
                    profile);
            }
            catch
            {
                Object.DestroyImmediate(profile);
                throw;
            }
        }

        [MenuItem("Elemental/Experimental/SONIC/3 Bake Retarget Profile From Selected Humanoid", true)]
        private static bool ValidateBakeSelected() =>
            !EditorApplication.isPlayingOrWillChangePlaymode && Selection.activeGameObject != null;

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Humanoid" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result;
        }
    }
}
