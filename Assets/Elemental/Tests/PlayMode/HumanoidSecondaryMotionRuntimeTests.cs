using System;
using System.Collections;
using System.IO;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class HumanoidSecondaryMotionRuntimeTests
    {
        [UnityTest]
        public IEnumerator ShippingActorsSimulateSecondaryMotionAfterFinalPoseAndRecoverFromReset()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool loadedForTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedForTest)
            {
                yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(path);
            }
            yield return null;
            HumanoidSecondaryMotion motion = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Planet Character") motion = root.GetComponentInChildren<HumanoidSecondaryMotion>(true);
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.HasCollisionProxies && motion.HasValidSecondaryHierarchy && motion.HasHelmetHairLock, Is.True);
            Animator animator = motion.GetComponentInChildren<Animator>();
            if (animator == null) animator = motion.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            var driver = motion.gameObject.AddComponent<SecondaryAnchorStressDriver>();
            driver.Hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            driver.Head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform[] transforms = motion.GetComponentsInChildren<Transform>();
            Transform tail = Array.Find(transforms, t => t.name == "Secondary_Tail_03");
            Transform belt = Array.Find(transforms, t => t.name == "Secondary_Belt_L_02");
            Transform hair = Array.Find(transforms, t => t.name == "Secondary_HairLock");
            Quaternion hairBind = hair.localRotation;
            float tailTravel = 0f, beltTravel = 0f, maximumAngle = 0f;
            Quaternion lastTail = tail.localRotation, lastBelt = belt.localRotation;
            int corrections = 0;
            try
            {
                for (int frame = 0; frame < 120; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    tailTravel += Quaternion.Angle(lastTail, tail.localRotation);
                    beltTravel += Quaternion.Angle(lastBelt, belt.localRotation);
                    lastTail = tail.localRotation; lastBelt = belt.localRotation;
                    maximumAngle = Mathf.Max(maximumAngle, motion.MaximumAppliedAngle);
                    corrections += motion.CollisionCorrectionCount;
                    Assert.That(Quaternion.Angle(hairBind, hair.localRotation), Is.LessThan(.01f));
                    Assert.That(float.IsFinite(motion.MaximumAppliedAngle), Is.True);
                }
                Assert.That(tailTravel, Is.GreaterThan(2f));
                Assert.That(beltTravel, Is.GreaterThan(2f));
                Assert.That(maximumAngle, Is.LessThan(65f));
                motion.ResetAfterDiscontinuity();
                Directory.CreateDirectory("BuildReports/CharacterRigRepair");
                File.WriteAllText("BuildReports/CharacterRigRepair/runtime-proof.json",
                    JsonUtility.ToJson(new Evidence { utc = DateTime.UtcNow.ToString("O"), frames = 120,
                        tailTravel = tailTravel, beltTravel = beltTravel, maximumAngle = maximumAngle,
                        collisionCorrections = corrections, rigidHair = true }, true));
                Debug.Log($"[CharacterRig] production LateUpdate: tail travel {tailTravel:F2}, belt travel {beltTravel:F2}, max angle {maximumAngle:F2}, collision corrections {corrections}.");
            }
            finally
            {
                UnityEngine.Object.Destroy(driver);
                motion.ResetAfterDiscontinuity();
            }
            if (loadedForTest) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Serializable]
        private sealed class Evidence
        {
            public string utc;
            public int frames;
            public float tailTravel;
            public float beltTravel;
            public float maximumAngle;
            public int collisionCorrections;
            public bool rigidHair;
        }
    }

    // Controlled perturbation of actual shipping animated anchors, immediately
    // before the final secondary pass. Animator restores the base next frame.
    [DefaultExecutionOrder(2800)]
    public sealed class SecondaryAnchorStressDriver : MonoBehaviour
    {
        public Transform Hips;
        public Transform Head;
        private float _phase;
        private void LateUpdate()
        {
            _phase += Time.deltaTime * 2f * Mathf.PI * 1.6f;
            if (Hips != null) Hips.localRotation *= Quaternion.Euler(Mathf.Sin(_phase) * 7f, Mathf.Sin(_phase * .7f) * 12f, 0f);
            if (Head != null) Head.localRotation *= Quaternion.Euler(0f, Mathf.Sin(_phase) * 25f, 0f);
        }
    }
}
