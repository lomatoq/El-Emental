using UnityEngine;
namespace Elemental.Presentation.Rendering
{
    [CreateAssetMenu(menuName="Elemental/Rendering/Valley Atmosphere V2")]
    public sealed class ValleyAtmosphereProfile:ScriptableObject
    {
        [Min(1)] public float PlaneClearance=15;
        [Min(1)] public float HeightFalloff=32;
        [Range(0,0.2f)] public float VeilDensity=0.025f;
        [Min(1000)] public float SkyDistance=12000;
        [Min(100)] public float NearClearRange=300;
        [Min(1)] public float FarHazeDistance=1800;
        [Range(0,1)] public float MaximumOpaqueOpacity=0.60f;
        public Color DayFog=new Color(0.84f,0.85f,0.84f);
        public Color NightFog=new Color(0.024f,0.035f,0.058f);
        public Color DuskFog=new Color(0.50f,0.36f,0.33f);
        public Texture2D CloudArt;
        [Range(0,1)] public float CloudOpacity=0.74f;
        [Min(0)] public float CloudDriftMetres=8;
        [Min(5)] public float CloudDriftPeriod=160;
        private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        private static bool Finite(Vector2 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&v.x>0&&v.y>0;
        public bool IsValid=>CloudArt!=null && float.IsFinite(PlaneClearance)&&PlaneClearance>0 &&
            float.IsFinite(HeightFalloff)&&HeightFalloff>0 && float.IsFinite(VeilDensity)&&VeilDensity>=0 &&
            float.IsFinite(SkyDistance)&&SkyDistance>=1000 && float.IsFinite(NearClearRange)&&NearClearRange>=100 &&
            float.IsFinite(FarHazeDistance)&&FarHazeDistance>0 && float.IsFinite(MaximumOpaqueOpacity)&&MaximumOpaqueOpacity>=0&&MaximumOpaqueOpacity<=1 &&
            float.IsFinite(CloudOpacity)&&CloudOpacity>=0&&CloudOpacity<=1 && float.IsFinite(CloudDriftMetres)&&CloudDriftMetres>=0 &&
            float.IsFinite(CloudDriftPeriod)&&CloudDriftPeriod>0 && Finite(Bank0)&&Finite(Bank1)&&Finite(Bank2)&&Finite(Bank0Size)&&Finite(Bank1Size)&&Finite(Bank2Size);
        public Vector3 Bank0=new Vector3(-510,35,650);
        public Vector3 Bank1=new Vector3(590,55,1000);
        public Vector3 Bank2=new Vector3(-120,0,1500);
        public Vector2 Bank0Size=new Vector2(650,430);
        public Vector2 Bank1Size=new Vector2(820,545);
        public Vector2 Bank2Size=new Vector2(1050,700);
    }
}
