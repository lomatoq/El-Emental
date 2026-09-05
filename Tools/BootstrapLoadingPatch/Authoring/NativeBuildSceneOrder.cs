using System;
using System.Collections.Generic;

namespace Elemental.Authoring.Build
{
    public static class NativeBuildSceneOrder
    {
        public static string[] Create(
            IReadOnlyList<string> enabledScenes,
            string bootstrapScene,
            string playableScene)
        {
            if (enabledScenes == null)
                throw new ArgumentNullException(nameof(enabledScenes));
            ValidateEnabled(enabledScenes, bootstrapScene, "bootstrap");
            ValidateEnabled(enabledScenes, playableScene, "playable");
            if (string.Equals(bootstrapScene, playableScene, StringComparison.Ordinal))
                throw new ArgumentException("Bootstrap and playable scenes must be distinct.", nameof(playableScene));

            var ordered = new List<string>(enabledScenes.Count) { bootstrapScene, playableScene };
            for (int index = 0; index < enabledScenes.Count; index++)
            {
                string scene = enabledScenes[index];
                if (scene.EndsWith("/EarthPolishLab.unity", StringComparison.OrdinalIgnoreCase) ||
                    scene.EndsWith("\\EarthPolishLab.unity", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(scene, bootstrapScene, StringComparison.Ordinal) &&
                    !string.Equals(scene, playableScene, StringComparison.Ordinal))
                    ordered.Add(scene);
            }
            return ordered.ToArray();
        }

        private static void ValidateEnabled(
            IReadOnlyList<string> enabledScenes,
            string requiredScene,
            string role)
        {
            if (string.IsNullOrWhiteSpace(requiredScene))
                throw new ArgumentException($"A {role} scene is required.", nameof(requiredScene));
            for (int index = 0; index < enabledScenes.Count; index++)
            {
                if (string.Equals(enabledScenes[index], requiredScene, StringComparison.Ordinal))
                    return;
            }
            throw new InvalidOperationException(
                $"The {role} scene '{requiredScene}' is not enabled in Build Settings.");
        }
    }
}
