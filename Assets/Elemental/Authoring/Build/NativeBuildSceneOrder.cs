using System;
using System.Collections.Generic;

namespace Elemental.Authoring.Build
{
    public static class NativeBuildSceneOrder
    {
        public static string[] Create(IReadOnlyList<string> enabledScenes, string playableStartupScene)
        {
            if (enabledScenes == null)
                throw new ArgumentNullException(nameof(enabledScenes));
            if (string.IsNullOrWhiteSpace(playableStartupScene))
                throw new ArgumentException("A playable startup scene is required.", nameof(playableStartupScene));

            bool containsStartup = false;
            for (int index = 0; index < enabledScenes.Count; index++)
            {
                if (string.Equals(enabledScenes[index], playableStartupScene, StringComparison.Ordinal))
                {
                    containsStartup = true;
                    break;
                }
            }

            if (!containsStartup)
                throw new InvalidOperationException(
                    $"The playable startup scene '{playableStartupScene}' is not enabled in Build Settings.");

            var ordered = new List<string>(enabledScenes.Count) { playableStartupScene };
            for (int index = 0; index < enabledScenes.Count; index++)
            {
                string scene = enabledScenes[index];
                if (scene.EndsWith("/EarthPolishLab.unity", StringComparison.OrdinalIgnoreCase) ||
                    scene.EndsWith("\\EarthPolishLab.unity", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(scene, playableStartupScene, StringComparison.Ordinal))
                    ordered.Add(scene);
            }

            return ordered.ToArray();
        }
    }
}
