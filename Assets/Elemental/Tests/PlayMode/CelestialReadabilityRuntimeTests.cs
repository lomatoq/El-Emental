#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class CelestialReadabilityRuntimeTests
    {
        private Scene scene, previous;
        private GameObject owner;
        private VolumeProfile profile;
        [UnityTest]
        public IEnumerator DayKeepsAuthoredOverridesNightLiftsShadowsAndDisableRestoresOwnedProfile()
        {
            previous = SceneManager.GetActiveScene();
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(scene);
            T One<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
            var gate = One<EarthSceneReadinessGate>(); var sky = One<CelestialSystemBehaviour>();
            double deadline = Time.realtimeSinceStartupAsDouble + 145;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            owner = new GameObject("Readability ownership evidence"); SceneManager.MoveGameObjectToScene(owner, scene);
            var volume = owner.AddComponent<Volume>(); volume.enabled = false;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.profile = profile;
            var color = profile.Add<ColorAdjustments>(false); color.contrast.value = 7;
            var shadows = profile.Add<ShadowsMidtonesHighlights>(false);
            shadows.shadows.value = new Vector4(.9f, 1, .95f, .12f); shadows.active = true;
            string original = JsonUtility.ToJson(shadows);
            sky.SetTimeOfDayForQa(.25f); sky.EvaluatePresentationForQa(); sky.BindReadabilityVolume(volume);
            Assert.That(sky.Snapshot.Night01, Is.LessThan(.001f));
            Assert.That(color.contrast.value, Is.EqualTo(7)); Assert.That(color.contrast.overrideState, Is.False);
            Assert.That(JsonUtility.ToJson(shadows), Is.EqualTo(original), "Day must retain authored grading including override ownership.");
            sky.SetTimeOfDayForQa(.75f); sky.EvaluatePresentationForQa();
            Assert.That(sky.Snapshot.Night01, Is.GreaterThan(.999f));
            Assert.That(color.contrast.value, Is.EqualTo(0).Within(.001f));
            Assert.That(shadows.shadows.value.w, Is.EqualTo(.62f).Within(.001f));
            Assert.That(color.contrast.overrideState && shadows.shadows.overrideState, Is.True);
            sky.enabled = false;
            Assert.That(color.contrast.value, Is.EqualTo(7)); Assert.That(color.contrast.overrideState, Is.False);
            Assert.That(JsonUtility.ToJson(shadows), Is.EqualTo(original));
            sky.BindReadabilityVolume(null);
            profile.Remove<ShadowsMidtonesHighlights>(); Object.Destroy(shadows);
            sky.enabled = true; sky.BindReadabilityVolume(volume);
            Assert.That(profile.TryGet(out ShadowsMidtonesHighlights added), Is.True);
            Assert.That(added.active, Is.True);
            sky.enabled = false;
            Assert.That(profile.TryGet(out ShadowsMidtonesHighlights _), Is.False, "Only the component created by this lease is removed.");
            Assert.That(color.contrast.value, Is.EqualTo(7));
            sky.BindReadabilityVolume(null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (owner != null) Object.Destroy(owner);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (profile != null) Object.Destroy(profile);
        }
    }
}
#endif
