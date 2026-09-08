using System;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class ClusterGroundDustSetup
    {
        public const string Path="Assets/Elemental/Content/Profiles/EarthClusterGroundDustProfile.asset";
        [MenuItem("Elemental/VFX/Install Cluster Ground Wisps")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before binding clustered ground wisps.");
            var scene=SceneManager.GetActiveScene();
            if(scene.name!="EarthCoreSlice")throw new InvalidOperationException("Open saved EarthCoreSlice first.");
            EarthSurfaceWindDust dust=null;FrontendFlowController frontend=null;int count=0;
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var value in root.GetComponentsInChildren<EarthSurfaceWindDust>(true)){dust=value;count++;}
                if(frontend==null)frontend=root.GetComponentInChildren<FrontendFlowController>(true);
            }
            if(count!=1||dust.Profile==null||frontend==null)throw new InvalidOperationException("Expected one existing bound Surface Wind Dust and real frontend; no duplicate emitter will be created.");
            var saved=new SerializedObject(dust);
            var planet=saved.FindProperty("planet").objectReferenceValue as Transform;
            var material=saved.FindProperty("dustMaterial").objectReferenceValue as Material;
            int mask=saved.FindProperty("surfaceMask").intValue;
            if(planet==null||material==null||dust.ArenaAnchor==null)throw new InvalidOperationException("Existing dust material/arena/planet binding is missing.");
            var profile=AssetDatabase.LoadAssetAtPath<EarthSurfaceWindDustProfile>(Path);
            if(profile==null)
            {
                profile=UnityEngine.Object.Instantiate(dust.Profile);profile.name="EarthClusterGroundDustProfile";
                profile.clusteredWisps=true;profile.enabledEffect=true;profile.maximumParticles=192;
                profile.groundRate=4;profile.stoneRate=24;profile.speed=.6f;
                profile.sizeMetres=new Vector2(.45f,1.1f);profile.lifetimeSeconds=new Vector2(2.8f,4.2f);
                profile.opacity=.18f;profile.hoverHeight=.12f;profile.stoneScanSeconds=.8f;
                profile.clusterRadius=3.5f;profile.saturatedNeighbours=4;profile.isolatedStoneWeight=.2f;
                profile.clusteredRateMultiplier=1.6f;profile.gapEmissionChance=.65f;
                profile.reducedMotionRate=.3f;profile.reducedMotionSpeed=.35f;
                AssetDatabase.CreateAsset(profile,Path);
            }
            Undo.RecordObject(dust,"Bind quiet clustered ground dust");
            dust.Configure(profile,dust.ArenaAnchor,planet,mask,material,frontend);
            EditorUtility.SetDirty(dust);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Cluster ground wisps bound to the existing single emitter. Original surface profile/material and impact bursts preserved.");
        }
    }
}
