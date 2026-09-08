using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Fire;
using Elemental.Simulation.Gravity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

namespace Elemental.Presentation.Fire
{
    public enum FireLabScenario { Direct, Oblique, Corner, Edge, Opening, Moving, Invalidation, EightGroups }

    // Isolated executable harness. Public controls are usable by PlayMode tests and
    // editor menu calls without adding gameplay hotkeys or touching the arena.
    public sealed class FireLabDriver : MonoBehaviour
    {
        [SerializeField] private FireVisualProfile profile;
        [SerializeField] private Material labSurface;
        [SerializeField] private FireLabScenario scenario;
        [SerializeField] private bool bloomEnabled = true;
        private FireWorldBehaviour world;
        private UnityEngine.Camera labCamera;
        private FireLightPool lights;
        private Volume volume;
        private VolumeProfile volumeProfile;
        private Material wallMaterial;
        private readonly FirePresentationController[] views = new FirePresentationController[8];
        private readonly FireGroupHandle[] groups = new FireGroupHandle[8];
        private readonly FirePresentationSnapshot[] snapshots = new FirePresentationSnapshot[8];
        private readonly FireFieldNode[] nodes = new FireFieldNode[1];
        private readonly GameObject[] walls = new GameObject[3];
        private readonly FireSurfaceBinding[] surfaces = new FireSurfaceBinding[3];
        private Rigidbody movingBody;
        private float scenarioStart;
        private bool invalidated;
        private bool initialized;
        public double CpuPresentationMilliseconds
        {
            get { double total=0;for(int i=0;i<views.Length;i++) if(groups[i].IsValid && views[i]!=null && views[i].CpuDiagnostics!=null) total+=views[i].CpuDiagnostics.LastStepMilliseconds;return total; }
        }
        public int TotalAliveParticles
        {
            get {int count=0;for(int i=0;i<views.Length;i++) if(views[i]!=null)count+=views[i].AliveParticles;return count;}
        }
        public FireWorldBehaviour World => world;
        public UnityEngine.Camera LabCamera => labCamera;
        public FireLabScenario Scenario => scenario;
        public FirePresentationController PrimaryView => views[0];
        public FirePresentationSnapshot PrimarySnapshot => snapshots[0];
        public void Configure(FireVisualProfile settings, Material surface = null) { profile = settings; if (surface != null) labSurface = surface; }
        private GameObject Child(string name)
        { var go = new GameObject(name); go.transform.SetParent(transform,false); return go; }
        private void Start()
        {
            if (profile == null || !profile.IsValid) { Debug.LogError("FireLab requires generated Fire_Default profile.",this); enabled=false; return; }
            var gravitySource = Child("FireLab gravity source").AddComponent<PointPlanetGravitySource>();
            gravitySource.transform.position = new Vector3(0,-10000,0);
            gravitySource.Configure(new GravityFieldId(707),10000,14,2,40000);
            var gravity = Child("FireLab gravity").AddComponent<GravityWorldBehaviour>();
            gravity.Configure(new[]{gravitySource});
            world = Child("FireLab world").AddComponent<FireWorldBehaviour>();
            world.Configure(gravity,new FireWorldSettings(8,profile.MaxLifetime,0.1f,profile.MaximumSpeed));
            lights = Child("FireLab lights (budget 2)").AddComponent<FireLightPool>();
            labCamera = Child("FireLab camera").AddComponent<UnityEngine.Camera>();
            labCamera.transform.position = new Vector3(-7,4,-8);
            labCamera.transform.LookAt(new Vector3(-0.5f,1.4f,0));
            labCamera.backgroundColor = new Color(0.025f,0.028f,0.04f);
            labCamera.clearFlags = CameraClearFlags.SolidColor; labCamera.allowHDR = true;
            labCamera.nearClipPlane = 0.05f; labCamera.farClipPlane = 100;
            labCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            labCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            var sun = Child("FireLab key light").AddComponent<Light>();
            sun.type=LightType.Directional; sun.intensity=1.2f; sun.transform.rotation=Quaternion.Euler(45,-30,0);
            volume = Child("FireLab bloom").AddComponent<Volume>(); volume.isGlobal=true;
            volume.priority=20; volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile=volumeProfile;
            var bloom=volumeProfile.Add<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
            SetBloom(bloomEnabled);
            wallMaterial = labSurface != null ? new Material(labSurface) : new Material(Shader.Find("Universal Render Pipeline/Lit")); wallMaterial.color = new Color(0.16f,0.18f,0.21f);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name="FireLab floor"; ground.transform.SetParent(transform,false);
            ground.transform.position=new Vector3(0,-0.2f,0); ground.transform.localScale=new Vector3(22,0.4f,35);
            ground.GetComponent<Renderer>().sharedMaterial=wallMaterial; ground.AddComponent<FireSurfaceBinding>().Configure(710);
            for(int i=0;i<walls.Length;i++)
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name="FireLab face " + i; wall.transform.SetParent(transform,false);
                wall.GetComponent<Renderer>().sharedMaterial=wallMaterial; walls[i]=wall;
                surfaces[i]=wall.AddComponent<FireSurfaceBinding>(); surfaces[i].Configure((uint)(720+i));
            }
            movingBody=walls[0].AddComponent<Rigidbody>(); movingBody.isKinematic=true; movingBody.useGravity=false;
            for(int i=0;i<8;i++)
            {
                snapshots[i]=new FirePresentationSnapshot(); var go=Child("FireLab group " + i); go.AddComponent<VisualEffect>();
                views[i]=go.AddComponent<FirePresentationController>(); views[i].Configure(profile,labCamera,lights);
            }
            initialized=true; ApplyScenario(scenario);
            foreach(var argument in System.Environment.GetCommandLineArgs())
                if(argument=="--fire-benchmark") { gameObject.AddComponent<FireLabBenchmark>().Configure(this); break; }
        }
        public void SetBloom(bool value) { bloomEnabled=value; if(volume!=null) volume.enabled=value; }
        public void SetTimeScale(float value) { Time.timeScale=Mathf.Clamp(value,0,1); }
        public void SetScenario(FireLabScenario value) { scenario=value; if(initialized) ApplyScenario(value); }
        public void StopEmission() { for(int i=0;i<groups.Length;i++) if(groups[i].IsValid) world.Stop(groups[i]); }
        private void ApplyScenario(FireLabScenario value)
        {
            scenarioStart=Time.time; invalidated=false;
            for(int i=0;i<walls.Length;i++) { surfaces[i].InvalidateGeometry(); walls[i].SetActive(i==0); }
            walls[0].transform.SetPositionAndRotation(new Vector3(0,1.7f,0),Quaternion.identity);
            walls[0].transform.localScale=new Vector3(0.25f,3.4f,4);
            if(value==FireLabScenario.Edge) walls[0].transform.position=new Vector3(0,1.7f,1.65f);
            if(value==FireLabScenario.Corner)
            {
                walls[1].SetActive(true); walls[1].transform.SetPositionAndRotation(new Vector3(-1.9f,1.7f,1.8f),Quaternion.identity);
                walls[1].transform.localScale=new Vector3(4,3.4f,0.25f);
            }
            if(value==FireLabScenario.Opening)
            {
                walls[0].transform.position=new Vector3(0,1.7f,-1.4f); walls[0].transform.localScale=new Vector3(0.25f,3.4f,1.4f);
                walls[1].SetActive(true); walls[1].transform.position=new Vector3(0,1.7f,1.4f); walls[1].transform.localScale=new Vector3(0.25f,3.4f,1.4f);
            }
            if(value==FireLabScenario.EightGroups)
            {
                walls[0].transform.localScale=new Vector3(0.25f,3.4f,30);
                labCamera.transform.position=new Vector3(-18,12,-18); labCamera.transform.LookAt(new Vector3(-1,1.4f,0));
            }
            else { labCamera.transform.position=new Vector3(-7,4,-8); labCamera.transform.LookAt(new Vector3(-0.5f,1.4f,0)); }
            Physics.SyncTransforms();
            for(int i=0;i<8;i++)
            {
                views[i].ForceHighDetail = value == FireLabScenario.EightGroups;
                if(i>0 && value!=FireLabScenario.EightGroups) { if(groups[i].IsValid) world.Stop(groups[i]); continue; }
                float z=value==FireLabScenario.EightGroups ? (i-3.5f)*3 : 0;
                var a=new float3(-3,1.4f,z); var b=new float3(-0.3f,1.4f,z); var flow=new float3(12,0,0);
                if(value==FireLabScenario.Oblique || value==FireLabScenario.Corner) { a.z-=1.2f; b.z+=0.4f; flow=math.normalize(b-a)*12; }
                if(value==FireLabScenario.Corner) {a.z=0;b.z=1.45f;flow=math.normalize(b-a)*12;}
                nodes[0]=FireFieldNode.Stream(a,b,flow,new float3(0,1,0)); nodes[0].NoiseFrequency=2.3f;
                if(groups[i].IsValid && world.World.IsCurrent(groups[i])) world.TrySetNodes(groups[i],nodes,1);
                else world.TryCreate((uint)(1707+i*997),1,nodes[0],out groups[i]);
            }
        }
        private void FixedUpdate()
        {
            if(!initialized) return;
            if(scenario==FireLabScenario.Moving)
                movingBody.MovePosition(new Vector3(Mathf.Sin(Time.time-scenarioStart)*0.7f,1.7f,0));
            if(scenario==FireLabScenario.Invalidation && !invalidated && Time.time-scenarioStart>3)
            { surfaces[0].InvalidateGeometry(); walls[0].SetActive(false); invalidated=true; }
        }
        private void LateUpdate()
        {
            if(!initialized) return;
            for(int i=0;i<8;i++)
            {
                if(!groups[i].IsValid) continue;
                if(world.CopySnapshot(groups[i],snapshots[i])) views[i].Publish(snapshots[i]);
                else { views[i].Retire(); groups[i]=default; }
            }
        }
        private void OnDestroy()
        {
            Time.timeScale=1;
            if(volumeProfile!=null) Destroy(volumeProfile);
            if(wallMaterial!=null) Destroy(wallMaterial);
        }
    }
}





