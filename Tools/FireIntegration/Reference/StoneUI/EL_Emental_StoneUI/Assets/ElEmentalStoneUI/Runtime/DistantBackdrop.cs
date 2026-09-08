using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElEmental.StoneUI
{
    /// <summary>Optional additive background. No colliders, rigidbodies, network entities or gameplay changes.</summary>
    public sealed class DistantBackdrop : MonoBehaviour
    {
        [Serializable] private struct Drift
        {
            public Transform target;public Vector3 origin;public Quaternion rotation;
            public float phase,period,amplitude,angle;
        }
        public DistantBackdropProfile profile;
        public Transform planetCenter;
        [Tooltip("World-space up of the arena's staging location, not camera.up every frame.")]
        public Vector3 stagingUp=Vector3.up;
        public bool reducedMotion;
        public bool animateWhenPaused=true;
        [SerializeField] private Transform generatedRoot;
        [SerializeField] private List<Drift> drift=new List<Drift>();
        private Vector3 _up;
        public int GeneratedCount=>drift.Count;
        public float ScaleFactor=>profile==null?1:Mathf.Max(1,profile.planetRadius)/36f;
        private void OnEnable(){_up=stagingUp.sqrMagnitude>.01f?stagingUp.normalized:Vector3.up;}
        [ContextMenu("Rebuild decorative background only")]
        public void Rebuild()
        {
            if(profile==null||profile.material==null||profile.silhouettes==null||profile.silhouettes.Length==0)
            {Debug.LogError("DistantBackdrop needs a profile, material and mesh library.",this);return;}
            // Only destroy the exact root previously created by this component.
            if(generatedRoot!=null){if(Application.isPlaying)Destroy(generatedRoot.gameObject);else DestroyImmediate(generatedRoot.gameObject);}
            drift.Clear();generatedRoot=new GameObject("EE_Generated_Backdrop").transform;generatedRoot.SetParent(transform,false);
            _up=stagingUp.sqrMagnitude>.01f?stagingUp.normalized:Vector3.up;
            Vector3 forward=Vector3.ProjectOnPlane(profile.heroViewDirection,_up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.forward+Vector3.right,_up).normalized;
            Vector3 right=Vector3.Cross(_up,forward).normalized;
            Vector3 center=planetCenter!=null?planetCenter.position:transform.position;
            var random=new System.Random(profile.seed);
            BuildLayer(0,profile.midMassifs,profile.midDistance,profile.midHeight,center,forward,right,random);
            BuildLayer(1,profile.farMassifs,profile.farDistance,profile.farHeight,center,forward,right,random);
            BuildLayer(2,profile.floatingIslands,profile.islandDistance,profile.islandHeight,center,forward,right,random);
        }
        private static float Next(System.Random r,float a,float b)=>Mathf.Lerp(a,b,(float)r.NextDouble());
        private void BuildLayer(int layerIndex,int count,Vector2 distances,Vector2 heights,Vector3 center,Vector3 forward,Vector3 right,System.Random rng)
        {
            float scale=ScaleFactor;
            for(int i=0;i<count;i++)
            {
                float degrees=(i+.2f)*360f/Mathf.Max(1,count)+Next(rng,-10,10)+(layerIndex==1?18:0);
                float wrapped=Mathf.DeltaAngle(0,degrees);
                if(layerIndex!=2&&Mathf.Abs(wrapped)<profile.clearViewHalfAngle)degrees+=Mathf.Sign(wrapped==0?1:wrapped)*profile.clearViewHalfAngle;
                float angle=degrees*Mathf.Deg2Rad;
                Vector3 direction=forward*Mathf.Cos(angle)+right*Mathf.Sin(angle);
                float distance=Next(rng,distances.x,distances.y)*scale;
                float height=Next(rng,heights.x,heights.y)*scale;
                float width=height*Next(rng,layerIndex==2?1.4f:.38f,layerIndex==2?2.7f:.82f);
                float altitude=(layerIndex==2?Next(rng,40,160):layerIndex==1?Next(rng,-20,95):Next(rng,0,85))*scale;
                var pivot=new GameObject((layerIndex==2?"Island_":layerIndex==1?"FarMassif_":"MidMassif_")+i.ToString("00")).transform;
                pivot.SetParent(generatedRoot,false);pivot.position=center+direction*distance+_up*altitude;
                pivot.rotation=Quaternion.LookRotation(direction,_up)*Quaternion.Euler(0,Next(rng,0,360),Next(rng,-5,5));
                int index=rng.Next(profile.silhouettes.Length);
                var renderers=new List<Renderer>();
                AddRenderer(pivot,"LOD0",profile.silhouettes[index],new Vector3(width,height,width*Next(rng,.65f,1.1f)),renderers);
                Mesh low=profile.lodSilhouettes!=null&&profile.lodSilhouettes.Length>index?profile.lodSilhouettes[index]:null;
                if(low!=null)
                {
                    var lowRenderers=new List<Renderer>();AddRenderer(pivot,"LOD1",low,pivot.GetChild(0).localScale,lowRenderers);
                    var lod=pivot.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.075f,renderers.ToArray()),new LOD(.0025f,lowRenderers.ToArray())});lod.RecalculateBounds();
                }
                drift.Add(new Drift{target=pivot,origin=pivot.localPosition,rotation=pivot.localRotation,phase=Next(rng,0,1),period=Next(rng,85,165),amplitude=(layerIndex==2?Next(rng,.65f,2.2f):Next(rng,.12f,.4f))*scale,angle=layerIndex==2?Next(rng,.06f,.22f):.025f});
            }
        }
        private void AddRenderer(Transform parent,string name,Mesh mesh,Vector3 scale,List<Renderer> result)
        {
            if(mesh==null)return;
            var go=new GameObject(name);go.layer=profile.layer;go.transform.SetParent(parent,false);go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=profile.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            result.Add(renderer);
        }
        public void SetReducedMotion(bool value)
        {
            reducedMotion=value;
            if(value)foreach(var d in drift)if(d.target!=null){d.target.localPosition=d.origin;d.target.localRotation=d.rotation;}
        }
        private void LateUpdate()
        {
            if(reducedMotion||generatedRoot==null)return;
            double time=animateWhenPaused?Time.unscaledTimeAsDouble:Time.timeAsDouble;
            Vector3 axis=generatedRoot.InverseTransformDirection(_up).normalized;
            foreach(var d in drift)
            {
                if(d.target==null)continue;
                double phase=(time/Math.Max(1,d.period)+d.phase)%1.0;float theta=(float)(phase*Math.PI*2);
                // Absolute baseline + sinusoid: no integration drift, no shared synchronous phase.
                d.target.localPosition=d.origin+axis*(Mathf.Sin(theta)*d.amplitude);
                d.target.localRotation=d.rotation*Quaternion.Euler(Mathf.Sin((float)(((time/(d.period*1.37)+d.phase*.71)%1.0)*Math.PI*2))*d.angle,0,Mathf.Cos(theta)*d.angle);
            }
        }
    }
}
