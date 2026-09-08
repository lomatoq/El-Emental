from pathlib import Path
lane=Path('Tools/FireIntegration/Staged/ClusterGroundDust');ui=lane/'after/Assets/Elemental/Presentation/VFX'
p=ui/'EarthSurfaceWindDustProfile.cs';s=p.read_text(encoding='utf-8-sig').replace('public bool enabledEffect = true;','''public bool enabledEffect = true;
        [Header("Optional quiet clustered ground wisps")]
        public bool clusteredWisps;
        [Range(.5f,8f)] public float clusterRadius=3.5f;
        [Range(1,12)] public int saturatedNeighbours=4;
        [Range(.05f,1f)] public float isolatedStoneWeight=.2f;
        [Range(1f,2f)] public float clusteredRateMultiplier=1.6f;
        [Range(0f,1f)] public float gapEmissionChance=.65f;
        [Range(0f,1f)] public float reducedMotionRate=.3f;
        [Range(0f,1f)] public float reducedMotionSpeed=.35f;''');p.write_text(s,encoding='utf-8')
p=ui/'EarthSurfaceWindDust.cs';s=p.read_text(encoding='utf-8-sig').replace('using System;','using System;\nusing Elemental.Presentation.UI;')
s=s.replace('[SerializeField] private Material dustMaterial;','''[SerializeField] private Material dustMaterial;
        [SerializeField] private FrontendFlowController frontend;
        private readonly Vector3[] stoneBases=new Vector3[96];
        private readonly UnityEngine.Object[] stoneOwners=new UnityEngine.Object[96];
        private readonly int[] neighbours=new int[96],nearest=new int[96];
        private readonly float[] densityWeights=new float[96];
        public int ClusteredStoneCandidates { get; private set; }
        public int GapEmitted { get; private set; }
        public float MeanStoneNeighbours { get; private set; }
        public float EffectiveStoneRate { get; private set; }
        private bool Reduced => profile.clusteredWisps && frontend!=null && frontend.Preferences.ReducedMotion;''')
s=s.replace('LayerMask mask, Material material)','LayerMask mask, Material material, FrontendFlowController flow=null)').replace('dustMaterial = material; if (Application.isPlaying)','dustMaterial = material; frontend=flow; if (Application.isPlaying)')
s=s.replace('renderer.lengthScale = 1.7f; renderer.velocityScale = .18f;', 'renderer.lengthScale = profile.clusteredWisps ? 2.1f : 1.7f; renderer.velocityScale = profile.clusteredWisps ? .08f : .18f;')
s=s.replace('EarthParticleSystemTuningApplier.UseMaterialDustColor(particles);','''EarthParticleSystemTuningApplier.UseMaterialDustColor(particles);
            if(profile.clusteredWisps)
            {
                // This low ambient renderer needs a shorter depth fade than metre-scale fracture smoke.
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                properties.SetVector("_SoftParticleFadeParams",new Vector4(.025f,1f/.475f,0,0));renderer.SetPropertyBlock(properties);
            }
            groundBudget=stoneBudget=0;GroundEmitted=StoneEmitted=GapEmitted=0;''')
s=s.replace('float gust = .9f + .1f * Mathf.Sin(Time.time * .8f + slot * .41f);','float gust = Reduced ? 1 : .9f + .1f * Mathf.Sin(Time.time * .8f + slot * .41f);').replace('profile.speed * gust);','profile.speed * gust * (Reduced ? profile.reducedMotionSpeed : 1));')
s=s.replace('groundBudget = Mathf.Min(4f, groundBudget + Mathf.Clamp(profile.groundRate,0,48) * dt);\n                stoneBudget = Mathf.Min(4f, stoneBudget + (stoneCount > 0 ? Mathf.Clamp(profile.stoneRate,0,48) : 0f) * dt);','''float rateScale=Reduced?profile.reducedMotionRate:1;
                float densityScale=profile.clusteredWisps?Mathf.Lerp(profile.isolatedStoneWeight,profile.clusteredRateMultiplier,Mathf.Clamp01(MeanStoneNeighbours/Mathf.Max(1,profile.saturatedNeighbours))):1;
                EffectiveStoneRate=stoneCount>0?Mathf.Clamp(profile.stoneRate*densityScale,0,48)*rateScale:0;
                groundBudget = Mathf.Min(4f, groundBudget + Mathf.Clamp(profile.groundRate,0,48)*rateScale * dt);
                stoneBudget = Mathf.Min(4f, stoneBudget + EffectiveStoneRate * dt);''')
