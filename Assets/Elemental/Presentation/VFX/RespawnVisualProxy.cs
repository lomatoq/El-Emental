using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    /// <summary>One cached standing pose. Meshes and transform-only ancestors; no live bones or gameplay components.</summary>
    internal sealed class RespawnVisualProxy : IDisposable
    {
        private static readonly int EmissionId = Shader.PropertyToID("_RespawnEmission");
        private sealed class Part
        {
            public Renderer Source;
            public Renderer Renderer;
            public Mesh BakedMesh;
            public MaterialPropertyBlock Properties;
            public MaterialPropertyBlock[] MaterialProperties;
            public bool HiddenByUs;
        }
        private sealed class SourceLease { public int Count; public bool WasHidden; }
        private static readonly Dictionary<Renderer,SourceLease> SourceLeases = new();
        private static void AcquireSource(Renderer source)
        {
            if(!SourceLeases.TryGetValue(source,out var lease))
            {lease=new SourceLease{WasHidden=source.forceRenderingOff};SourceLeases.Add(source,lease);}
            lease.Count++;source.forceRenderingOff=true;
        }
        private static void ReleaseSource(Renderer source)
        {
            if(ReferenceEquals(source,null)||!SourceLeases.TryGetValue(source,out var lease))return;
            if(--lease.Count>0)return;
            if(source!=null)source.forceRenderingOff=lease.WasHidden;
            SourceLeases.Remove(source);
        }
        private readonly List<Part> _parts = new();
        private readonly Dictionary<Transform, Transform> _nodes = new();
        private Transform _actor, _pose;
        private Vector3 _cachedFeetOffset;
        public GameObject Root { get; private set; }
        public bool Ready => _parts.Count > 0;
        public bool Visible => Root != null && Root.activeSelf;
        // Shared admission for stage validation and both proxy modes. Unsupported opaque
        // body shaders remain eligible so their explicit contract check still fails visibly.
        public static bool IsBodyRenderer(Renderer source)
        {
            if(source==null||source is not (SkinnedMeshRenderer or MeshRenderer)||!source.enabled||!source.gameObject.activeInHierarchy)return false;
            foreach(var material in source.sharedMaterials)
            {
                if(material==null)continue;
                if(material.renderQueue>(int)RenderQueue.GeometryLast||material.GetTag("RenderType",false,"")=="Transparent"||
                    (material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f))return false;
            }
            return true;
        }
        public bool Capture(Transform actor, Transform parent, Vector3 feet, bool skinnedStage = false)
        {
            if (Ready) return true;
            _actor = actor;
            Root = new GameObject("Standing respawn proxy"); Root.transform.SetParent(parent, false); Root.SetActive(false);
            _pose = new GameObject("Cached standing pose").transform; _pose.SetParent(Root.transform, false);
            _pose.localScale = actor.lossyScale;
            _pose.localPosition = -Vector3.Scale(actor.InverseTransformPoint(feet), actor.lossyScale);
            _cachedFeetOffset = -_pose.localPosition;
            _nodes.Add(actor, _pose);
            foreach (var source in actor.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsBodyRenderer(source)) continue;
                var materials = source.sharedMaterials;
                foreach (var material in materials)
                    if (material == null || !skinnedStage && !material.HasProperty(EmissionId))
                        throw new InvalidOperationException($"{source.name} needs the opaque _RespawnEmission shader contract before gold respawn can be installed.");
                Mesh mesh; Mesh owned = null;
                if (source is SkinnedMeshRenderer skin && !skinnedStage)
                {
                    if (skin.sharedMesh == null) continue;
                    owned = new Mesh { name = "Cached " + source.name };
                    // MeshRenderer will inherit the copied transform hierarchy. Bake in the
                    // renderer's scale-compensated local space so scale is applied once.
                    try { skin.BakeMesh(owned,true); }
                    catch { UnityEngine.Object.Destroy(owned); throw; }
                    mesh = owned;
                }
                else if(source is SkinnedMeshRenderer sharedSkin){mesh=sharedSkin.sharedMesh;if(mesh==null)continue;}
                else
                {
                    var filter = source.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) continue;
                    mesh = filter.sharedMesh;
                }
                Transform node = CopyNode(source.transform);
                var visible = new GameObject("Render only " + source.name); visible.transform.SetParent(node, false);
                Renderer renderer;
                if(skinnedStage&&source is SkinnedMeshRenderer originalSkin)
                {
                    var copy=visible.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=mesh;
                    var sourceBones=originalSkin.bones;var copiedBones=new Transform[sourceBones.Length];
                    for(int bone=0;bone<sourceBones.Length;bone++)copiedBones[bone]=sourceBones[bone]!=null?CopyNode(sourceBones[bone]):null;
                    copy.bones=copiedBones;copy.rootBone=originalSkin.rootBone!=null?CopyNode(originalSkin.rootBone):null;
                    copy.localBounds=new Bounds(originalSkin.localBounds.center,originalSkin.localBounds.size*1.6f);
                    for(int shape=0;shape<mesh.blendShapeCount;shape++)copy.SetBlendShapeWeight(shape,originalSkin.GetBlendShapeWeight(shape));
                    copy.updateWhenOffscreen=true;renderer=copy;
                }
                else{visible.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=visible.AddComponent<MeshRenderer>();}
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                renderer.renderingLayerMask = source.renderingLayerMask;
                var part = new Part { Source = source, Renderer = renderer, BakedMesh = owned,
                    Properties = new MaterialPropertyBlock(), MaterialProperties = new MaterialPropertyBlock[materials.Length] };
                source.GetPropertyBlock(part.Properties);
                for (int index = 0; index < materials.Length; index++)
                {
                    var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block, index);
                    if (!block.isEmpty) part.MaterialProperties[index] = block;
                }
                _parts.Add(part);
            }
            if(!skinnedStage)_nodes.Clear();
            if (!Ready) throw new InvalidOperationException("No renderable standing pose exists beneath the configured actor.");
            return true;
        }
        private Transform CopyNode(Transform source)
        {
            if (_nodes.TryGetValue(source, out Transform copied)) return copied;
            if (source == null || !source.IsChildOf(_actor)) throw new InvalidOperationException("Standing pose must be cached before the ragdoll leaves its actor hierarchy.");
            Transform parent = CopyNode(source.parent);
            copied = new GameObject(source.name).transform; copied.SetParent(parent, false);
            copied.localPosition = source.localPosition; copied.localRotation = source.localRotation; copied.localScale = source.localScale;
            _nodes.Add(source, copied); return copied;
        }
        // The reservation's root includes capsule clearance; the ground hit does not.
        // Reproduce the captured actor-relative geometry at that exact root pose.
        public void RenderAtActorPose(Vector3 position, Quaternion rotation, float scale, float lift, Vector3 up, Color emission)
        {
            Render(position + rotation * _cachedFeetOffset, rotation, scale, lift, up, emission);
        }
        public void Render(Vector3 feet, Quaternion rotation, float scale, float lift, Vector3 up, Color emission)
        {
            if (!Ready) return;
            Root.transform.SetPositionAndRotation(feet + up * lift, rotation); Root.transform.localScale = Vector3.one * scale;
            Root.SetActive(true);
            foreach (var part in _parts)
            {
                if (!part.HiddenByUs && part.Source != null)
                { AcquireSource(part.Source); part.HiddenByUs = true; }
                // This proxy is the sole MPB writer. Preserve every captured team/texture/fade property.
                part.Properties.SetColor(EmissionId, emission); part.Renderer.SetPropertyBlock(part.Properties);
                for (int index = 0; index < part.MaterialProperties.Length; index++)
                    if (part.MaterialProperties[index] != null)
                    { part.MaterialProperties[index].SetColor(EmissionId, emission); part.Renderer.SetPropertyBlock(part.MaterialProperties[index], index); }
            }
        }
        public bool TryGetBone(Transform source,out Transform copy)=>_nodes.TryGetValue(source,out copy);
        public bool AimBoneToward(Transform source,Transform child,Vector3 direction)
        {
            if(!_nodes.TryGetValue(source,out Transform bone)||!_nodes.TryGetValue(child,out Transform endpoint))return false;
            Vector3 current=endpoint.position-bone.position;
            if(current.sqrMagnitude<.000001f||direction.sqrMagnitude<.000001f)return false;
            bone.rotation=Quaternion.FromToRotation(current,direction)*bone.rotation;return true;
        }
        public bool SetLocalBoneRotation(Transform source,Quaternion rotation)
        {
            if(source==null||!_nodes.TryGetValue(source,out Transform copy))return false;
            copy.localRotation=rotation;return true;
        }
        public void RenderStage(Vector3 feet,Quaternion rotation,float lift,Vector3 up,uint renderingLayers)
        {
            if(!Ready)return;
            Root.transform.SetPositionAndRotation(feet+up*lift,rotation);Root.transform.localScale=Vector3.one;Root.SetActive(true);
            foreach(var part in _parts)
            {
                if(!part.HiddenByUs&&part.Source!=null)
                {AcquireSource(part.Source);part.HiddenByUs=true;}
                part.Renderer.renderingLayerMask=renderingLayers;
                part.Renderer.SetPropertyBlock(part.Properties);
                for(int index=0;index<part.MaterialProperties.Length;index++)if(part.MaterialProperties[index]!=null)
                    part.Renderer.SetPropertyBlock(part.MaterialProperties[index],index);
            }
        }
        public void HideAndRestoreSource()
        {
            if (Root != null) Root.SetActive(false);
            foreach (var part in _parts)
            {
                if (part.HiddenByUs) ReleaseSource(part.Source);
                part.HiddenByUs = false;
            }
        }
        public void Dispose()
        {
            HideAndRestoreSource();
            foreach (var part in _parts) if (part.BakedMesh != null) UnityEngine.Object.Destroy(part.BakedMesh);
            if (Root != null) UnityEngine.Object.Destroy(Root);
            _parts.Clear(); _nodes.Clear(); Root = null;
        }
    }
}
