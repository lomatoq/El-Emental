using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Elemental.Presentation.UI;

namespace Elemental.Presentation.DistantScenery
{
    /// <summary>Optional additive background. No colliders, rigidbodies, network entities or gameplay changes.</summary>
    public sealed partial class DistantBackdrop : MonoBehaviour
    {
        [Serializable] private struct Drift
        {
            public Transform target;public Vector3 origin;public Quaternion rotation;
            public float phase,period,amplitude,angle;
            public Vector3 tangent,bitangent;public float horizontalFraction;public Vector3 spinAxis,centerOffset;public float spinDegreesPerSecond;
        }
        private struct SatelliteRequest
        {
            public int index;public Vector3 position;public Quaternion rotation;public float scale;
            public Mesh high,low;public Bounds envelope;
        }
        public DistantBackdropProfile profile;
        public Transform planetCenter;
        [SerializeField] private FrontendFlowController settingsSource;
        public FrontendFlowController SettingsSource => settingsSource;
        [Tooltip("World-space up of the arena's staging location, not camera.up every frame.")]
        public Vector3 stagingUp=Vector3.up;
        public bool reducedMotion;
        public bool animateWhenPaused=false;
        public bool environmentPaused;
        [Min(0)] public float environmentSpeed=1;
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
            if(profile.proceduralValley&&profile.viewComposition&&(profile.viewGroundPillars==null||profile.viewGroundPillars.Length!=6||profile.viewLandmarks==null||(profile.viewLandmarks.Length<12||profile.viewLandmarks.Length>44)))
            {Debug.LogError("Use Compose Main And Combat Views to bind the six accepted pillar assets and twelve to forty-four authored slots.",this);return;}
            // Only destroy the exact root previously created by this component.
            if(generatedRoot!=null){generatedRoot.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generatedRoot.gameObject);else DestroyImmediate(generatedRoot.gameObject);}
            drift.Clear();generatedRoot=new GameObject("EE_Generated_Backdrop").transform;generatedRoot.SetParent(transform,false);
            _up=stagingUp.sqrMagnitude>.01f?stagingUp.normalized:Vector3.up;
            Vector3 forward=Vector3.ProjectOnPlane(profile.heroViewDirection,_up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.forward+Vector3.right,_up).normalized;
            Vector3 right=Vector3.Cross(_up,forward).normalized;
            Vector3 center=planetCenter.position; RejectedPlacements=0;
            var random=new System.Random(profile.seed);
            if(profile.proceduralValley){BuildValley(center,forward,right);return;}
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
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=profile.material;renderer.shadowCastingMode=profile.proceduralValley?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=profile.proceduralValley;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            result.Add(renderer);
        }
        public void ClearGenerated()
        {
            if(generatedRoot!=null){generatedRoot.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generatedRoot.gameObject);else DestroyImmediate(generatedRoot.gameObject);}
            generatedRoot=null;drift.Clear();
        }
        private void BuildValley(Vector3 center,Vector3 forward,Vector3 right)
        {
            var occupied=new List<Bounds>();
            var satellites=new List<SatelliteRequest>();
            var componentCache=new Dictionary<Mesh,Bounds[]>();
            int ground=Mathf.Clamp(profile.valleyGroups,8,12),floating=Mathf.Clamp(profile.floatingGroups,4,6);
            int originalCount=ground+floating, acceptedGround=0, acceptedFloating=0;
            int legacyCount=originalCount+(profile.rearContinuation?4:0);
            int savedCount=profile.viewComposition?Mathf.Min(44,profile.viewLandmarks.Length):0;
            int nearCount=profile.viewComposition&&profile.hardPolishSupplements&&profile.nearLandmarks!=null?Mathf.Min(5,profile.nearLandmarks.Length):0;
            int authoredCount=savedCount+nearCount;
            for(int i=0;i<legacyCount+authoredCount;i++)
            {
                bool authored=i>=legacyCount;
                int authoredIndex=i-legacyCount;
                var landmark=authored?(authoredIndex<savedCount?profile.viewLandmarks[authoredIndex]:profile.nearLandmarks[authoredIndex-savedCount]):default;
                bool rear=!authored&&i>=originalCount;int rearIndex=i-originalCount;
                bool airborne=authored?landmark.airborne:rear?rearIndex==3:i>=ground;
                if(profile.viewComposition&&!authored&&airborne)continue;
                int index=authored?100+i-legacyCount:rear?(airborne?floating:ground+rearIndex):(airborne?i-ground:i);
                if(authored&&(airborne?acceptedFloating>=32+nearCount:acceptedGround>=24))continue;
                if(rear && (airborne?acceptedFloating>=6:acceptedGround>=12))continue;
                var placement=new RockRandom(unchecked(profile.seed+index*73856093+(airborne?19391:0)));
                Mesh[] bank=airborne?profile.islandSilhouettes:authored?profile.viewGroundPillars:profile.silhouettes;
                Mesh[] lowBank=airborne?profile.islandLodSilhouettes:authored?null:profile.lodSilhouettes;
                int variant=authored?Mathf.Clamp(landmark.variant,0,bank.Length-1):index%bank.Length;Mesh mesh=bank[variant];
                if(mesh==null)throw new InvalidOperationException("Bake the V2 mesh library before regenerating the valley.");
                float t=index/(float)Mathf.Max(1,(airborne?floating:ground)-1);
                float side=rear?(rearIndex==1?1:-1):(index%2==0?-1:1);
                float height=airborne?placement.Next(profile.floatingPillarScale.x,profile.floatingPillarScale.y):
                    Mathf.Lerp(profile.valleyGroupHeight.x,profile.valleyGroupHeight.y,.5f+.35f*Mathf.Sin(t*5.3f+.4f));
                Quaternion rotation=Quaternion.LookRotation(forward,_up)*Quaternion.Euler(0,placement.Next(-15,15),placement.Next(-3,3));
                if(authored){height=Mathf.Clamp(landmark.scale,1,600);rotation=Quaternion.LookRotation(forward,_up);}
                if(airborne)
                {
                    var orientation=new RockRandom(unchecked(profile.motionSeed+index*486187739));
                    Vector3 uprightForward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
                    if(uprightForward.sqrMagnitude<.01f)uprightForward=Vector3.forward;
                    rotation=Quaternion.LookRotation(uprightForward,Vector3.up)*Quaternion.Euler(0,orientation.Next(-180,180),0);
                }
                Vector3 position=default;Bounds envelope=default;bool accepted=false;
                for(int attempt=0;attempt<24;attempt++)
                {
                    float z=airborne?Mathf.Lerp(140,profile.valleyLength*.90f,t)+placement.Next(-55,55):
                        -130+profile.valleyLength*t+placement.Next(-35,35);
                    float x=side*(airborne?placement.Next(profile.valleyWidth*.08f,profile.valleyWidth*.35f):
                        profile.valleyWidth*.5f+z*.10f+Mathf.Sin(t*4+side)*65+placement.Next(-25,25));
                    float y=airborne?placement.Next(105,255):-profile.valleyFloorBelowCenter;
                    if(rear)
                    {
                        // A longitudinal continuation behind Main, not a surrounding ring.
                        // Independent streams retain every existing combat transform.
                        z=(airborne?-650f:rearIndex==0?-380f:rearIndex==1?-600f:-850f)+placement.Next(-25,25);
                        x=(airborne?20f:side*(rearIndex==0?330f:rearIndex==1?360f:400f))+placement.Next(-20,20);
                        if(airborne)y=55f+placement.Next(-15,15);
                    }
                    if(authored)
                    {
                        x=landmark.position.x;y=landmark.position.y;z=landmark.position.z;
                        if(attempt>0){x+=placement.Next(-20,20);z+=placement.Next(-20,20);}
                    }
                    position=center+right*x+forward*z+_up*y;
                    envelope=WorldBounds(mesh.bounds,Matrix4x4.TRS(position,rotation,Vector3.one*height));
                    if(airborne)
                    {
                        envelope=FloatingMotionEnvelope(mesh.bounds,height,position,rotation,
                            FloatingAmplitude(mesh.bounds.size.y*height,profile.levitationAmplitude),
                            profile.horizontalMotionFraction,Mathf.Max(profile.rockingDegrees.x,profile.rockingDegrees.y));
                    }
                    bool blocked=envelope.SqrDistance(center)<Mathf.Pow(profile.planetRadius+profile.arenaExclusionPadding,2);
                    if(airborne)foreach(Bounds prior in occupied)if(prior.Intersects(envelope)){blocked=true;break;}
                    if(profile.exclusionVolumes!=null)foreach(Bounds local in profile.exclusionVolumes)
                        if(envelope.Intersects(WorldBounds(local,Matrix4x4.TRS(center,Quaternion.LookRotation(forward,_up),Vector3.one)))){blocked=true;break;}
                    if(!blocked){accepted=true;break;}
                }
                if(!accepted){RejectedPlacements++;continue;}
                if(airborne)acceptedFloating++;else acceptedGround++;
                var pivot=new GameObject(authored?"View_"+landmark.name:(rear?"Rear_":"")+(airborne?"Island_":"ValleyGroup_")+index.ToString("00")).transform;
                pivot.SetParent(generatedRoot,false);pivot.SetPositionAndRotation(position,rotation);
                var high=new List<Renderer>();AddRenderer(pivot,"LOD0",mesh,Vector3.one*height,high);
                if(lowBank!=null && variant<lowBank.Length && lowBank[variant]!=null)
                {
                    var low=new List<Renderer>();AddRenderer(pivot,"LOD1",lowBank[variant],Vector3.one*height,low);
                    var lod=pivot.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.13f,high.ToArray()),new LOD(.003f,low.ToArray())});lod.RecalculateBounds();
                }
                var motion=new RockRandom(unchecked(profile.motionSeed+index*19349663));
                drift.Add(new Drift{target=pivot,origin=position,rotation=rotation,tangent=right,bitangent=forward,
                    phase=motion.Next(0,1),period=motion.Next(profile.levitationPeriod.x,profile.levitationPeriod.y),
                    amplitude=airborne?FloatingAmplitude(mesh.bounds.size.y*height,profile.levitationAmplitude):0,
                    angle=airborne?motion.Next(profile.rockingDegrees.x,profile.rockingDegrees.y):0,
                    horizontalFraction=airborne?profile.horizontalMotionFraction:0,
                    centerOffset=airborne?mesh.bounds.center*height:Vector3.zero,
                    spinAxis=Vector3.up,
                    spinDegreesPerSecond=airborne?motion.Next(.035f,.075f)*(index%2==0?1:-1):0});
                if(airborne)
                {
                    occupied.Add(envelope);
                    if(authored&&profile.hardPolishSupplements&&(landmark.name.StartsWith("MainIsland")||landmark.name.StartsWith("CombatIsland")))
                        satellites.Add(new SatelliteRequest{index=index,position=position,rotation=rotation,scale=height,high=mesh,
                            low=lowBank!=null&&variant<lowBank.Length?lowBank[variant]:null,envelope=envelope});
                }
                else
                {
                    if(!componentCache.TryGetValue(mesh,out Bounds[] components))
                    {components=ConnectedComponentBounds(mesh);componentCache.Add(mesh,components);}
                    Matrix4x4 matrix=Matrix4x4.TRS(position,rotation,Vector3.one*height);
                    foreach(Bounds component in components)occupied.Add(WorldBounds(component,matrix));
                }
            }
            foreach(var request in satellites)
                AddSatellites(request.index,request.position,request.rotation,request.scale,request.high,request.low,
                    request.envelope,occupied,center,forward,right);
        }
        public static float FloatingAmplitude(float bodyHeight,Vector2 authoredRange)
        {
            float desired=Mathf.Clamp(bodyHeight*.015f,Mathf.Max(0,authoredRange.x),Mathf.Max(authoredRange.x,authoredRange.y));
            return Mathf.Min(desired,Mathf.Max(0,bodyHeight)*.02f);
        }
        public static Bounds FloatingMotionEnvelope(Bounds meshBounds,float scale,Vector3 position,Quaternion rotation,
            float amplitude,float horizontalFraction,float rockingDegrees)
        {
            Vector3 e=meshBounds.extents*scale;
            // Full yaw plus two bounded rock axes about the geometric center.
            float tiltPad=2f*e.magnitude*Mathf.Sin(Mathf.Min(90,Mathf.Abs(rockingDegrees)*2)*Mathf.Deg2Rad*.5f);
            float horizontalPad=Mathf.Abs(amplitude*horizontalFraction)*Mathf.Sqrt(2);
            float radius=Mathf.Sqrt(e.x*e.x+e.z*e.z)+tiltPad+horizontalPad;
            // Vertical levitation follows the authored arena up, which need not be
            // global Y. Include its full amplitude on every axis conservatively.
            return new Bounds(position+rotation*(meshBounds.center*scale),
                new Vector3(radius+Mathf.Abs(amplitude),e.y+tiltPad+Mathf.Abs(amplitude)+horizontalPad,radius+Mathf.Abs(amplitude))*2);
        }
        private void AddSatellites(int index,Vector3 origin,Quaternion rotation,float scale,Mesh highMesh,Mesh lowMesh,
            Bounds mainEnvelope,List<Bounds> occupied,Vector3 center,Vector3 forward,Vector3 right)
        {
            var random=new RockRandom(unchecked(profile.geometrySeed+index*92821));
            int count=Mathf.Clamp(profile.satellitesPerMainIsland,2,5);
            for(int satellite=0;satellite<count;satellite++)
            {
                float size=scale*(satellite==0?.18f:random.Next(.07f,.12f));
                float side=(index%2==0?-1:1)*(satellite==1?-1:1);
                Vector3 position=origin+right*(side*(mainEnvelope.extents.x+size*.7f+8+satellite*5))+
                    _up*(-scale*(.055f+satellite*.024f))+forward*random.Next(-12,12);
                float amplitude=FloatingAmplitude(highMesh.bounds.size.y*size,profile.levitationAmplitude);
                Bounds envelope=FloatingMotionEnvelope(highMesh.bounds,size,position,rotation,amplitude,profile.horizontalMotionFraction,profile.rockingDegrees.y);
                bool blocked=envelope.SqrDistance(center)<Mathf.Pow(profile.planetRadius+profile.arenaExclusionPadding,2);
                foreach(Bounds prior in occupied)if(prior.Intersects(envelope)){blocked=true;break;}
                if(profile.exclusionVolumes!=null)foreach(Bounds box in profile.exclusionVolumes)
                    if(envelope.Intersects(WorldBounds(box,Matrix4x4.TRS(center,Quaternion.LookRotation(forward,_up),Vector3.one))))blocked=true;
                if(blocked){RejectedPlacements++;continue;}
                var pivot=new GameObject("Satellite_"+index+"_"+satellite).transform;
                pivot.SetParent(generatedRoot,false);pivot.SetPositionAndRotation(position,rotation);
                var high=new List<Renderer>();AddRenderer(pivot,"LOD0",highMesh,Vector3.one*size,high);
                if(lowMesh!=null)
                {
                    var low=new List<Renderer>();AddRenderer(pivot,"LOD1",lowMesh,Vector3.one*size,low);
                    var lod=pivot.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.04f,high.ToArray()),new LOD(.002f,low.ToArray())});lod.RecalculateBounds();
                }
                var motion=new RockRandom(unchecked(profile.motionSeed+index*19349663+satellite*73856093));
                drift.Add(new Drift{target=pivot,origin=position,rotation=rotation,tangent=right,bitangent=forward,
                    phase=motion.Next(0,1),period=motion.Next(profile.levitationPeriod.x,profile.levitationPeriod.y),
                    amplitude=amplitude,angle=motion.Next(profile.rockingDegrees.x,profile.rockingDegrees.y),
                    horizontalFraction=profile.horizontalMotionFraction,centerOffset=highMesh.bounds.center*size});
                occupied.Add(envelope);
            }
        }
        // Ground libraries combine disconnected pillars. A whole-group AABB falsely fills
        // the air between them. Weld face-split vertices, then bound each connected solid.
        // Rebuild-only work; never reads mesh arrays or allocates during animation.
        private static Bounds[] ConnectedComponentBounds(Mesh mesh)
        {
            Vector3[] vertices=mesh.vertices;int[] triangles=mesh.triangles;
            var welded=new Dictionary<Vector3,int>();var parent=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                if(welded.TryGetValue(vertices[i],out int first))parent[i]=first;
                else{parent[i]=i;welded.Add(vertices[i],i);}
            }
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                int a=ComponentRoot(parent,triangles[i]);
                int b=ComponentRoot(parent,triangles[i+1]);
                int c=ComponentRoot(parent,triangles[i+2]);
                parent[b]=a;parent[c]=a;
            }
            var bounds=new Dictionary<int,Bounds>();
            for(int i=0;i<vertices.Length;i++)
            {
                int component=ComponentRoot(parent,i);
                if(bounds.TryGetValue(component,out Bounds value)){value.Encapsulate(vertices[i]);bounds[component]=value;}
                else bounds.Add(component,new Bounds(vertices[i],Vector3.zero));
            }
            var result=new Bounds[bounds.Count];bounds.Values.CopyTo(result,0);return result;
        }
        private static int ComponentRoot(int[] parent,int index)
        {
            while(parent[index]!=index){parent[index]=parent[parent[index]];index=parent[index];}
            return index;
        }
        private static Bounds WorldBounds(Bounds local,Matrix4x4 matrix)
        {
            var result=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            for(int i=0;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
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
            if(environmentPaused)return;
            time*=Mathf.Max(0,environmentSpeed);
            if(generatedRoot==null || double.IsNaN(time) || double.IsInfinity(time))return;
            Vector3 axis=_up;
            foreach(var d in drift)
            {
                if(d.target==null)continue;
                if(reducedMotion || (d.amplitude==0 && d.angle==0 && d.spinDegreesPerSecond==0))
                {
                    if(d.target.position!=d.origin || d.target.rotation!=d.rotation)
                        d.target.SetPositionAndRotation(d.origin,d.rotation);
                    continue;
                }
                double phase=(time/Math.Max(1,d.period)+d.phase)%1.0;float theta=(float)(phase*Math.PI*2);
                // Absolute baseline + sinusoid: no integration drift, no shared synchronous phase.
                Vector3 position=d.origin+axis*(Mathf.Sin(theta)*d.amplitude);
                if(d.horizontalFraction>0)
                    position+=d.tangent*(Mathf.Sin((float)(((time/(d.period*1.57)+d.phase*.61)%1.0)*Math.PI*2))*d.amplitude*d.horizontalFraction)+
                              d.bitangent*(Mathf.Sin((float)(((time/(d.period*1.83)+d.phase*.37)%1.0)*Math.PI*2))*d.amplitude*d.horizontalFraction);
                Quaternion rotation=d.rotation*Quaternion.Euler(Mathf.Sin((float)(((time/(d.period*1.37)+d.phase*.71)%1.0)*Math.PI*2))*d.angle,0,Mathf.Cos(theta)*d.angle);
                if(d.spinDegreesPerSecond!=0)
                    rotation=d.rotation*Quaternion.AngleAxis((float)((time*d.spinDegreesPerSecond)%360.0),d.spinAxis)*Quaternion.Inverse(d.rotation)*rotation;
                position+=d.rotation*d.centerOffset-rotation*d.centerOffset;
                if(d.target.position!=position || d.target.rotation!=rotation)d.target.SetPositionAndRotation(position,rotation);
            }
        }
    }
}
