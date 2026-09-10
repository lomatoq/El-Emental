            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Transparent receivers must use their own world-space shadow lookup,
            // never the opaque depth surface cached by screen-space shadows.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SoftMap);
            SAMPLER(sampler_SoftMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Brightness;
                half _CameraNearFadeStart;
                half _CameraNearFadeEnd;
                half _FlipbookBlending;
                half _FlipbookColumns;
                half _FlipbookRows;
                half _FlipbookSpeed;
                half _SoftMix;
                half _EdgeSoftness;
                half _FlowStrength;
                half _SurfaceWisp;
                half _WispWaveHeight;
                half _NightVisibility;
                half _ShadowFloor;
                half _ShadowSoftnessMeters;
                half _ProceduralRadialMask;
                half _SoftParticleNearDistance;
                half _SoftParticleInvDistance;
            CBUFFER_END
            float4 _ElementalPlanetCenterRadius;
            float4 _WispUp;
            // Same-frame QA comparison only. Runtime default zero bounds excessive
            // diffuse radiance; no material tint/alpha override or alternate system.
            float _ElementalDustLegacyRadiance;
            float3 ElementalDustUp(float3 positionWS)
            {
                // The established wisp frame remains the non-planet preview fallback.
                float3 radial=positionWS-_ElementalPlanetCenterRadius.xyz;
                return _ElementalPlanetCenterRadius.w>0 && dot(radial,radial)>1e-8
                    ? normalize(radial) : (dot(_WispUp.xyz,_WispUp.xyz)>1e-8
                        ? normalize(_WispUp.xyz) : float3(0,1,0));
            }
            half DustVolumeMainShadow(float3 positionWS,float3 up,half centerShadow)
            {
                half volumeShadow=centerShadow;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                UNITY_BRANCH if(_ShadowSoftnessMeters>.001h)
                {
                    // A ground wisp covers a finite volume. Integrate nearby shadow
                    // transmittance instead of turning the entire wisp black at one
                    // hard receiver boundary. Opacity and material tint remain authored.
                    float3 tangent=normalize(cross(up,abs(up.y)<.9?float3(0,1,0):float3(1,0,0)));
                    float3 bitangent=cross(up,tangent);
                    float radius=_ShadowSoftnessMeters;
                    half sum=centerShadow*.4h;
                    sum+=MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS+tangent*radius))*.15h;
                    sum+=MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS-tangent*radius))*.15h;
                    sum+=MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS+bitangent*radius))*.15h;
                    sum+=MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS-bitangent*radius))*.15h;
                    volumeShadow=sum;
                }
                #endif
                return volumeShadow;
            }
            half3 ElementalAdditionalResponse(Light light, half3 unusedNormal)
            {
                return light.color*light.distanceAttenuation*
                    lerp(_ShadowFloor,1.0h,light.shadowAttenuation);
            }
            #include "ElementalAdditionalRadiance.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // Shuriken UV + UV2 pack current/next frame coordinates in
                // TEXCOORD0; AnimBlend is TEXCOORD1.x. The sheet module owns
                // frame selection, never a global clock shared by all puffs.
                float4 uv : TEXCOORD0;
                float4 frameMotion : TEXCOORD1;
                #if defined(ELEMENTAL_FIRE_DUST)
                float4 clipA:TEXCOORD2;float4 clipB:TEXCOORD3;float4 fireCenter:TEXCOORD4;float4 fireAxis:TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
                half animBlend : TEXCOORD2;
                half4 color : COLOR;
                float3 motion : TEXCOORD3;
                #if defined(ELEMENTAL_FIRE_DUST)
                nointerpolation float4 clipA:TEXCOORD4;nointerpolation float4 clipB:TEXCOORD5;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                if(_SurfaceWisp>.5h)
                {
                    float2 tileUv=frac(input.uv.xy*max(float2(_FlipbookColumns,_FlipbookRows),1));
                    float phase=input.frameMotion.y*6.28318;
                    float time=_Time.y*lerp(.6,1.3,input.frameMotion.z)+phase;
                    float edge=sin(tileUv.x*3.14159)*sin(tileUv.y*3.14159);
                    float wave=max(0,_WispWaveHeight)*(sin(tileUv.x*6+time)+.6*cos(tileUv.y*8-time*.7))*edge;
                    positionInputs.positionWS+=ElementalDustUp(positionInputs.positionWS)*wave;
                    positionInputs.positionCS=TransformWorldToHClip(positionInputs.positionWS);
                }
                #if defined(ELEMENTAL_FIRE_DUST)
                float3 facing=SafeNormalize(GetCameraPositionWS()-input.fireCenter.xyz);
                float3 rise=input.fireAxis.xyz-facing*dot(input.fireAxis.xyz,facing);
                if(dot(rise,rise)<.0001)rise=UNITY_MATRIX_I_V._m01_m11_m21;
                rise=SafeNormalize(rise-facing*dot(rise,facing));float3 side=SafeNormalize(cross(rise,facing));
                float2 q=input.uv.xy*2-1;
                positionInputs.positionWS=input.fireCenter.xyz+side*q.x*input.fireCenter.w+rise*q.y*input.fireAxis.w;
                positionInputs.positionCS=TransformWorldToHClip(positionInputs.positionWS);
                float2 tiles=max(float2(_FlipbookColumns,_FlipbookRows),1);float total=tiles.x*tiles.y;
                float frame=saturate(input.frameMotion.x)*max(0,total-1.001),first=floor(frame),next=min(first+1,total-1);
                float2 cellA=float2(fmod(first,tiles.x),tiles.y-1-floor(first/tiles.x));
                float2 cellB=float2(fmod(next,tiles.x),tiles.y-1-floor(next/tiles.x));
                input.uv=float4((cellA+clamp(input.uv.xy,.003,.997))/tiles,(cellB+clamp(input.uv.xy,.003,.997))/tiles);
                input.color=float4(1,1,1,input.frameMotion.w);
                input.frameMotion=float4(frac(frame),frac(input.frameMotion.z),.5,frac(input.frameMotion.z*.71));
                output.clipA=input.clipA;output.clipB=input.clipB;
                #endif
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.animBlend = input.frameMotion.x;
                output.motion=input.frameMotion.yzw;
                output.color = input.color;
                return output;
            }

            half SheetCoverage(float2 uv,float2 tileSize)
            {
                // Keep every tap within its own cell, including atlas borders.
                float2 cell=floor(uv/tileSize+0.00001)*tileSize;
                float2 lo=cell+tileSize*.003,hi=cell+tileSize*.997;
                float2 d=tileSize*_EdgeSoftness;
                half a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,clamp(uv,lo,hi)).a*.4h;
                a+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,clamp(uv+float2(d.x,0),lo,hi)).a*.15h;
                a+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,clamp(uv-float2(d.x,0),lo,hi)).a*.15h;
                a+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,clamp(uv+float2(0,d.y),lo,hi)).a*.15h;
                a+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,clamp(uv-float2(0,d.y),lo,hi)).a*.15h;
                return a;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(ELEMENTAL_FIRE_DUST)
                if(dot(input.clipA.xyz,input.clipA.xyz)>.5&&dot(float4(input.positionWS,1),input.clipA)<0)discard;
                if(dot(input.clipB.xyz,input.clipB.xyz)>.5&&dot(float4(input.positionWS,1),input.clipB)<0)discard;
                #endif
                float2 centered = input.uv.xy * 2.0 - 1.0;
                float radiusSquared = dot(centered, centered);
                half radialMask = saturate((1.0h - (half)radiusSquared) * 4.0h);
                radialMask *= radialMask;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                if (_FlipbookBlending > .5h)
                {
                    float2 tiles=max(float2(_FlipbookColumns,_FlipbookRows),1);
                    float2 localUv=frac(input.uv.xy*tiles);
                    float t=_SurfaceWisp>.5h?_Time.y*lerp(.6,1.3,input.motion.y)+input.motion.x*6.28318:
                        _Time.y*.65+dot(input.positionWS,float3(.13,.09,.17));
                    float2 flow=float2(sin(localUv.y*7+t),cos(localUv.x*6-t*.81));
                    flow*=max(_FlowStrength,_SurfaceWisp*.1)*sin(localUv.x*3.14159)*sin(localUv.y*3.14159);
                    float2 warped=clamp(localUv+flow,.003,.997);
                    float2 currentCell=floor(input.uv.xy*tiles+.00001);
                    float2 nextCell=floor(input.uv.zw*tiles+.00001);
                    half currentAlpha=SheetCoverage((currentCell+warped)/tiles,1/tiles);
                    half nextAlpha=SheetCoverage((nextCell+warped)/tiles,1/tiles);
                    // The authored atlas is a monochrome coverage mask. Its black
                    // transparent padding must not enter RGB during bilinear filtering:
                    // multiplying the already filtered RGB by alpha creates a dark rim.
                    half blend = saturate(input.animBlend);
                    half frameAlpha = lerp(currentAlpha, nextAlpha, blend);
                    baseSample = half4(1, 1, 1, frameAlpha);
                    half softAlpha = SAMPLE_TEXTURE2D(_SoftMap, sampler_SoftMap, warped).a;
                    if(_SurfaceWisp>.5h)
                    {
                        // Two advected phases: the resetting phase is fully hidden
                        // by its partner. Weights sum to one, never add radiance.
                        float a=frac(_Time.y*lerp(.09,.18,input.motion.y)+input.motion.z);
                        float b=frac(a+.5);
                        float2 direction=float2(sin(input.motion.x*6.28318)*.15,.48);
                        half first=SAMPLE_TEXTURE2D(_SoftMap,sampler_SoftMap,clamp(warped-direction*(a-.5),.003,.997)).a;
                        half second=SAMPLE_TEXTURE2D(_SoftMap,sampler_SoftMap,clamp(warped-direction*(b-.5),.003,.997)).a;
                        softAlpha=lerp(first,second,abs(a*2-1));
                    }
                    baseSample.a = lerp(frameAlpha, softAlpha, _SoftMix);
                }
                half mote = baseSample.a *
                    lerp(1.0h, radialMask, _ProceduralRadialMask);
                if(_SurfaceWisp>.5h)
                {
                    float2 sheetUv=_FlipbookBlending>.5h?frac(input.uv.xy*max(float2(_FlipbookColumns,_FlipbookRows),1)):input.uv.xy;
                    float2 q=sheetUv*2-1;
                    q.x+=.12*sin(q.y*5+input.motion.x*6.28318-_Time.y*.7);
                    // Fade the carrier before its rectangular boundary, independently
                    // of the artist's texture/color. Thin grazing silhouettes also fade.
                    mote*=1-smoothstep(.38,1,dot(q,q));
                    float3 geometricNormal=normalize(cross(ddx(input.positionWS),ddy(input.positionWS)));
                    float facing=abs(dot(geometricNormal,SafeNormalize(_WorldSpaceCameraPos-input.positionWS)));
                    mote*=smoothstep(.025,.2,facing);
                }
                clip(mote - 0.001h);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                // Dust is volumetric and has no stable billboard surface normal.
                // Use the real directional radiance plus the scene SH ambient,
                // instead of an invented N.L or a clock-derived day/night tint.
                half3 radialUp=ElementalDustUp(input.positionWS);
                mainLight.shadowAttenuation=DustVolumeMainShadow(input.positionWS,radialUp,mainLight.shadowAttenuation);
                half3 directLighting=ElementalAdditionalResponse(mainLight,radialUp)+
                    ElementalAdditionalRadiance(input.positionWS,
                        GetNormalizedScreenSpaceUV(input.positionCS),radialUp);
                half3 ambientLighting=max(0.0h,SampleSH(radialUp));
                // The shared environment owns night radiance. Opacity remains authored below.
                // Smooth neutral fill keeps tinted dust from collapsing into saturated
                // brown/blue at dusk while retaining actual direct-light contrast.
                half ambientLuma=dot(ambientLighting,half3(.2126,.7152,.0722));
                half3 ambientHue=lerp(ambientLuma.xxx,ambientLighting,.55h);
                half3 lighting=ambientHue+max(0.0h,_NightVisibility-ambientLuma)*half3(.82h,.88h,1.0h)+directLighting;

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth=SampleSceneDepth(screenUv);
                float sceneDepth=LinearEyeDepth(rawDepth,_ZBufferParams);
                #if UNITY_REVERSED_Z
                bool dustClear=rawDepth<=0;
                #else
                bool dustClear=rawDepth>=1;
                #endif
                float particleDepth = -TransformWorldToView(input.positionWS).z;
                half softFade = saturate(
                    (sceneDepth - particleDepth - _SoftParticleNearDistance) *
                    _SoftParticleInvDistance);

                if(dustClear)softFade=1;

                // Lighting changes radiance, not the authored cloud density/fade.
                // This preserves effect silhouette and opacity at dusk and night.
                half alpha = mote * input.color.a * _BaseColor.a * softFade;
                // Opt-in camera-near fade for airborne motes only; ordinary dust and fire smoke default to zero.
                if(_CameraNearFadeEnd>_CameraNearFadeStart)
                    alpha*=smoothstep(_CameraNearFadeStart,_CameraNearFadeEnd,particleDepth);
                // Keep the complete legacy Particles/Unlit texture contract.
                // RumbleDustSoft is currently white RGB with authored alpha, but
                // sampling RGB prevents a future colored dust texture from being
                // silently flattened by this lighting shader.
                half3 color = baseSample.rgb * input.color.rgb * _BaseColor.rgb * lighting * _Brightness;
                // Preserve the authored low-light response. Only excessive diffuse
                // radiance receives a smooth shoulder; non-emissive dust must not
                // turn into a bright yellow light source before sky compositing.
                // Scale all channels together so the warm tint cannot hue-shift as
                // independent RGB channels clip. Actual fire emission is separate.
                half peak=max(color.r,max(color.g,color.b));
                half bounded=.75h+.25h*(1-exp(-max(0.0h,peak-.75h)/.25h));
                half gain=peak>.75h?bounded/max(.0001h,peak):1.0h;
                color*=lerp(gain,1.0h,saturate(_ElementalDustLegacyRadiance));
                return half4(color * alpha, alpha);
            }
