using UnityEngine;

namespace Elemental.Presentation.DistantScenery
{
    [CreateAssetMenu(menuName="EL EMENTAL/Environment/Distant backdrop profile")]
    public sealed class DistantBackdropProfile : ScriptableObject
    {
        [Header("Procedural valley V2 (explicit bake opt-in)")]
        public bool proceduralValley;
        [System.Serializable] public struct ViewLandmark
        {public string name;public bool airborne;public int variant;public Vector3 position;public float scale;}
        [Header("Explicit Main and Combat composition")]
        public bool viewComposition;
        public Mesh[] viewGroundPillars=new Mesh[0];
        public ViewLandmark[] viewLandmarks=DefaultViewLandmarks();
        public static ViewLandmark[] DefaultViewLandmarks()=>new[]{
                new ViewLandmark{name="MainGround0",airborne=false,variant=0,position=new Vector3(157.1664f,-155.0000f,-358.2600f),scale=56.0281f},
                new ViewLandmark{name="MainGround1",airborne=false,variant=1,position=new Vector3(-247.9900f,-155.0000f,-657.9805f),scale=101.1590f},
                new ViewLandmark{name="MainGround2",airborne=false,variant=2,position=new Vector3(34.4868f,-155.0000f,-768.6380f),scale=60.3472f},
                new ViewLandmark{name="CombatGround0",airborne=false,variant=3,position=new Vector3(-220.2358f,-155.0000f,370.4188f),scale=53.5820f},
                new ViewLandmark{name="CombatGround1",airborne=false,variant=4,position=new Vector3(347.4264f,-155.0000f,553.7343f),scale=74.9388f},
                new ViewLandmark{name="CombatGround2",airborne=false,variant=5,position=new Vector3(-116.1746f,-155.0000f,744.4065f),scale=114.6714f},
                new ViewLandmark{name="MainIsland0",airborne=true,variant=0,position=new Vector3(110.7934f,77.6273f,-542.6574f),scale=200.8952f},
                new ViewLandmark{name="MainIsland1",airborne=true,variant=2,position=new Vector3(-123.3363f,164.0052f,-878.0622f),scale=188.0579f},
                new ViewLandmark{name="MainIsland2",airborne=true,variant=5,position=new Vector3(-15.5528f,289.9939f,-1166.1901f),scale=156.5881f},
                new ViewLandmark{name="CombatIsland0",airborne=true,variant=1,position=new Vector3(-188.5729f,77.4896f,540.3479f),scale=195.0210f},
                new ViewLandmark{name="CombatIsland1",airborne=true,variant=3,position=new Vector3(313.7724f,72.6067f,790.1863f),scale=181.1913f},
                new ViewLandmark{name="CombatIsland2",airborne=true,variant=4,position=new Vector3(108.0505f,210.3195f,1100.2729f),scale=161.0330f},
        };

        [Tooltip("Add bounded negative-Z sidebands for the front-facing menu; existing combat placements stay unchanged.")]
        public bool rearContinuation;
        public int geometrySeed=13771, motionSeed=24611;
        public RockShapeSettings rockShape=RockShapeSettings.Default;
        [Range(8,12)] public int valleyGroups=10;
        [Range(4,6)] public int floatingGroups=5;
        [Range(3,7)] public int pillarsPerGroup=5;
        [Min(100)] public float valleyWidth=660;
        [Min(300)] public float valleyLength=1050;
        [Min(50)] public float valleyFloorBelowCenter=155;
        public Vector2 valleyGroupHeight=new Vector2(220,380);
        public Vector2 floatingGroupHeight=new Vector2(55,100);
        [Tooltip("Reference pillar scale. Floating meshes are already 40–50% as tall; do not shrink them a second time.")]
        public Vector2 floatingPillarScale=new Vector2(220,380);
        [Min(30)] public float arenaExclusionPadding=120;
        [Tooltip("Boxes in the fixed planet-centered authored frame: x=tangent, y=up, z=valley direction.")]
        public Bounds[] exclusionVolumes=new Bounds[0];
        public Vector2 levitationAmplitude=new Vector2(.08f,.4f);
        public Vector2 levitationPeriod=new Vector2(10,22);
        [Range(.2f,.4f)] public float horizontalMotionFraction=.3f;
        public Vector2 rockingDegrees=new Vector2(.3f,1.2f);
        public int seed=4471;
        [Min(1)] public float planetRadius=55.1f;
        public Material material;
        public Mesh[] silhouettes;
        public Mesh[] lodSilhouettes;
        public Mesh[] islandSilhouettes;
        public Mesh[] islandLodSilhouettes;
        [Range(0,31)] public int layer=0;
        [Range(0,40)] public int midMassifs=12;
        [Range(0,40)] public int farMassifs=14;
        [Range(0,24)] public int floatingIslands=8;
        public Vector2 midDistance=new Vector2(460,850);
        public Vector2 farDistance=new Vector2(1250,2300);
        public Vector2 islandDistance=new Vector2(320,740);
        public Vector2 midHeight=new Vector2(105,230);
        public Vector2 farHeight=new Vector2(340,680);
        public Vector2 islandHeight=new Vector2(24,66);
        [Tooltip("Tangential width relative to height; angular coverage can increase it further.")]
        public Vector2 massifWidthAspect=new Vector2(2.2f,4f);
        [Range(1.2f,2f)] public float ridgeOverlap=1.65f;
        [Tooltip("Fraction of each ridge height below the planet center's staging plane.")]
        public Vector2 ridgeBuriedFraction=new Vector2(.28f,.42f);
        [Range(0,45)] public float clearViewHalfAngle=14;
        [Tooltip("Local focus / menu camera direction. The environment stays fixed in world space.")]
        public Vector3 heroViewDirection=Vector3.forward;
        [Tooltip("Suggested explicit camera far plane. The generator never sets this on a live camera.")]
        public float suggestedFarClip=3000;
    }
}