old='''                bool rock = value.GetComponentInParent<EarthDestructibleDecorRock>() != null ||
                    value.GetComponentInParent<EarthRockDebris>() != null || value.GetComponentInParent<EarthArenaPiece>() != null ||
                    value.GetComponentInParent<EarthFragment>() != null || value.GetComponentInParent<EarthWallPiece>() != null;
                if (rock) stones[stoneCount++] = value;'''
new='''                UnityEngine.Object owner=value.GetComponentInParent<EarthDestructibleDecorRock>();
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
                stoneBases[stoneCount]=bounds.center-up*verticalExtent;stoneCount++;'''
assert old in s;s=s.replace(old,new)
s=s.replace('''            }
        }
        private bool Emit(bool nearStone)''','''            }
            MeanStoneNeighbours=0;ClusteredStoneCandidates=0;
            if(profile.clusteredWisps)
            {
                EarthGroundDustDensity.Evaluate(stoneBases,stoneCount,(arenaAnchor.position-planet.position).normalized,profile.clusterRadius,profile.isolatedStoneWeight,profile.saturatedNeighbours,neighbours,nearest,densityWeights,out float mean);
                MeanStoneNeighbours=mean;for(int i=0;i<stoneCount;i++)if(neighbours[i]>0)ClusteredStoneCandidates++;
            }
        }
        private bool Emit(bool nearStone)''')
s=s.replace('Collider ignore = null;','Collider ignore = null;bool gap=false;')
s=s.replace('ignore = stones[(scanCursor++) % stoneCount];','int selected=profile.clusteredWisps?EarthGroundDustDensity.Select(densityWeights,stoneCount,Next()):(scanCursor++)%stoneCount;\n                if(selected<0)return false;ignore = stones[selected];')
s=s.replace('point = bounds.center + wind * (extent + profile.stoneMargin) + side * ((Next()-.5f) * .8f);','''point = bounds.center + wind * (extent + profile.stoneMargin) + side * ((Next()-.5f) * .8f);
                if(profile.clusteredWisps)
                {
                    point=stoneBases[selected]+wind*(extent+profile.stoneMargin)+side*((Next()-.5f)*.8f);
                    int partner=nearest[selected];
                    if(partner>=0 && Next()<profile.gapEmissionChance && stones[partner]!=null && stones[partner].enabled)
                    {
                        var other=stones[partner];var otherBody=other.attachedRigidbody;
                        if(otherBody==null || otherBody.isKinematic || otherBody.linearVelocity.sqrMagnitude<=profile.maximumSettledSpeed*profile.maximumSettledSpeed)
                        {
                            Vector3 a=bounds.ClosestPoint(stoneBases[partner]),b=other.bounds.ClosestPoint(stoneBases[selected]);
                            point=Vector3.Lerp(a,b,.3f+.4f*Next())+side*((Next()-.5f)*.3f);gap=true;
                        }
                    }
                }''')
s=s.replace('profile.sizeMetres.y, Next())','profile.sizeMetres.y, profile.clusteredWisps?Next()*Next():Next())')
s=s.replace('profile.worldWind, hit.normal, profile.speed)','profile.worldWind, hit.normal, profile.speed * (Reduced?profile.reducedMotionSpeed:1))')
s=s.replace('particles.Emit(emit,1); if (nearStone)','particles.Emit(emit,1); if(gap)GapEmitted++; if (nearStone)')
p.write_text(s,encoding='utf-8')
p=lane/'compile_review.py';p.write_text(Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity/compile_review.py').read_text(encoding='utf-8').replace('ReferenceSpriteFidelity','ClusterGroundDust'),encoding='utf-8')
