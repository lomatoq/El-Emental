using System;
using Elemental.Presentation.DistantScenery;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class DistantBirdFlockInstaller
    {
        private const string RootName="EE_Distant_Birds";
        private const string MaterialPath="Assets/Elemental/Content/Environment/DistantStone/DistantBirdSilhouette.mat";
        [MenuItem("Elemental/Environment/Install Or Update Distant Birds")]
        public static void Install()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.name!="EarthCoreSlice")throw new InvalidOperationException("Open EarthCoreSlice in Edit mode before installing decorative birds.");
            DistantBackdrop backdrop=null;DistantBirdFlock birds=null;FrontendFlowController flow=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var candidate in root.GetComponentsInChildren<DistantBackdrop>(true))
                {if(backdrop!=null)throw new InvalidOperationException("Ambiguous backdrop frame.");backdrop=candidate;}
                foreach(var candidate in root.GetComponentsInChildren<DistantBirdFlock>(true))
                {if(birds!=null)throw new InvalidOperationException("Multiple bird owners.");birds=candidate;}
                foreach(var candidate in root.GetComponentsInChildren<FrontendFlowController>(true))
                {if(flow!=null)throw new InvalidOperationException("Ambiguous reduced-motion settings owner.");flow=candidate;}
            }
            if(backdrop==null||backdrop.profile==null||backdrop.planetCenter==null||flow==null)
                throw new InvalidOperationException("Existing backdrop planet frame and frontend settings owner are required.");
            if(birds!=null&&(birds.name!=RootName||birds.transform.parent!=null))
                throw new InvalidOperationException("Existing bird component is not on the owned scene root; review ownership before updating.");
            Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(shader==null)throw new InvalidOperationException("URP Unlit shader required for distant silhouettes.");
                material=new Material(shader){name="Distant Bird Silhouette"};
                material.SetColor("_BaseColor",new Color(.065f,.075f,.085f,1));material.SetFloat("_Cull",0);
                AssetDatabase.CreateAsset(material,MaterialPath);
            }
            // Update the existing owned material as well as first installs.
            Undo.RecordObject(material,"Improve distant bird silhouette contrast");
            material.SetColor("_BaseColor",new Color(.025f,.032f,.040f,1));material.SetFloat("_Cull",0);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            if(birds==null)
            {
                var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);
                Undo.RegisterCreatedObjectUndo(root,"Install distant decorative birds");birds=root.AddComponent<DistantBirdFlock>();
            }
            Undo.RecordObject(birds,"Bind distant birds to authored planet frame");
            birds.Configure(backdrop.planetCenter,backdrop.stagingUp,backdrop.profile.heroViewDirection,flow,material);
            EditorUtility.SetDirty(birds);EditorSceneManager.MarkSceneDirty(scene);
            // Scene save remains an explicit reviewed action. No camera/light changes.
        }
    }
}
