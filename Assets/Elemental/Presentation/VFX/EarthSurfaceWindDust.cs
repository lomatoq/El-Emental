using System;
using Elemental.Presentation.UI;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Rendering;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    /// <summary>One bounded cosmetic layer: continuous ground drift and denser settled-stone wakes.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem))]
    public sealed class EarthSurfaceWindDust : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.VFX.SurfaceWindDust");
        [SerializeField] private EarthSurfaceWindDustProfile profile;
        [SerializeField] private Transform arenaAnchor, planet;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private FrontendFlowController frontend;
        private readonly Vector3[] stoneBases=new Vector3[96];
        private readonly UnityEngine.Object[] stoneOwners=new UnityEngine.Object[96];
        private readonly int[] neighbours=new int[96],nearest=new int[96];
        private readonly float[] densityWeights=new float[96];
        public int ClusteredStoneCandidates { get; private set; }
        public int GapEmitted { get; private set; }
        public float MeanStoneNeighbours { get; private set; }
        public float EffectiveStoneRate { get; private set; }
        private bool Reduced => profile.clusteredWisps && frontend!=null && frontend.Preferences.ReducedMotion;
        private ParticleSystem particles;
        private Mesh surfaceMesh;
        private MaterialPropertyBlock previousRendererProperties;
        private bool shortenedDepthFade;
        private readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[512];
        private readonly Vector3[] ground = new Vector3[512], normals = new Vector3[512];
        private readonly Vector3[] targetGround=new Vector3[512],targetNormals=new Vector3[512],baseSizes=new Vector3[512];
        private readonly Quaternion[] orientations=new Quaternion[512];
        private readonly float[] speedFactors=new float[512],liftAmounts=new float[512],phases=new float[512];
        private readonly bool[] retiring=new bool[512];
        private readonly float[] retirementAlpha=new float[512];
        private readonly uint[] particleIds=new uint[512];
        private uint birthSerial;
        private static readonly System.Collections.Generic.List<ParticleSystemVertexStream> WispStreams=new()
        {
            ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.AnimBlend,
            ParticleSystemVertexStream.StableRandomXYZ
        };
        private readonly bool[] occupied = new bool[512];
        private readonly Vector3[] wakeCenters = new Vector3[512];
        private readonly bool[] inWake = new bool[512];
        private readonly bool[] seamVolume = new bool[512];
        private readonly Collider[] overlaps = new Collider[512], stones = new Collider[96];
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private int stoneCount, scanCursor, probeCursor, appliedCapacity;
        private float nextScan, groundBudget, stoneBudget;
        private uint random = 0x6A09E667u;
        public int LiveParticles => particles != null ? particles.particleCount : 0;
        public int SettledStoneCandidates => stoneCount;
        public int GroundEmitted { get; private set; }
        public int StoneEmitted { get; private set; }
        public EarthSurfaceWindDustProfile Profile => profile;
        public Transform ArenaAnchor => arenaAnchor;
        public ParticleSystem System => particles;

        public void Configure(EarthSurfaceWindDustProfile tuning, Transform anchor, Transform planetCenter, LayerMask mask, Material material, FrontendFlowController flow=null)
        { profile = tuning; arenaAnchor = anchor; planet = planetCenter; surfaceMask = mask; dustMaterial = material; frontend=flow; if (Application.isPlaying) Initialize(); }
        private void Awake() => Initialize();
        private void Initialize()
        {
            particles = GetComponent<ParticleSystem>();
            if (profile == null || arenaAnchor == null || planet == null || dustMaterial == null)
            { Debug.LogError("Surface wind dust needs its profile, arena anchor, planet and existing dust material. Run Elemental > VFX > Install Surface Wind Dust.", this); enabled = false; return; }
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f; main.maxParticles = Mathf.Clamp(profile.maximumParticles, 32, 512); appliedCapacity = main.maxParticles;
            main.startSize3D=profile.clusteredWisps;main.startRotation3D=profile.clusteredWisps;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var collision = particles.collision; collision.enabled = false;
            var noise = particles.noise; noise.enabled = false;
            var trails = particles.trails; trails.enabled = false;
            var lights = particles.lights; lights.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.18f), new GradientAlphaKey(.65f,.7f), new GradientAlphaKey(0,1) });
            color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0,.65f,1,1.3f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial; renderer.renderMode = ParticleSystemRenderMode.Stretch;
            if(profile.clusteredWisps)
            {
                if(surfaceMesh==null)surfaceMesh=SurfaceDustMesh.Create();
                renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.alignment=ParticleSystemRenderSpace.World;renderer.mesh=surfaceMesh;
            }
            renderer.lengthScale = profile.clusteredWisps ? 2.1f : 1.7f; renderer.velocityScale = profile.clusteredWisps ? .08f : .18f; renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            EarthParticleSystemTuningApplier.UseMaterialDustColor(particles);
            if(profile.clusteredWisps)
            {
                // This low ambient renderer needs a shorter depth fade than metre-scale fracture smoke.
                if(!shortenedDepthFade){previousRendererProperties=new MaterialPropertyBlock();renderer.GetPropertyBlock(previousRendererProperties);}
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                properties.SetVector("_SoftParticleFadeParams",new Vector4(.015f,8f,0,0));
                // LightDustMote uses these scalar properties; the legacy URP vector alone is ignored.
                properties.SetFloat("_SoftParticleNearDistance",.015f);
                properties.SetFloat("_SoftParticleInvDistance",8f);
                properties.SetFloat("_SurfaceWisp",1f);
                properties.SetVector("_WispUp",(arenaAnchor.position-planet.position).normalized);
                renderer.SetActiveVertexStreams(WispStreams);


                renderer.SetPropertyBlock(properties);shortenedDepthFade=true;
            }
            else if(shortenedDepthFade){renderer.SetPropertyBlock(previousRendererProperties);shortenedDepthFade=false;}
            groundBudget=stoneBudget=0;GroundEmitted=StoneEmitted=GapEmitted=0;
            Array.Clear(occupied, 0, occupied.Length); nextScan = 0f;
            if (gameObject.activeInHierarchy && enabled) particles.Play(true);
        }
        private void OnEnable() { if (particles != null && profile != null) particles.Play(true); }
        private void OnDisable() { if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        private void OnDestroy(){if(surfaceMesh!=null)Destroy(surfaceMesh);}
        private void LateUpdate()
        {
            if (profile == null || particles == null || arenaAnchor == null || planet == null) return;
            if (!profile.enabledEffect) { if (particles.particleCount > 0) particles.Clear(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); if (dt <= 0f) return;
            using (Marker.Auto())
            {
                int capacity = Mathf.Clamp(profile.maximumParticles, 32, 512);
                if (capacity != appliedCapacity) { var main = particles.main; main.maxParticles = capacity; appliedCapacity = capacity; }
                if (Time.time >= nextScan) { nextScan = Time.time + Mathf.Max(.2f, profile.stoneScanSeconds); ScanStones(); }
                int count = particles.GetParticles(buffer);
                Array.Clear(occupied, 0, occupied.Length);
                int probes = Mathf.Min(8, count);
                for (int i = 0; i < count; i++)
                {
                    int slot = (int)((buffer[i].randomSeed-1u)&511u);
                    if (buffer[i].randomSeed!=particleIds[slot]) { buffer[i].remainingLifetime = 0f; continue; }
                    occupied[slot] = true;
                    int relative = (i - probeCursor + count) % Mathf.Max(1, count);
                    if (relative < probes)
                    {
                        Vector3 radialUp = (buffer[i].position - planet.position).normalized;
                        if (TryGround(buffer[i].position + radialUp * .75f, radialUp, 1.6f, null, out RaycastHit support))
                        {
                            // A wall/stone top is an obstacle, not a new floor to
                            // pull the wisp onto. Fade naturally without jumping
                            // lifetime (which also jumped the flipbook frame).
                            if(Mathf.Abs(Vector3.Dot(support.point-ground[slot],radialUp))>.55f)retiring[slot]=true;
                            else{targetGround[slot] = support.point; targetNormals[slot] = support.normal;}
                        }
                        else retiring[slot]=true;
                    }
                    if(retiring[slot])
                    {
                        retirementAlpha[slot]=Mathf.Max(0,retirementAlpha[slot]-dt/.3f);
                        var color=buffer[i].startColor;color.a=(byte)Mathf.RoundToInt(Mathf.Clamp01(profile.opacity*retirementAlpha[slot])*255);
                        buffer[i].startColor=color;
                        if(retirementAlpha[slot]<=0){buffer[i].remainingLifetime=0;continue;}
                    }
                    float follow=1-Mathf.Exp(-7*dt);
                    ground[slot]=Vector3.Lerp(ground[slot],targetGround[slot],follow);
                    normals[slot]=Vector3.Slerp(normals[slot],targetNormals[slot],follow).normalized;
                    Vector3 normal = normals[slot];
                    float height = Vector3.Dot(buffer[i].position - ground[slot], normal);
                    float age=buffer[i].startLifetime-buffer[i].remainingLifetime;
                    float life=Mathf.Clamp01(age/Mathf.Max(.01f,buffer[i].startLifetime));
                    float billow=Mathf.Sin(life*Mathf.PI);
                    float targetHeight=Mathf.Clamp(profile.hoverHeight,.02f,.4f)*.55f+
                        liftAmounts[slot]*billow+billow*.035f*Mathf.Sin(phases[slot]+age*1.8f);
                    buffer[i].position += normal * ((targetHeight - height)*(1-Mathf.Exp(-18*dt)));
                    Vector3 desired=WindAt(buffer[i].position, normal, slot)*speedFactors[slot];
                    buffer[i].velocity=Vector3.Lerp(buffer[i].velocity,desired,1-Mathf.Exp(-4*dt));
                    if(profile.clusteredWisps)
                    {
                        orientations[slot]=Quaternion.Slerp(orientations[slot],Quaternion.LookRotation(normal,buffer[i].velocity.normalized),1-Mathf.Exp(-3*dt));
                        buffer[i].rotation3D=orientations[slot].eulerAngles;
                        float breathing=1+.14f*Mathf.Sin(phases[slot]+age*1.6f)*billow;
                        buffer[i].startSize3D=Vector3.Scale(baseSizes[slot],new Vector3(breathing,1/breathing,.65f+.5f*billow));
                    }
                }
                probeCursor = count > 0 ? (probeCursor + probes) % count : 0;
                particles.SetParticles(buffer, count);
                float rateScale=Reduced?Mathf.Clamp01(profile.reducedMotionRate):1;
                float densityScale=profile.clusteredWisps?Mathf.Lerp(profile.isolatedStoneWeight,profile.clusteredRateMultiplier,Mathf.Clamp01(MeanStoneNeighbours/Mathf.Max(1,profile.saturatedNeighbours))):1;
                EffectiveStoneRate=stoneCount>0?Mathf.Clamp(profile.stoneRate*densityScale,0,128)*rateScale:0;
                groundBudget = Mathf.Min(8f, groundBudget + Mathf.Clamp(profile.groundRate,0,48)*rateScale * dt);
                stoneBudget = Mathf.Min(8f, stoneBudget + EffectiveStoneRate * dt);
                int remaining = capacity - count;
                for (int i = 0; i < 8 && remaining > 0; i++)
                {
                    bool nearStone = stoneBudget >= 1f && (i % 2 == 0 || groundBudget < 1f);
                    if (!nearStone && groundBudget < 1f) break;
                    if (nearStone) stoneBudget -= 1f; else groundBudget -= 1f;
                    if (Emit(nearStone)) remaining--;
                }
            }
        }
        private void ScanStones()
        {
            stoneCount = 0;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(arenaAnchor.position, Mathf.Clamp(profile.areaRadius,2,30), overlaps, surfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && stoneCount < stones.Length; i++)
            {
                Collider value = overlaps[i]; if (value == null || !value.enabled) continue;
                Rigidbody body = value.attachedRigidbody;
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > profile.maximumSettledSpeed * profile.maximumSettledSpeed) continue;
                UnityEngine.Object owner=value.GetComponentInParent<EarthDestructibleDecorRock>();
                if(owner==null)owner=value.GetComponentInParent<EarthRockDebris>();
                if(owner==null)owner=value.GetComponentInParent<EarthArenaPiece>();
                if(owner==null)owner=value.GetComponentInParent<EarthFragment>();
                if(owner==null)owner=value.GetComponentInParent<EarthWallPiece>();
                if(owner==null)continue;
                bool duplicate=false;if(profile.clusteredWisps)for(int j=0;j<stoneCount;j++)if(stoneOwners[j]==owner){duplicate=true;break;}
                if(duplicate)continue;
                stones[stoneCount]=value;stoneOwners[stoneCount]=owner;
                var bounds=value.bounds;var up=(bounds.center-planet.position).normalized;
                float verticalExtent=Mathf.Abs(up.x)*bounds.extents.x+Mathf.Abs(up.y)*bounds.extents.y+Mathf.Abs(up.z)*bounds.extents.z;
                stoneBases[stoneCount]=bounds.center-up*verticalExtent;stoneCount++;
            }
            MeanStoneNeighbours=0;ClusteredStoneCandidates=0;
            if(profile.clusteredWisps)
            {
                EarthGroundDustDensity.Evaluate(stoneBases,stoneCount,(arenaAnchor.position-planet.position).normalized,profile.clusterRadius,profile.isolatedStoneWeight,profile.saturatedNeighbours,neighbours,nearest,densityWeights,out float mean);
                MeanStoneNeighbours=mean;for(int i=0;i<stoneCount;i++)if(neighbours[i]>0)ClusteredStoneCandidates++;
            }
        }
        private bool Emit(bool nearStone)
        {
            Vector3 point = arenaAnchor.position;
            Vector3 up = (point - planet.position).normalized;
            Vector3 wind = EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind, up, 1f);
            Vector3 side = Vector3.Cross(up, wind);
            Collider ignore = null;bool gap=false;Vector3 wakeCenter=point;
            if (nearStone)
            {
                if (stoneCount == 0) return false;
                int selected=profile.clusteredWisps?EarthGroundDustDensity.Select(densityWeights,stoneCount,Next()):(scanCursor++)%stoneCount;
                if(selected<0)return false;ignore = stones[selected]; if (ignore == null || !ignore.enabled || !ignore.gameObject.activeInHierarchy) return false;
                Rigidbody body = ignore.attachedRigidbody;
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > profile.maximumSettledSpeed * profile.maximumSettledSpeed) return false;
                Bounds bounds = ignore.bounds;
                wakeCenter=stoneBases[selected];
                float extent = Mathf.Abs(wind.x) * bounds.extents.x + Mathf.Abs(wind.y) * bounds.extents.y + Mathf.Abs(wind.z) * bounds.extents.z;
                point = bounds.center + wind * (extent + profile.stoneMargin) + side * ((Next()-.5f) * .8f);
                if(profile.clusteredWisps)
                {
                    point=stoneBases[selected]+wind*(extent+profile.stoneMargin)+side*((Next()-.5f)*.8f);
                    // Cover the actual perimeter, including the windward seam; avoid
                    // placing every birth half a metre beyond the leeward edge.
                    float edgeAngle=Next()*Mathf.PI*2;
                    Vector3 edgeDirection=wind*Mathf.Cos(edgeAngle)+side*Mathf.Sin(edgeAngle);
                    float edgeExtent=Mathf.Abs(edgeDirection.x)*bounds.extents.x+Mathf.Abs(edgeDirection.y)*bounds.extents.y+Mathf.Abs(edgeDirection.z)*bounds.extents.z;
                    point=stoneBases[selected]+edgeDirection*(edgeExtent+profile.stoneMargin);
                    int partner=nearest[selected];
                    if(partner>=0 && Next()<profile.gapEmissionChance && stones[partner]!=null && stones[partner].enabled)
                    {
                        var other=stones[partner];var otherBody=other.attachedRigidbody;
                        if(otherBody==null || otherBody.isKinematic || otherBody.linearVelocity.sqrMagnitude<=profile.maximumSettledSpeed*profile.maximumSettledSpeed)
                        {
                            Vector3 a=bounds.ClosestPoint(stoneBases[partner]),b=other.bounds.ClosestPoint(stoneBases[selected]);
                            if((b-a).sqrMagnitude>.04f){point=Vector3.Lerp(a,b,.3f+.4f*Next())+side*((Next()-.5f)*.3f);gap=true;}
                        }
                    }
                }
            }
            else
            {
                float angle = Next() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Next()) * Mathf.Clamp(profile.areaRadius,2,30);
                point += (wind * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * radius;
            }
            up = (point - planet.position).normalized;
            // The visible curl lives just behind the obstacle edge. Centering it
            // inside a large collider left almost no curl at the emitted particle.
            if(nearStone)wakeCenter=point-wind*.6f;
            if (!TryGround(point + up * 3f, up, 7f, ignore, out RaycastHit hit)) return false;
            int slot = 0; while (slot < appliedCapacity && occupied[slot]) slot++;
            if (slot >= appliedCapacity) return false;
            occupied[slot] = true; ground[slot] = hit.point; normals[slot] = hit.normal;
            targetGround[slot]=hit.point;targetNormals[slot]=hit.normal;
            retiring[slot]=false;retirementAlpha[slot]=1;
            // Stable shader randomness and diagnostics identify a birth, not a
            // reusable slot. Keep the slot in the low nine bits for O(1) lookup.
            particleIds[slot]=((++birthSerial<<9)|(uint)slot)+1u;
            wakeCenters[slot]=wakeCenter;inWake[slot]=nearStone;
            seamVolume[slot]=nearStone&&Next()<profile.seamVolumeFraction;
            speedFactors[slot]=Mathf.Lerp(.45f,1.5f,Next());phases[slot]=Next()*Mathf.PI*2;
            liftAmounts[slot]=Mathf.Lerp(.035f,seamVolume[slot]?.48f:.22f,Next());
            float size = Mathf.Clamp(Mathf.Lerp(profile.sizeMetres.x, profile.sizeMetres.y,Next())*(profile.clusteredWisps?Mathf.Lerp(.5f,1.25f,Next()):1),.1f,4.5f);
            var emit = new ParticleSystem.EmitParams
            {
                position = hit.point + hit.normal * (Mathf.Clamp(profile.hoverHeight,.02f,.4f)*.55f),
                velocity = WindAt(hit.point, hit.normal, slot)*speedFactors[slot],
                startSize = size, startLifetime = Mathf.Clamp(Mathf.Lerp(profile.lifetimeSeconds.x,profile.lifetimeSeconds.y,Next()),.5f,6f),
                startColor = new Color(1,1,1,Mathf.Clamp(profile.opacity,.03f,.8f)), randomSeed = particleIds[slot],
                rotation3D = Vector3.zero
            };
            if(profile.clusteredWisps)
            {
                // A broad ground-parallel wisp stays above the surface. Tall camera-facing cards bury most alpha below the floor.
                baseSizes[slot]=new Vector3(size*Mathf.Lerp(.35f,1.1f,Next()),size*Mathf.Lerp(1.2f,2.1f,Next()),size);
                emit.startSize3D=baseSizes[slot];
                orientations[slot]=Quaternion.LookRotation(hit.normal,EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind,hit.normal,1));
                emit.rotation3D=orientations[slot].eulerAngles;
            }
            particles.Emit(emit,1); if(gap)GapEmitted++; if (nearStone) StoneEmitted++; else GroundEmitted++;
            return true;
        }
        private bool TryGround(Vector3 origin, Vector3 up, float distance, Collider ignore, out RaycastHit support)
        {
            support = default; float nearest = float.PositiveInfinity;
            int count = UnityEngine.Physics.RaycastNonAlloc(origin, -up, hits, distance, surfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i]; if (hit.collider == null || hit.collider == ignore || hit.distance >= nearest || Vector3.Dot(hit.normal,up) < .6f) continue;
                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
                nearest = hit.distance; support = hit;
            }
            return nearest < float.PositiveInfinity;
        }
        private float Next() { random = random * 1664525u + 1013904223u; return (random & 0xffffffu) / 16777216f; }
        private Vector3 WindAt(Vector3 position, Vector3 normal, int slot)
        {
            if (Reduced)
                return EarthSurfaceWindPolicy.TangentVelocity(profile.worldWind, normal,
                    profile.speed * Mathf.Clamp01(profile.reducedMotionSpeed));
            Vector3 velocity = EarthSurfaceWindPolicy.GustVelocity(profile.worldWind, normal,
                position - arenaAnchor.position, Time.time, profile.speed, profile.gustStrength,
                profile.gustPeriodSeconds, profile.gustWavelengthMetres, profile.turbulenceStrength);
            return inWake[slot] ? (Vector3)EarthSurfaceWindPolicy.StoneWakeVelocity(velocity,normal,
                position-wakeCenters[slot],profile.wakeRadius,profile.wakeStrength,Time.time) : velocity;
        }
    }
}
