using System.Collections;
using Elemental.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Elemental.Online
{
    /// <summary>One scene transition intent, discarded after the fresh world is ready.</summary>
    public sealed class OnlineFreshArenaEntry : MonoBehaviour
    {
        private bool _cancelled;
        public static OnlineFreshArenaEntry Begin(string scenePath, string joinCode, NetworkManager oldManager)
        {
            var owner = new GameObject("Fresh online arena entry").AddComponent<OnlineFreshArenaEntry>();
            DontDestroyOnLoad(owner.gameObject);
            owner.StartCoroutine(owner.Reload(scenePath, joinCode, oldManager));
            return owner;
        }
        public void CancelEntry() => _cancelled = true;
        private IEnumerator Reload(string scenePath, string joinCode, NetworkManager oldManager)
        {
            // No connection has started: the old scene owns its offline manager.
            // A destroyed arena is never silently used as the SDF replay baseline.
            if (oldManager != null) Destroy(oldManager.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthOnlineFrontend frontend = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<EarthOnlineFrontend>(true);
                if (candidate == null) continue;
                if (frontend != null) { Debug.LogError("Fresh arena contains multiple online frontends."); Destroy(gameObject); yield break; }
                frontend = candidate;
            }
            if (frontend == null) { Debug.LogError("Fresh arena has no online frontend."); Destroy(gameObject); yield break; }
            double deadline = Time.realtimeSinceStartupAsDouble + 140;
            while (!_cancelled && frontend != null && frontend.Flow.State != FrontendState.Main &&
                   Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!_cancelled && frontend != null)
            {
                if (frontend.Flow.State != FrontendState.Main)
                    frontend.Flow.SetNetworkStatus("The fresh online arena could not become ready.", true);
                else frontend.ResumeFreshEntry(joinCode);
            }
            Destroy(gameObject);
        }
    }
}
