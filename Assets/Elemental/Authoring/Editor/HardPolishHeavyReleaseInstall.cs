#if UNITY_EDITOR
using System.Linq;
using Elemental.Presentation.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace Elemental.Authoring.Editor
{
    public static class HardPolishHeavyReleaseInstall
    {
        public static void Install()
        {
            var clip=AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.Magic2HAttack03Path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            if(!clip.humanMotion||Mathf.Abs(clip.length-4.3f)>.01f)throw new System.InvalidOperationException("Owned heavy source changed; remeasure its interval before installation.");
            var trees=AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.ControllerPath).OfType<BlendTree>().Where(t=>t.children.Any(c=>c.directBlendParameter=="EarthPose04"||c.directBlendParameter=="EarthPoseA04"||c.directBlendParameter=="EarthPoseB04")).ToArray();
            if(trees.Length<2)throw new System.InvalidOperationException("Both saved A/B magic buffers must exist.");
            var profile=AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>("Assets/Elemental/Content/Profiles/EarthMagicMotionProfile.asset");
            if(profile==null||profile.Find(4)==null)throw new System.InvalidOperationException("Saved heavy profile entry missing.");
            foreach(var tree in trees)
            {
                var children=tree.children;
                for(int i=0;i<children.Length;i++)if(children[i].directBlendParameter=="EarthPose04"||children[i].directBlendParameter=="EarthPoseA04"||children[i].directBlendParameter=="EarthPoseB04")children[i].motion=clip;
                tree.children=children;EditorUtility.SetDirty(tree);
            }
            var selected=EarthMagicMotionProfile.CreateDefaults().Single(e=>(int)e.slot==4);var actual=profile.Find(4);
            actual.timing=selected.timing;actual.releaseStartNormalized=selected.releaseStartNormalized;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            Debug.Log("G04 heavy installed: owned 2H Attack03, native release .8815..1.376s, existing A/B owner and gameplay timestamps unchanged.");
        }
    }
}
#endif
