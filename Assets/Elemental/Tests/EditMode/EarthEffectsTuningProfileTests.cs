using System.Reflection;
using Elemental.Authoring.Editor;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthEffectsTuningProfileTests
    {
        private const string TemporaryFolder = "Assets/Elemental/Tests/Generated";
        private const string TemporaryProfilePath =
            TemporaryFolder + "/EarthEffectsPersistenceTest.asset";

        [Test]
        public void Evaluators_AreDeterministicBoundedAndFiniteSafe()
        {
            EarthEffectsTuningProfile profile = ScriptableObject.CreateInstance<EarthEffectsTuningProfile>();
            try
            {
                var impact = new EarthImpactEvent(
                    11u,
                    29u,
                    1450f,
                    42000f,
                    24f,
                    18f,
                    float3.zero,
                    new float3(0f, 1f, 0f),
                    EarthImpactMaterialKind.Structure);
                EarthImpactEffectsSample first = profile.EvaluateImpact(in impact);
                EarthImpactEffectsSample repeated = profile.EvaluateImpact(in impact);

                Assert.That(repeated.DustCount, Is.EqualTo(first.DustCount));
                Assert.That(repeated.RubbleCount, Is.EqualTo(first.RubbleCount));
                Assert.That(repeated.SparkCount, Is.EqualTo(first.SparkCount));
                Assert.That(first.DustCount, Is.InRange(0, profile.Impact.MaximumDustCount));
                Assert.That(first.RubbleCount, Is.InRange(0, profile.Impact.MaximumRubbleCount));
                Assert.That(first.SparkCount, Is.EqualTo(profile.Impact.SparkCount));

                Assert.That(
                    profile.EvaluateFractureCount(int.MaxValue, float.PositiveInfinity),
                    Is.EqualTo(profile.Fracture.MaximumCount));
                Assert.That(
                    profile.EvaluateFractureCount(-10, float.NaN),
                    Is.InRange(profile.Fracture.MinimumCount, profile.Fracture.MaximumCount));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ParticleApplier_AppliesProfileCapacityRangesAndMaterial()
        {
            EarthEffectsTuningProfile profile = ScriptableObject.CreateInstance<EarthEffectsTuningProfile>();
            GameObject host = new GameObject("Earth Effects Application Test");
            ParticleSystem system = host.AddComponent<ParticleSystem>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null, "A simple shader is required for the material binding gate.");
            Material material = new Material(shader);
            try
            {
                profile.InitializeAuthoringDefaults(
                    material, material, material, material, material, material, material);
                EarthParticleSystemTuningApplier.Apply(
                    system,
                    profile.Fracture.Dust,
                    profile.Materials.FractureDust);

                ParticleSystem.MainModule main = system.main;
                Assert.That(main.maxParticles, Is.EqualTo(profile.Fracture.Dust.MaxParticles));
                Assert.That(main.startLifetime.constantMin, Is.EqualTo(profile.Fracture.Dust.Lifetime.x).Within(0.0001f));
                Assert.That(main.startLifetime.constantMax, Is.EqualTo(profile.Fracture.Dust.Lifetime.y).Within(0.0001f));
                Assert.That(main.startSize.constantMin, Is.EqualTo(profile.Fracture.Dust.Size.x).Within(0.0001f));
                Assert.That(system.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.SameAs(material));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void M3ProfileLoader_PreservesExistingInspectorValuesAndGuid()
        {
            EnsureTemporaryFolder();
            AssetDatabase.DeleteAsset(TemporaryProfilePath);
            EarthEffectsTuningProfile created = InvokeCreateOrLoad(TemporaryProfilePath);
            string originalGuid = AssetDatabase.AssetPathToGUID(TemporaryProfilePath);
            var serialized = new SerializedObject(created);
            SerializedProperty maximumCount = serialized.FindProperty("fracture.maximumCount");
            Assert.That(maximumCount, Is.Not.Null);
            maximumCount.intValue = 777;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();

            EarthEffectsTuningProfile loaded = InvokeCreateOrLoad(TemporaryProfilePath);
            try
            {
                Assert.That(loaded, Is.SameAs(created));
                Assert.That(AssetDatabase.AssetPathToGUID(TemporaryProfilePath), Is.EqualTo(originalGuid));
                Assert.That(loaded.Fracture.MaximumCount, Is.EqualTo(777));
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemporaryProfilePath);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void RegeneratedEarthCoreScene_UsesOneCanonicalEffectsProfileAndItsMaterials()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            EarthEffectsTuningProfile profile = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(
                M3EarthCoreSetup.EarthEffectsProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.SchemaVersion, Is.EqualTo(EarthEffectsTuningProfile.CurrentSchemaVersion));
            Assert.That(profile.Materials.FractureDust, Is.Not.Null);
            Assert.That(profile.Materials.ImpactDust, Is.Not.Null);
            Assert.That(profile.Materials.ImpactSparks, Is.Not.Null);
            Assert.That(profile.Materials.ImpactRubble, Is.Not.Null);
            Assert.That(profile.Materials.SurfDust, Is.Not.Null);
            Assert.That(profile.Materials.StoneFadeDust, Is.Not.Null);
            Assert.That(profile.Materials.AmbientMotes, Is.Not.Null);
            Assert.That(profile.Materials.MeteorStreaks, Is.Not.Null);
            Assert.That(profile.Materials.PillarChips, Is.Not.Null);

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool closeWhenDone = !scene.IsValid() || !scene.isLoaded;
            if (closeWhenDone) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                AssertComponentsUseProfile<EarthArenaFractureDustPresenter>(scene, profile, "effectsProfile", 1);
                AssertComponentsUseProfile<EarthMagicFeedback>(scene, profile, "effectsProfile", 1);
                AssertComponentsUseProfile<EarthSurfController>(scene, profile, "effectsProfile", 1);
                AssertComponentsUseProfile<MeteorShowerBehaviour>(scene, profile, "effectsProfile", 1);
                AssertComponentsUseProfile<EarthPillarFeedback>(scene, profile, "effectsProfile", 1);
                AssertComponentsUseProfile<HumanoidRagdollRig>(scene, profile, "effectsProfile", 2);

                GameObject motes = FindInScene(scene, "Sunlit Air Motes");
                Assert.That(motes, Is.Not.Null);
                ParticleSystemRenderer renderer = motes.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sharedMaterial, Is.SameAs(profile.Materials.AmbientMotes));
            }
            finally
            {
                if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static EarthEffectsTuningProfile InvokeCreateOrLoad(string path)
        {
            MethodInfo method = typeof(M3EarthCoreSetup).GetMethod(
                "CreateOrLoadProfile",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            MethodInfo generic = method.MakeGenericMethod(typeof(EarthEffectsTuningProfile));
            return (EarthEffectsTuningProfile)generic.Invoke(
                null,
                new object[] { path, "Earth Effects Persistence Test" });
        }

        private static void EnsureTemporaryFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Elemental/Tests/Generated"))
                AssetDatabase.CreateFolder("Assets/Elemental/Tests", "Generated");
        }

        private static void AssertComponentsUseProfile<T>(
            Scene scene,
            EarthEffectsTuningProfile profile,
            string fieldName,
            int minimumCount)
            where T : Component
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must remain serialized.");
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            int found = 0;
            for (int index = 0; index < all.Length; index++)
            {
                T component = all[index];
                if (component == null || component.gameObject.scene != scene) continue;
                found++;
                Assert.That(field.GetValue(component), Is.SameAs(profile), component.name);
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(minimumCount), typeof(T).Name);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                    if (transforms[index].name == name) return transforms[index].gameObject;
            }
            return null;
        }
    }
}
