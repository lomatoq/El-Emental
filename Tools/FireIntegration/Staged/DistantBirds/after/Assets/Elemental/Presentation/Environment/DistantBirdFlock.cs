using Elemental.Presentation.UI;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Elemental.Presentation.DistantScenery
{
    [DisallowMultipleComponent,RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class DistantBirdFlock:MonoBehaviour
    {
        [SerializeField] private Transform planetCenter;
        [SerializeField] private FrontendFlowController settingsSource;
        [SerializeField] private Material silhouetteMaterial;
        [SerializeField] private Vector3 stagingUp=Vector3.up,viewDirection=Vector3.forward;
        [SerializeField] private int seed=38217;
        [SerializeField,Range(8,24)] private int birdCount=16;
        private DistantBirdFlight.Bird[] birds;
        private Vector3[] vertices;
        private Mesh mesh;
        private Vector3 center,anchorPosition;
        private Quaternion frame,anchorRotation;
        private Vector3 anchorScale;
        private bool wasReduced;
        public int BirdCount=>birds==null?0:birds.Length;
        public Mesh GeneratedMesh=>mesh;
        public void Configure(Transform planet,Vector3 up,Vector3 direction,FrontendFlowController settings,Material material)
        {planetCenter=planet;stagingUp=up;viewDirection=direction;settingsSource=settings;silhouetteMaterial=material;Rebuild();}
        private void OnEnable(){if(planetCenter!=null&&silhouetteMaterial!=null)Rebuild();}
        public void Rebuild()
        {
            Release();if(planetCenter==null||silhouetteMaterial==null)return;
            center=planetCenter.position;Vector3 up=stagingUp.normalized,forward=Vector3.ProjectOnPlane(viewDirection,up).normalized;
            if(up.sqrMagnitude<.9f||forward.sqrMagnitude<.9f){Debug.LogError("Bird flock requires an explicit valid authored planet frame.",this);return;}
            frame=Quaternion.LookRotation(forward,up);
            // This owned root is world anchored; vertices are evaluated in its fixed frame.
            transform.SetPositionAndRotation(center,frame);transform.localScale=Vector3.one;
            anchorPosition=transform.position;anchorRotation=transform.rotation;anchorScale=transform.localScale;
            int count=Mathf.Clamp(birdCount,8,24);birds=new DistantBirdFlight.Bird[count];vertices=new Vector3[count*8];int[] indices=new int[count*18];
            int[] topology={0,2,1,0,1,3,2,4,5,2,5,1,3,1,7,3,7,6};
            Bounds bounds=new Bounds();
            for(int i=0;i<count;i++)
            {
                birds[i]=DistantBirdFlight.Create(seed,i);
                var envelope=new Bounds((Vector3)birds[i].Center,new Vector3(260,100,180));
                if(i==0)bounds=envelope;else bounds.Encapsulate(envelope);
                for(int j=0;j<18;j++)indices[i*18+j]=i*8+topology[j];
            }
            mesh=new Mesh{name="EE_DistantBirds_Dynamic",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();mesh.vertices=vertices;mesh.triangles=indices;mesh.bounds=bounds;
            GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=silhouetteMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            wasReduced=false;ApplyTime(0,settingsSource!=null&&settingsSource.Preferences.ReducedMotion);
        }
        private void LateUpdate()
        {
            bool reduced=settingsSource!=null&&settingsSource.Preferences.ReducedMotion;
            if(reduced&&wasReduced&&transform.position==anchorPosition&&transform.rotation==anchorRotation&&transform.localScale==anchorScale)return;
            ApplyTime(Time.unscaledTimeAsDouble,reduced);
        }
        public void ApplyTime(double time,bool reducedMotion)
        {
            if(mesh==null||birds==null)return;
            Profiler.BeginSample("DistantBirdFlock.UpdateMesh");
            try
            {
                if(transform.position!=anchorPosition||transform.rotation!=anchorRotation||transform.localScale!=anchorScale)
                {transform.SetPositionAndRotation(anchorPosition,anchorRotation);transform.localScale=anchorScale;}
                for(int i=0;i<birds.Length;i++)
                {
                    var pose=DistantBirdFlight.Evaluate(birds[i],time,reducedMotion);
                    Quaternion rotation=Quaternion.LookRotation((Vector3)pose.Forward,Vector3.up)*Quaternion.AngleAxis(pose.Bank*Mathf.Rad2Deg,Vector3.forward);
                    Vector3 p=(Vector3)pose.Position;float span=birds[i].Span,c=Mathf.Cos(pose.WingAngle),s=Mathf.Sin(pose.WingAngle);int v=i*8;
                    vertices[v]=p+rotation*new Vector3(0,0,.30f*span);vertices[v+1]=p+rotation*new Vector3(0,0,-.23f*span);
                    vertices[v+2]=p+rotation*new Vector3(-.04f*span,0,.05f*span);vertices[v+3]=p+rotation*new Vector3(.04f*span,0,.05f*span);
                    vertices[v+4]=p+rotation*new Vector3(-.5f*c*span,.5f*s*span,-.06f*span);vertices[v+5]=p+rotation*new Vector3(-.30f*c*span,.30f*s*span,-.22f*span);
                    vertices[v+6]=p+rotation*new Vector3(.5f*c*span,.5f*s*span,-.06f*span);vertices[v+7]=p+rotation*new Vector3(.30f*c*span,.30f*s*span,-.22f*span);
                }
                mesh.SetVertices(vertices,0,vertices.Length,MeshUpdateFlags.DontRecalculateBounds);wasReduced=reducedMotion;
            }
            finally{Profiler.EndSample();}
        }
        private void OnDisable()=>Release();
        private void Release()
        {if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}mesh=null;birds=null;vertices=null;}
    }
}
