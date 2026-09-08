using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Fire;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using A = Elemental.Authoring.Editor.Fire.FireGraphApi;

namespace Elemental.Authoring.Editor.Fire
{
    public static class FireGraphBuilder
    {
        public const string Content = "Assets/Elemental/Content/VFX/Fire";
        public const string Shaders = "Assets/Elemental/Presentation/Fire/Shaders/";
        public const string ShaderGraphPath = Content + "/SG_FireFlame.shadergraph";
        public const string VfxPath = Content + "/VFX_FireGroup.vfx";
        private const string SG = "UnityEditor.ShaderGraph.";
        private const string VFX = "UnityEditor.VFX.";

        [MenuItem("Elemental/Fire/Build Graphs And Profile")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before authoring Fire assets.");
            Directory.CreateDirectory(Content);
            // Idempotent: generated assets are preserved on subsequent runs. Delete only
            // these two generated assets explicitly when choosing to rebuild their graph topology.
            if (!File.Exists(ShaderGraphPath)) BuildShaderGraph();
            if (!File.Exists(VfxPath)) BuildVfx();
            var profile = AssetDatabase.LoadAssetAtPath<FireVisualProfile>(Content + "/Fire_Default.asset");
            if (profile == null) { profile = ScriptableObject.CreateInstance<FireVisualProfile>(); AssetDatabase.CreateAsset(profile,Content + "/Fire_Default.asset"); }
            profile.Graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxPath);
            var cpuMaterial=AssetDatabase.LoadAssetAtPath<Material>(Content+"/Fire_CpuMesh.mat");
            if(cpuMaterial==null)
            {
                var cpuShader=Shader.Find("Elemental/Fire/CpuMeshFlame");
                if(cpuShader==null || ShaderUtil.ShaderHasError(cpuShader)) throw new InvalidOperationException("Fire CPU mesh shader missing or compilation failed.");
                cpuMaterial=new Material(cpuShader); AssetDatabase.CreateAsset(cpuMaterial,Content+"/Fire_CpuMesh.mat");
            }
            profile.CpuMaterial=cpuMaterial;
            if(AssetDatabase.LoadAssetAtPath<Material>(Content+"/Fire_LabSurface.mat")==null)
            {
                var labSurface=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                labSurface.color=new Color(0.16f,0.18f,0.21f);AssetDatabase.CreateAsset(labSurface,Content+"/Fire_LabSurface.mat");
            }
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            Validate();
        }
        private static void BuildShaderGraph()
        {
            var graph = A.New(SG + "GraphData"); A.Call(graph,"AddContexts"); A.Set(graph,"isSubGraph",false); A.Set(graph,"path","Elemental/Fire");
            var target = A.New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
            A.Call(target,"TrySetActiveSubTarget",A.Type("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"));
            A.Set(target,"surfaceType","Transparent"); A.Set(target,"alphaMode","Alpha");
            A.Set(target,"renderFace","Both"); A.Set(target,"zWriteControl","ForceDisabled"); A.Set(target,"m_SupportVFX",true);
            var surface = A.Type(SG + "BlockFields+SurfaceDescription");
            A.Call(graph,"InitializeOutputs", A.Array(SG + "Target",target),
                A.Array(SG + "BlockFieldDescriptor", A.Get(surface,"BaseColor"), A.Get(surface,"Alpha")));
            var function = A.New(SG + "CustomFunctionNode");
            A.Set(function,"sourceType","File"); A.Set(function,"functionName","EF_FireGraphFlame");
            A.Set(function,"functionSource",AssetDatabase.AssetPathToGUID(Shaders + "FireGraphFlame.hlsl"));
            A.Set(function,"precision","Single");
            var fragment = A.Enum(SG + "ShaderStageCapability","Fragment");
            var input = A.Enum("UnityEditor.Graphing.SlotType","Input");
            var output = A.Enum("UnityEditor.Graphing.SlotType","Output");
            A.Call(function,"AddSlot", A.New(SG + "UVMaterialSlot",0,"UV","UV",A.Enum(SG + "Internal.UVChannel","UV0"),fragment));
            string[] names = { "FireTime","FirePhase","FireAge","FireHeat" };
            for (int i=0;i<names.Length;i++) A.Call(function,"AddSlot", A.New(SG + "Vector1MaterialSlot",i+1,names[i],names[i],input, i == 3 ? 1f : 0f,fragment));
            A.Call(function,"AddSlot",A.New(SG + "Vector3MaterialSlot",5,"Color","Color",output,Vector3.zero,fragment));
            A.Call(function,"AddSlot",A.New(SG + "Vector1MaterialSlot",6,"Alpha","Alpha",output,0f,fragment));
            A.Call(graph,"AddNode",function);
            for(int i=0;i<names.Length;i++)
            {
                var property = A.New(SG + "Internal.Vector1ShaderProperty");
                A.Set(property,"displayName",names[i]); A.Set(property,"overrideReferenceName",names[i]);
                A.Set(property,"generatePropertyBlock",true); A.Set(property,"value",i == 3 ? 1f : 0f);
                A.Call(graph,"AddGraphInput",property);
                var node = A.New(SG + "PropertyNode"); A.Call(graph,"AddNode",node); A.Set(node,"property",property);
                A.Call(graph,"Connect",A.Call(node,"GetSlotReference",0),A.Call(function,"GetSlotReference",i+1));
            }
            foreach(var wrapper in A.Items(A.Get(A.Get(graph,"fragmentContext"),"blocks")))
            {
                var block = A.Get(wrapper,"value"); var name = (string)A.Get(A.Get(block,"descriptor"),"name");
                if (name == "BaseColor" || name == "Alpha")
                    A.Call(graph,"Connect",A.Call(function,"GetSlotReference",name == "BaseColor" ? 5 : 6), A.Call(block,"GetSlotReference",0));
            }
            A.Call(graph,"ValidateGraph");
            var json = (string)A.Call(A.Type(SG + "Serialization.MultiJson"),"Serialize",graph);
            File.WriteAllText(ShaderGraphPath,json);
            AssetDatabase.ImportAsset(ShaderGraphPath,ImportAssetOptions.ForceSynchronousImport);
        }
        private static void BuildVfx()
        {
            A.Call(A.Type("UnityEditor.VisualEffectAssetEditorUtility"),"CreateNewAsset",VfxPath);
            var resource = A.Call(A.Type("UnityEditor.VFX.VisualEffectResource"),"GetResourceAtPath",VfxPath);
            var extensions = A.Type(VFX + "VisualEffectResourceExtensions");
            var graph = A.Call(extensions,"GetOrCreateGraph",resource);
            foreach (var name in new[]{"firePhase","fireHeat","fireWidth","fireAspect","fireAge"})
                A.Call(graph,"TryAddCustomAttribute",name,A.Enum("UnityEngine.VFX.VFXValueType","Float"),"Persistent Fire cosmetic attribute",false,null);
            var spawn = Add(graph,"VFXBasicSpawner");
            var init = Add(graph,"VFXBasicInitialize");
            var update = Add(graph,"VFXBasicUpdate");
            var output = Add(graph,"VFXComposedParticleOutput");
            A.Set(spawn,"position",new Vector2(0,0)); A.Set(init,"position",new Vector2(0,200));
            A.Set(update,"position",new Vector2(0,650)); A.Set(output,"position",new Vector2(0,1100));
            A.Setting(output,"m_Topology",A.New(VFX + "ParticleTopologyPlanarPrimitive", A.Enum(VFX + "VFXPrimitiveType","Quad")));
            var shading = A.New(VFX + "ParticleShadingShaderGraph");
            var sg = AssetDatabase.LoadAllAssetsAtPath(ShaderGraphPath).FirstOrDefault(x=>x.GetType().Name == "ShaderGraphVfxAsset");
            if (sg == null) throw new InvalidOperationException("Shader Graph import did not produce ShaderGraphVfxAsset. Confirm Support VFX Graph and inspect shader compiler log.");
            A.Set(shading,"shaderGraph",sg);
            // Notify after assigning both traits, so output ports are generated together.
            A.Setting(output,"m_Shading",shading);
            A.Call(spawn,"LinkTo",init); A.Call(init,"LinkTo",update); A.Call(update,"LinkTo",output);
            var data = A.Call(init,"GetData"); A.Set(data,"space","World"); A.Setting(data,"capacity",512u);
            A.Setting(data,"boundsMode","Manual");
            A.Setting(update,"integration","None"); A.Setting(update,"angularIntegration","None");
            A.Setting(update,"ageParticles",true); A.Setting(update,"reapParticles",true);
            var properties = new Dictionary<string,object>();
            AddParameter(graph,properties,"FireNodes",typeof(GraphicsBuffer),null);
            AddParameter(graph,properties,"FireContacts",typeof(GraphicsBuffer),null);
            AddParameter(graph,properties,"FireNodeCount",typeof(uint),0u);
            AddParameter(graph,properties,"FireContactCount",typeof(uint),0u);
            AddParameter(graph,properties,"FireSubsteps",typeof(uint),2u);
            AddParameter(graph,properties,"FireOriginWS",typeof(Vector3),Vector3.zero);
            AddParameter(graph,properties,"FireFreeUpWS",typeof(Vector3),Vector3.up);
            AddParameter(graph,properties,"FireBoundsCenter",typeof(Vector3),Vector3.zero);
            AddParameter(graph,properties,"FireBoundsSize",typeof(Vector3),Vector3.one*50);
            string[] floats={"FireTime","FireMinLifetime","FireMaxLifetime","FireParticleRadius","FireFreeDrag","FireFreeLift","FireMaxSpeed","FireSpawnRate"};
            float[] defaults={0,0.45f,0.85f,0.035f,1.2f,2,24,320};
            for(int i=0;i<floats.Length;i++) AddParameter(graph,properties,floats[i],typeof(float),defaults[i]);
            var rate = Add(spawn,"VFXSpawnerConstantRate"); Link(properties["FireSpawnRate"],A.Slot(rate,"inputSlots","Rate"));
            var initialize = Hlsl(init,"FireParticleInitialize.hlsl"); var step = Hlsl(update,"FireParticleUpdate.hlsl");
            foreach(var block in new[]{initialize,step})
                foreach(var slot in A.Items(A.Get(block,"inputSlots")))
                {
                    var name = ((string)A.Get(A.Get(slot,"property"),"name")).TrimStart('_');
                    if (name == "FireDeltaTime") continue;
                    if (!properties.TryGetValue(name,out var property)) throw new InvalidOperationException("Unbound Fire HLSL input: " + name);
                    Link(property,slot);
                }
            var delta = Add(graph,"VFXDynamicBuiltInParameter"); A.Setting(delta,"m_BuiltInParameters","VfxDeltaTime");
            Link(delta,A.Slot(step,"inputSlots","_FireDeltaTime"));
            var shape = Add(output,"Block.CustomHLSL");
            A.Setting(shape,"m_HLSLCode","void EF_FireOutput(inout VFXAttributes attributes) { attributes.size = attributes.fireWidth; attributes.scaleY = attributes.fireAspect; }");
            var orient = Add(output,"Block.Orient"); A.Setting(orient,"mode","FaceCameraPlane");
            foreach(var pair in new[]{("FirePhase","firePhase"),("FireAge","fireAge"),("FireHeat","fireHeat")})
            {
                var attribute=Add(graph,"VFXAttributeParameter"); A.Setting(attribute,"attribute",pair.Item2);
                Link(attribute,A.Slot(output,"inputSlots",pair.Item1));
            }
            Link(properties["FireTime"],A.Slot(output,"inputSlots","FireTime"));
            var bounds = A.Slot(init,"inputSlots","bounds");
            Link(properties["FireBoundsCenter"],A.Slot(bounds,"children","center"));
            Link(properties["FireBoundsSize"],A.Slot(bounds,"children","size"));
            A.Call(graph,"RecompileIfNeeded",false,false);
            A.Call(extensions,"WriteAssetWithSubAssets",resource);
            AssetDatabase.ImportAsset(VfxPath,ImportAssetOptions.ForceSynchronousImport);
        }
        private static object Add(object parent,string name)
        {
            // Spawner blocks live directly in UnityEditor.VFX, other HLSL/Orient blocks in .Block.
            var model = A.New(VFX + name); A.Call(parent,"AddChild",model); return model;
        }
        private static object Hlsl(object context,string file)
        {
            var block = Add(context,"Block.CustomHLSL");
            A.Setting(block,"m_HLSLCode",File.ReadAllText(Shaders + file));
            return block;
        }
        private static void AddParameter(object graph,Dictionary<string,object> map,string name,Type type,object value)
        {
            var p = Add(graph,"VFXParameter"); A.Call(p,"Init",type);
            A.Setting(p,"m_ExposedName",name); A.Setting(p,"m_Exposed",true);
            if (value != null) A.Set(p,"value",value);
            map.Add(name,p);
        }
        private static void Link(object source,object input) => A.Call(input,"Link",A.Items(A.Get(source,"outputSlots"))[0]);
        [MenuItem("Elemental/Fire/Refresh Graph HLSL Sources")]
        public static void RefreshSources()
        {
            var resource=A.Call(A.Type("UnityEditor.VFX.VisualEffectResource"),"GetResourceAtPath",VfxPath);
            var ext=A.Type(VFX+"VisualEffectResourceExtensions"); var graph=A.Call(ext,"GetOrCreateGraph",resource);
            foreach(var context in A.Items(A.Get(graph,"children")))
            {
                string file=context.GetType().Name == "VFXBasicInitialize" ? "FireParticleInitialize.hlsl" : context.GetType().Name == "VFXBasicUpdate" ? "FireParticleUpdate.hlsl" : null;
                if(file==null) continue;
                foreach(var block in A.Items(A.Get(context,"children")))
                    if(block.GetType().Name=="CustomHLSL") A.Setting(block,"m_HLSLCode",File.ReadAllText(Shaders+file));
            }
            A.Call(graph,"RecompileIfNeeded",false,false); A.Call(ext,"WriteAssetWithSubAssets",resource);
            AssetDatabase.ImportAsset(VfxPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        }
        [MenuItem("Elemental/Fire/Validate Generated Assets")]
        public static void Validate()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderGraphPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Fire Shader Graph failed import/compilation. Inspect Console.");
            var vfx=AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxPath);
            if (vfx == null) throw new InvalidOperationException("Fire VFX asset missing.");
            var temp = new GameObject("Fire contract validation") { hideFlags=HideFlags.HideAndDontSave };
            try
            {
                var effect=temp.AddComponent<VisualEffect>(); effect.visualEffectAsset=vfx;
                if (!effect.HasGraphicsBuffer("FireNodes") || !effect.HasGraphicsBuffer("FireContacts") || !effect.HasUInt("FireNodeCount") || !effect.HasUInt("FireContactCount") || !effect.HasVector3("FireOriginWS") || !effect.HasVector3("FireFreeUpWS") || !effect.HasFloat("FireTime"))
                    throw new InvalidOperationException("Fire graph buffer/frame property contract mismatch.");
                foreach(var name in new[]{"FireSpawnRate","FireMinLifetime","FireMaxLifetime","FireFreeDrag","FireFreeLift","FireMaxSpeed","FireParticleRadius"})
                    if(!effect.HasFloat(name)) throw new InvalidOperationException("Fire graph missing float: " + name);
                if(!effect.HasUInt("FireSubsteps") || !effect.HasVector3("FireBoundsCenter") || !effect.HasVector3("FireBoundsSize"))
                    throw new InvalidOperationException("Fire graph missing substeps or dynamic bounds.");
            }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
            Debug.Log("Fire graph import and exposed-buffer contract validated. Visual, PlayMode and performance gates remain separate.");
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) Debug.Log("Fire native URP VFX requires Linear. Automatic profile selects the Gamma-compatible bounded CPU mesh backend; global settings are unchanged.");
        }
    }
}







