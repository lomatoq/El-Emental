using UnityEngine;
using Unity.Profiling;
namespace Elemental.Presentation.Rendering
{
    // Explicit optional frame publisher for the existing atmosphere fullscreen owner.
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class ValleyAtmosphereController:MonoBehaviour
    {
        public const string OwnedName="Valley Atmosphere V2 Frame";
        [SerializeField] private ValleyAtmosphereProfile profile;
        [SerializeField] private float planetRadius;
        [SerializeField] private GameObject retiredVolume;
        [SerializeField] private bool retiredVolumeWasActive;
        public bool FogEnabled=true,CloudsEnabled=true,AnimateClouds=true;
        public bool UseParticleClouds;
        public bool FarArtEnabled=true;
        [Range(0,0.06f)] public float FarStippleStrength=.04f;
        [Range(0,3)] public int DebugMode;
        private readonly Vector4[] banks=new Vector4[4],sizes=new Vector4[4];
        private static readonly ProfilerMarker Marker=new ProfilerMarker("Elemental.ValleyAtmosphere.Publish");
        private bool invalidReported;
        public void Configure(Transform planet,float radius,Vector3 authoredUp,Vector3 authoredForward,ValleyAtmosphereProfile settings)
        {
            if(planet==null || settings==null || radius<=0)throw new System.ArgumentException("Explicit planet, radius and valley profile required.");
            Vector3 up=planet.InverseTransformDirection(authoredUp).normalized;
            Vector3 forward=Vector3.ProjectOnPlane(planet.InverseTransformDirection(authoredForward),up).normalized;
            if(up.sqrMagnitude<0.5f || forward.sqrMagnitude<0.5f)throw new System.ArgumentException("Invalid authored valley frame.");
            transform.SetParent(planet,false);transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.LookRotation(forward,up);transform.localScale=Vector3.one;
            profile=settings;planetRadius=radius;Publish();
        }
        public void RetireRejectedVolume(GameObject volume)
        {
            if(volume==null)return;
            if(retiredVolume==null){retiredVolume=volume;retiredVolumeWasActive=volume.activeSelf;}
            volume.SetActive(false);
        }
        public void RestoreOriginalOwner()
        {
            FogEnabled=false;Publish();if(retiredVolume!=null)retiredVolume.SetActive(retiredVolumeWasActive);
        }
        private void OnEnable()=>Publish();
        private void LateUpdate()=>Publish();
        private void OnDisable(){Shader.SetGlobalFloat("_ElementalValleyEnabled",0);Shader.SetGlobalFloat("_ElementalValleyParticleCloudsEnabled",0);}
        public void Publish()
        {
            using(Marker.Auto())
            {
                if(profile==null)return;
                if(!profile.IsValid)
                {
                    Shader.SetGlobalFloat("_ElementalValleyEnabled",0);
                    if(!invalidReported){Debug.LogError("Valley atmosphere profile is invalid: assign cloud art and finite positive ranges.",this);invalidReported=true;}
                    return;
                }
                invalidReported=false;
                var palette=Elemental.Simulation.Rendering.ValleyTimePalette.Evaluate(Shader.GetGlobalFloat("_ElementalNight01"),Shader.GetGlobalFloat("_ElementalSolarAltitude"),
                    new Unity.Mathematics.float3(profile.DayFog.r,profile.DayFog.g,profile.DayFog.b),new Unity.Mathematics.float3(profile.DayFogBottom.r,profile.DayFogBottom.g,profile.DayFogBottom.b));
                Shader.SetGlobalVector("_ElementalValleyTimeFogTop",new Vector4(palette.FogTop.x,palette.FogTop.y,palette.FogTop.z,1));
                Shader.SetGlobalVector("_ElementalValleyTimeFogBottom",new Vector4(palette.FogBottom.x,palette.FogBottom.y,palette.FogBottom.z,1));
                Shader.SetGlobalVector("_ElementalValleyTimeCloudTop",new Vector4(palette.CloudTop.x,palette.CloudTop.y,palette.CloudTop.z,1));
                Shader.SetGlobalVector("_ElementalValleyTimeCloudBottom",new Vector4(palette.CloudBottom.x,palette.CloudBottom.y,palette.CloudBottom.z,1));
                float top=-planetRadius-profile.PlaneClearance;
                Shader.SetGlobalFloat("_ElementalValleyEnabled",FogEnabled?1:0);
                Shader.SetGlobalFloat("_ElementalValleyParticleCloudsEnabled",FogEnabled && CloudsEnabled && UseParticleClouds?1:0);
                Shader.SetGlobalFloat("_ElementalValleyDebug",DebugMode);
                Shader.SetGlobalFloat("_ElementalValleyChromaticPixels",profile.FarChromaticPixels);
                Shader.SetGlobalVector("_ElementalValleyFarArt",new Vector4(FarArtEnabled?1:0,600,1100,Mathf.Clamp(FarStippleStrength,0,.06f)));
                Shader.SetGlobalMatrix("_ElementalWorldToValley",Matrix4x4.TRS(transform.position,transform.rotation,Vector3.one).inverse);
                Shader.SetGlobalVector("_ElementalValleyFog",new Vector4(top,Mathf.Max(1,profile.HeightFalloff),Mathf.Max(0,profile.VeilDensity),Mathf.Max(1000,profile.SkyDistance)));
                Shader.SetGlobalVector("_ElementalValleyFar",new Vector4(Mathf.Max(100,profile.NearClearRange),Mathf.Max(1,profile.FarHazeDistance),Mathf.Clamp01(profile.MaximumOpaqueOpacity),CloudsEnabled && !UseParticleClouds?Mathf.Clamp01(profile.CloudOpacity):0));
                Shader.SetGlobalColor("_ElementalValleyDay",profile.DayFog);Shader.SetGlobalColor("_ElementalValleyBottom",profile.DayFogBottom);Shader.SetGlobalColor("_ElementalValleyNight",profile.NightFog);Shader.SetGlobalColor("_ElementalValleyDusk",profile.DuskFog);
                Shader.SetGlobalTexture("_ElementalValleyCloudArt",profile.CloudArt);
                float t=Application.isPlaying && AnimateClouds?Time.time*Mathf.PI*2/Mathf.Max(5,profile.CloudDriftPeriod):0;
                for(int i=0;i<4;i++)
                {
                    Vector3 p=i==0?profile.Bank0:i==1?profile.Bank1:i==2?profile.Bank2:profile.Bank3;Vector2 size=i==0?profile.Bank0Size:i==1?profile.Bank1Size:i==2?profile.Bank2Size:profile.Bank3Size;
                    p.y+=top;p.x+=Mathf.Sin(t+i*1.7f)*profile.CloudDriftMetres;p.y+=Mathf.Sin(t*0.73f+i*2.1f)*profile.CloudDriftMetres*0.2f;
                    banks[i]=new Vector4(p.x,p.y,p.z,0);sizes[i]=new Vector4(Mathf.Max(1,size.x),Mathf.Max(1,size.y),i%2,0);
                }
                Shader.SetGlobalVectorArray("_ElementalValleyBanks4",banks);Shader.SetGlobalVectorArray("_ElementalValleyBankSizes4",sizes);
            }
        }
    }
}
