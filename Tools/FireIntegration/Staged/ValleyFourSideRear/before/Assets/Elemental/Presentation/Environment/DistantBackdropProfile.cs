using UnityEngine;

namespace Elemental.Presentation.DistantScenery
{
    [CreateAssetMenu(menuName="EL EMENTAL/Environment/Distant backdrop profile")]
    public sealed class DistantBackdropProfile : ScriptableObject
    {
        [Header("Procedural valley V2 (explicit bake opt-in)")]
        public bool proceduralValley;
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
