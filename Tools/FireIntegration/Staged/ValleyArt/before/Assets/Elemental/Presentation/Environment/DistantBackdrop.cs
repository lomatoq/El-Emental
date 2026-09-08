using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Elemental.Presentation.UI;

namespace Elemental.Presentation.DistantScenery
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
        [SerializeField] private FrontendFlowController settingsSource;
        public FrontendFlowController SettingsSource => settingsSource;
        [Tooltip("World-space up of the arena's staging location, not camera.up every frame.")]
        public Vector3 stagingUp=Vector3.up;
        public bool reducedMotion;
        public bool animateWhenPaused=false;
        [SerializeField] private Transform generatedRoot;
        [SerializeField] private List<Drift> drift=new List<Drift>();
        private Vector3 _up;
        public int RejectedPlacements { get; private set; }
        public int GeneratedCount=>drift.Count;
        public float ScaleFactor=>profile==null?1:Mathf.Max(1,profile.planetRadius)/36f;
        private void OnEnable(){_up=stagingUp.sqrMagnitude>.01f?stagingUp.normalized:Vector3.up;}
        public void Configure(DistantBackdropProfile settings, Transform planet, Vector3 authoredUp)
        { profile=settings; planetCenter=planet; stagingUp=authoredUp.normalized; }
        public void BindSettings(FrontendFlowController source) => settingsSource=source;
        [ContextMenu("Rebuild decorative background only")]
        public void Rebuild()
        {
            if(profile==null||planetCenter==null||profile.material==null||profile.silhouettes==null||profile.silhouettes.Length==0 ||
                profile.islandSilhouettes==null||profile.islandSilhouettes.Length==0)
            {Debug.LogError("DistantBackdrop needs a profile, material and mesh library.",this);return;}
            // Only destroy the exact root previously created by this component.
            if(generatedRoot!=null){generatedRoot.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generatedRoot.gameObject);else DestroyImmediate(generatedRoot.gameObject);}
            drift.Clear();generatedRoot=new GameObject("EE_Generated_Backdrop").transform;generatedRoot.SetParent(transform,false);
            _up=stagingUp.sqrMagnitude>.01f?stagingUp.normalized:Vector3.up;
            Vector3 forward=Vector3.ProjectOnPlane(profile.heroViewDirection,_up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.forward+Vector3.right,_up).normalized;
            Vector3 right=Vector3.Cross(_up,forward).normalized;
            Vector3 center=planetCenter.position; RejectedPlacements=0;
            var random=new System.Random(profile.seed);
            BuildLayer(0,Mathf.Clamp(profile.midMassifs,0,40),profile.midDistance,profile.midHeight,center,forward,right,random);
            BuildLayer(1,Mathf.Clamp(profile.farMassifs,0,40),profile.farDistance,profile.farHeight,center,forward,right,random);
            BuildLayer(2,Mathf.Clamp(profile.floatingIslands,0,24),profile.islandDistance,profile.islandHeight,center,forward,right,random);
        }
        private static float Next(System.Random r,float a,float b)=>Mathf.Lerp(a,b,(float)r.NextDouble());
        private void BuildLayer(int layerIndex,int count,Vector2 distances,Vector2 heights,Vector3 center,Vector3 forward,Vector3 right,System.Random rng)
        {
            float scale=ScaleFactor;
            var bearings=new float[count];int accepted=0;
            for(int i=0;i<count;i++)
            {
                float degrees=0;bool placed=false;
                // Ridge cells guarantee broad coverage; islands retain their independent placement.
                float cell=(360f-2f*profile.clearViewHalfAngle)/Mathf.Max(1,count);
                if(layerIndex!=2)
                {
                    degrees=profile.clearViewHalfAngle+(i+.5f+Next(rng,-.12f,.12f))*cell;
                    placed=true;
                }
                for(int attempt=0;!placed && attempt<24;attempt++)
                {
                    degrees=Next(rng,-180,180);
                    if(Mathf.Abs(degrees)<profile.clearViewHalfAngle)continue;
                    bool separated=true;
                    for(int j=0;j<accepted;j++) if(Mathf.Abs(Mathf.DeltaAngle(degrees,bearings[j]))<360f/Mathf.Max(1,count)*.32f) {separated=false;break;}
                    if(separated){placed=true;break;}
                }
                if(!placed){RejectedPlacements++;continue;} bearings[accepted++]=degrees;
                float angle=degrees*Mathf.Deg2Rad;
                Vector3 direction=forward*Mathf.Cos(angle)+right*Mathf.Sin(angle);
                // A slowly varying radius joins adjacent ridge pieces instead of scattering pillars.
                float distance=(layerIndex==2?Next(rng,distances.x,distances.y):
                    Mathf.Lerp(distances.x,distances.y,.5f+.18f*Mathf.Sin(angle*2.3f+layerIndex*1.7f)+Next(rng,-.035f,.035f)))*scale;
                float sizeSample=(float)rng.NextDouble();
                float height=Mathf.Lerp(heights.x,heights.y,sizeSample*sizeSample)*scale;
                float width=height*(layerIndex==2?Next(rng,1.4f,2.7f):Next(rng,profile.massifWidthAspect.x,profile.massifWidthAspect.y));
                if(layerIndex!=2)
                    width=Mathf.Max(width,2f*distance*Mathf.Sin(Mathf.Min(cell,90f)*.5f*Mathf.Deg2Rad)*Mathf.Clamp(profile.ridgeOverlap,1.2f,2f)/.77f);
                float verticalCenter=layerIndex==2?profile.planetRadius+Next(rng,40,160)*scale:
                    height*(.5f-Mathf.Clamp(Next(rng,profile.ridgeBuriedFraction.x,profile.ridgeBuriedFraction.y),.1f,.48f));
                float depth=layerIndex==2?width*Next(rng,.65f,1.1f):Mathf.Min(width*Next(rng,.30f,.45f),distance*.32f);
                var pivot=new GameObject((layerIndex==2?"Island_":layerIndex==1?"FarMassif_":"MidMassif_")+i.ToString("00")).transform;
                pivot.SetParent(generatedRoot,false);pivot.position=center+direction*distance+_up*verticalCenter;
                pivot.rotation=Quaternion.LookRotation(direction,_up)*Quaternion.Euler(0,layerIndex==2?Next(rng,0,360):Next(rng,-6,6),layerIndex==2?Next(rng,-5,5):0);
                Mesh[] meshes=layerIndex==2?profile.islandSilhouettes:profile.silhouettes;
                Mesh[] lodMeshes=layerIndex==2?profile.islandLodSilhouettes:profile.lodSilhouettes;
                int index=rng.Next(meshes.Length);
                var renderers=new List<Renderer>();
                AddRenderer(pivot,"LOD0",meshes[index],new Vector3(width,height,depth),renderers);
                Mesh low=lodMeshes!=null&&lodMeshes.Length>index?lodMeshes[index]:null;
                if(low!=null)
                {
                    var lowRenderers=new List<Renderer>();AddRenderer(pivot,"LOD1",low,pivot.GetChild(0).localScale,lowRenderers);
                    var lod=pivot.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.075f,renderers.ToArray()),new LOD(.0025f,lowRenderers.ToArray())});lod.RecalculateBounds();
                }
                drift.Add(new Drift{target=pivot,origin=pivot.position,rotation=pivot.rotation,phase=Next(rng,0,1),period=Next(rng,85,165),amplitude=(layerIndex==2?Next(rng,.65f,2.2f):0)*scale,angle=layerIndex==2?Next(rng,.06f,.22f):0});
            }
        }
        private void AddRenderer(Transform parent,string name,Mesh mesh,Vector3 scale,List<Renderer> result)
        {
            if(mesh==null)return;
            var go=new GameObject(name);go.layer=Mathf.Clamp(profile.layer,0,31);go.transform.SetParent(parent,false);go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=profile.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            result.Add(renderer);
        }
        public void SetReducedMotion(bool value)
        {
            reducedMotion=value;
            if(value)foreach(var d in drift)if(d.target!=null){d.target.position=d.origin;d.target.rotation=d.rotation;}
        }
        private void LateUpdate()
        {
            if(settingsSource!=null && reducedMotion!=settingsSource.Preferences.ReducedMotion)
                SetReducedMotion(settingsSource.Preferences.ReducedMotion);
            double time=animateWhenPaused?Time.unscaledTimeAsDouble:Time.timeAsDouble;
            ApplyTime(time);
        }
        public void ApplyTime(double time)
        {
            if(generatedRoot==null || double.IsNaN(time) || double.IsInfinity(time))return;
            Vector3 axis=_up;
            foreach(var d in drift)
            {
                if(d.target==null)continue;
                if(reducedMotion || (d.amplitude==0 && d.angle==0))
                {
                    if(d.target.position!=d.origin || d.target.rotation!=d.rotation)
                        d.target.SetPositionAndRotation(d.origin,d.rotation);
                    continue;
                }
                double phase=(time/Math.Max(1,d.period)+d.phase)%1.0;float theta=(float)(phase*Math.PI*2);
                // Absolute baseline + sinusoid: no integration drift, no shared synchronous phase.
                Vector3 position=d.origin+axis*(Mathf.Sin(theta)*d.amplitude);
                Quaternion rotation=d.rotation*Quaternion.Euler(Mathf.Sin((float)(((time/(d.period*1.37)+d.phase*.71)%1.0)*Math.PI*2))*d.angle,0,Mathf.Cos(theta)*d.angle);
                if(d.target.position!=position || d.target.rotation!=rotation)d.target.SetPositionAndRotation(position,rotation);
            }
        }
    }
}
