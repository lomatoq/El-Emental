using Elemental.Authoring.Editor;
using Elemental.Presentation.Camera;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMobilityVisualBindingRepairTests
    {
        [Test]
        public void MissingBindingsReuseAuthoredViewsAndSecondRepairIsNoOp()
        {
            RunRepairTest(false);
        }

        [Test]
        public void RepairPreservesCustomMaterialProfileAndPillarReference()
        {
            RunRepairTest(true);
        }

        private static void RunRepairTest(bool customBindings)
        {
            var root = new GameObject("Mobility binding fixture");
            root.SetActive(false);
            Material customMaterial = null;
            EarthSurfProfile customProfile = null;
            try
            {
                GameObject player = Child(root.transform, "Player");
                player.AddComponent<Rigidbody>();
                player.AddComponent<PlanetMotor>();
                player.AddComponent<EarthPillarMobility>();
                EarthSurfController surf = player.AddComponent<EarthSurfController>();
                Transform planet = Child(root.transform, "Planet").transform;
                PlanetCameraRig rig = Child(root.transform, "Camera rig").AddComponent<PlanetCameraRig>();
                EarthPillarFeedback feedback = Child(root.transform, "Feedback").AddComponent<EarthPillarFeedback>();
                Transform pillar = Child(feedback.transform, "Rising Earth Pillar").transform;
                Transform chip = Child(feedback.transform, "Lift Ground Chip 01").transform;
                pillar.localPosition = new Vector3(2f, 3f, 4f);
                Transform selectedPillar = pillar;
                if (customBindings)
                {
                    customMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    customMaterial.color = new Color(0.21f, 0.34f, 0.47f);
                    customProfile = ScriptableObject.CreateInstance<EarthSurfProfile>();
                    var data = new SerializedObject(surf);
                    data.FindProperty("material").objectReferenceValue = customMaterial;
                    data.FindProperty("profile").objectReferenceValue = customProfile;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    selectedPillar = Child(feedback.transform, "Custom launch pillar").transform;
                    data = new SerializedObject(feedback);
                    data.FindProperty("pillar").objectReferenceValue = selectedPillar;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                int childCount = root.GetComponentsInChildren<Transform>(true).Length;
                Assert.That(M3EarthCoreSetup.RepairEarthMobilityVisualBindings(player, planet, rig, feedback), Is.EqualTo(2));
                Assert.That(M3EarthCoreSetup.RepairEarthMobilityVisualBindings(player, planet, rig, feedback), Is.Zero);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
                Assert.That(pillar.localPosition, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                var repairedSurf = new SerializedObject(surf);
                foreach (string property in new[] { "casterBody", "motor", "planetCenter", "profile", "material", "dustMaterial", "effectsProfile" })
                    Assert.That(repairedSurf.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
                var repairedFeedback = new SerializedObject(feedback);
                Assert.That(repairedFeedback.FindProperty("pillar").objectReferenceValue, Is.SameAs(selectedPillar));
                Assert.That(repairedFeedback.FindProperty("groundChips").arraySize, Is.EqualTo(1));
                Assert.That(repairedFeedback.FindProperty("groundChips").GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(chip));
                if (customBindings)
                {
                    Assert.That(repairedSurf.FindProperty("material").objectReferenceValue, Is.SameAs(customMaterial));
                    Assert.That(repairedSurf.FindProperty("profile").objectReferenceValue, Is.SameAs(customProfile));
                    Assert.That(customMaterial.color, Is.EqualTo(new Color(0.21f, 0.34f, 0.47f)));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (customMaterial != null) Object.DestroyImmediate(customMaterial);
                if (customProfile != null) Object.DestroyImmediate(customProfile);
            }
        }

        private static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
