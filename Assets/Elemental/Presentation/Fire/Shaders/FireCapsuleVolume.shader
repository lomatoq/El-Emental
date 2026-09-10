// Local absorption/emission integral following GPU Gems 3 chapter 30.3.
// Artist-authored analytic 3D density, not a grid fluid simulation or stacked sprite sheets.
Shader "Elemental/Fire/CapsuleVolumePrototype"
{
    Properties
    {
        _MacroShape("Width scale / root opening / taper start / tongue spread",Vector)=(1,.13,.48,1)
        _ShapeSpeed("Large shape motion speed",Range(.4,4))=1.8
        [HDR] _VolumeEdge("Cool edge",Color)=(.78,.065,.004,1)
        [HDR] _VolumeBody("Orange body",Color)=(1.55,.34,.014,1)
        [HDR] _VolumeCore("Short gold root",Color)=(2.3,1.5,.48,1)
        _Absorption("Absorption per metre",Range(.5,6))=3.8
        _VolumeSteps("Maximum local ray steps",Range(32,48))=40
        _VolumeDebug("QA 0 radiance, 1 integrated opacity, 2 ray interval",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-2" "RenderType"="Transparent" }
        Pass
        {
            Name "LocalCapsuleVolume"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            Cull Front ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _VolumeOrigin,_VolumeAxis,_VolumeSide,_VolumeUp;
            float4 _VolumeExtent; // transverse half-box x/y, finite source length, broad density radius
            float4 _VolumeState; // canonical scaled time, lifecycle opacity, persistent phase, energy density
            float4 _MacroShape,_VolumeEdge,_VolumeBody,_VolumeCore,_VolumeTiming;
            float _VolumeSteps,_VolumeDebug,_ShapeSpeed,_Absorption;
            CBUFFER_END
            int _ContactCount;
            float4 _ContactPointRadius[8],_ContactNormalSkin[8];
            float _EarthSeismicVision;
            struct Attributes { float3 positionWS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0; };
            Varyings Vert(Attributes v){Varyings o;o.positionWS=v.positionWS;o.positionCS=TransformWorldToHClip(v.positionWS);return o;}
            float Hash(float3 p)
            {
                p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);
            }
            float Noise(float3 p)
            {
                float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            float3 LocalPoint(float3 p)
            {float3 q=p-_VolumeOrigin.xyz;return float3(dot(q,_VolumeSide.xyz),dot(q,_VolumeUp.xyz),dot(q,_VolumeAxis.xyz));}
            float3 LocalDirection(float3 d)
            {return float3(dot(d,_VolumeSide.xyz),dot(d,_VolumeUp.xyz),dot(d,_VolumeAxis.xyz));}
            float2 BoxInterval(float3 origin,float3 direction)
            {
                float3 safe=lerp(-1,1,step(0,direction))*max(abs(direction),1e-6);
                float3 a=(float3(-_VolumeExtent.xy,0)-origin)/safe;
                float3 b=(float3(_VolumeExtent.xy,_VolumeExtent.z)-origin)/safe;
                float3 low=min(a,b),high=max(a,b);
                return float2(max(max(low.x,low.y),low.z),min(min(high.x,high.y),high.z));
            }
            float Density(float3 local,float3 world,out float heat)
            {
                float u=local.z/max(_VolumeExtent.z,.005);
                heat=0;if(u<=0||u>=1)return 0;
                // Finite surface discs agree with existing snapshot clipping; no invented infinite walls.
                [loop]for(int c=0;c<min(_ContactCount,8);c++)
                {
                    if(_ContactPointRadius[c].w<=0)continue;
                    float3 n=normalize(_ContactNormalSkin[c].xyz),q=world-_ContactPointRadius[c].xyz;
                    float plane=dot(q,n);float3 lateral=q-plane*n;
                    if(dot(lateral,lateral)<_ContactPointRadius[c].w*_ContactPointRadius[c].w&&plane<_ContactNormalSkin[c].w)return 0;
                }
                float radius=_VolumeExtent.w*_MacroShape.x,phase=_VolumeState.z;
                float elapsed=_VolumeTiming.x,speed=max(1,_VolumeTiming.y),stopAt=_VolumeTiming.z;
                float flowTime=local.z-elapsed*speed;
                float broad=Noise(float3(local.xy*1.3,flowTime*.65)+phase);
                // Four born, stretched and cooled parcels share a rooted stream. Older
                // parcels cool fully before their analytic slot is reused.
                float spawnTime=stopAt>=0?min(elapsed,stopAt):elapsed;
                float newest=floor(spawnTime/.20);
                float field=-1000, hottest=0;
                [unroll]for(int lobe=0;lobe<4;lobe++)
                {
                    float serial=newest-lobe;
                    if(serial<0)continue;
                    float age=elapsed-serial*.20;
                    float seed=Hash(float3(serial,phase,17));
                    float centerZ=age*speed-.40;
                    float stretch=lerp(.48,2.10,smoothstep(0,.48,age))*(.91+.18*seed);
                    float coolingStart=clamp(_MacroShape.z,.25,.60);
                    // Four slots recycle at age .80. Fully extinguish by .78
                    // regardless of source speed, including nonproduction slow QA nodes.
                    float life=1-smoothstep(coolingStart,min(coolingStart+.35,.78),age);
                    float size=radius*smoothstep(0,.08,age)*life*(.74+.32*seed);
                    if(size<.002)continue;
                    float bend=smoothstep(.06,.48,age)*_MacroShape.w;
                    float2 center=radius*bend*float2(sin(seed*6.28+age*_ShapeSpeed)*.57,
                        .22+sin(seed*4.1+age*2.0)*.28);
                    float along=(local.z-centerZ)/stretch;
                    float taper=pow(saturate(1-along*along),.65);
                    float2 q=local.xy-center;
                    // Shedding comes from cooled rear/front taper of each moving lobe;
                    // the deformation remains smooth in three dimensions.
                    float parcel=size*taper*(.84+.26*broad)-length(q);
                    if(abs(along)>=1)parcel=-1000;
                    field=max(field,parcel);
                    hottest=max(hottest,smoothstep(.20,.78,saturate(parcel/max(size,.02)))*(1-smoothstep(.08,.42,age)));
                }
                // A short palm bridge is the only stationary source shape. It closes
                // from the palm after release instead of fading the entire8m body at once.
                float releaseFront=stopAt>=0?(elapsed-stopAt)*speed:0;
                float sourceLength=min(_VolumeExtent.z,1.05);
                float opening=smoothstep(0,max(.04,_MacroShape.y),u);
                float rootWidth=radius*lerp(.14,.47,opening)*(1-smoothstep(.20,sourceLength,local.z));
                if(local.z<sourceLength&&rootWidth>.002)field=max(field,rootWidth-length(local.xy));
                float edge=.055*radius+.012;
                float density=smoothstep(-edge,edge*2,field);
                heat=hottest*(1-smoothstep(.22,.60,u));
                // A bounded warm fan on the near side of a real finite contact disc.
                // It adds no burning-rock state, obstacle simulation or gameplay damage.
                [loop]for(int c=0;c<min(_ContactCount,8);c++)
                {
                    float4 patch=_ContactPointRadius[c];if(patch.w<=0)continue;
                    float3 n=normalize(_ContactNormalSkin[c].xyz);
                    float3 endWS=_VolumeOrigin.xyz+_VolumeAxis.xyz*_VolumeExtent.z;
                    float front=dot(endWS-patch.xyz,n);
                    if(front<patch.w*-0.1||front>radius*1.8)continue;
                    float3 q=world-endWS;float axial=dot(q,n);
                    float3 lateral=q-n*axial;
                    float fanRadius=min(patch.w*.80,radius*(.82+.20*broad));
                    float reach=smoothstep(_VolumeExtent.z/speed-.08,_VolumeExtent.z/speed+.03,elapsed);
                    float fan=saturate(1-length(lateral)/max(.03,fanRadius))*
                        (1-smoothstep(.035,.19,abs(axial)))*reach;
                    density=max(density,fan*.80);
                }
                density*=smoothstep(releaseFront-.05,releaseFront+.12,local.z);
                return density*smoothstep(0,.016,u)*(1-smoothstep(.975,1,u))*_VolumeState.y*_VolumeState.w;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float3 origin=GetCameraPositionWS();
                float3 direction=normalize(input.positionWS-origin);
                if(unity_OrthoParams.w>.5)
                {
                    direction=-UNITY_MATRIX_I_V._m02_m12_m22;
                    origin=input.positionWS-direction*dot(input.positionWS-origin,direction);
                }
                float2 interval=BoxInterval(LocalPoint(origin),LocalDirection(direction));
                float enter=max(0,interval.x),leave=interval.y;
                // Clamp integration itself, not just the proxy face. This handles
                // opaque terrain in front, halfway through, and a camera inside the box.
                float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 opaque=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
                leave=min(leave,dot(opaque-origin,direction));
                if(leave<=enter)return 0;
                int steps=clamp((int)_VolumeSteps,32,48);
                float ds=(leave-enter)/steps;
                float transmission=1;float3 radiance=0;
                [loop]for(int i=0;i<48;i++)
                {
                    if(i>=steps||transmission<.02)break;
                    float t=enter+(i+.5)*ds;
                    float3 world=origin+direction*t;float heat;
                    float density=Density(LocalPoint(world),world,heat);
                    // Beer-Lambert attenuation makes opacity stable when sample count changes.
                    float absorbed=1-exp(-density*ds*_Absorption);
                    float3 cool=_VolumeEdge.rgb,body=_VolumeBody.rgb,hot=_VolumeCore.rgb;
                    float3 temperature=lerp(cool,body,saturate(density*.95));
                    temperature=lerp(temperature,hot,heat);
                    radiance+=transmission*absorbed*temperature;
                    transmission*=1-absorbed;
                }
                float alpha=1-transmission;
                if(_VolumeDebug>1.5)return float4(float3(saturate((leave-enter)/_VolumeExtent.z),.12,.55)*.7,.7);
                if(_VolumeDebug>.5)return float4(alpha.xxx,alpha);
                float visibility=1-saturate(_EarthSeismicVision);
                return float4(radiance,alpha)*visibility;
            }
            ENDHLSL
        }
    }
}
