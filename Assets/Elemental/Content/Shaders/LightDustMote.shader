Shader "Elemental/Light Dust Mote"
{
    Properties
    {
        _BaseMap("Soft Particle Alpha", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1.0, 0.86, 0.58, 0.62)
        _Brightness("Brightness", Range(0, 4)) = 1.55
        _FlipbookBlending("Particle Sheet Frame Blending", Range(0, 1)) = 0
        _FlipbookColumns("Particle Sheet Columns", Float) = 1
        _FlipbookRows("Particle Sheet Rows", Float) = 1
        _FlipbookSpeed("Sheet Playback Speed", Range(1, 5)) = 1
        _SoftMap("Original Soft Dust", 2D) = "white" {}
        _SoftMix("Original Soft Dust Mix", Range(0, 1)) = 0
        _EdgeSoftness("Sheet edge feather in tile UV",Range(0,0.08))=0
        _FlowStrength("Continuous smoke deformation",Range(0,0.08))=0
        [HideInInspector] _SurfaceWisp("Curved surface wisp",Float)=0
        _NightVisibility("Night Ambient Visibility", Range(0.02, 0.2)) = 0.09
        _ShadowFloor("Shadow Visibility", Range(0, 0.25)) = 0.03
        _ProceduralRadialMask("Procedural Radial Mask", Range(0, 1)) = 1
        _SoftParticleNearDistance("Soft Particle Near Distance", Range(0, 4)) = 0
        _SoftParticleInvDistance("Soft Particle Inverse Distance", Range(0.1, 12)) = 3.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Sunlit Dust"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SoftMap);
            SAMPLER(sampler_SoftMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Brightness;
                half _FlipbookBlending;
                half _FlipbookColumns;
                half _FlipbookRows;
                half _FlipbookSpeed;
                half _SoftMix;
                half _EdgeSoftness;
                half _FlowStrength;
                half _SurfaceWisp;
                half _NightVisibility;
                half _ShadowFloor;
                half _ProceduralRadialMask;
                half _SoftParticleNearDistance;
                half _SoftParticleInvDistance;
            CBUFFER_END
            float _ElementalNight01;
            float4 _WispUp;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // Shuriken UV + UV2 pack current/next frame coordinates in
                // TEXCOORD0; AnimBlend is TEXCOORD1.x. The sheet module owns
                // frame selection, never a global clock shared by all puffs.
                float4 uv : TEXCOORD0;
                float4 frameMotion : TEXCOORD1;
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
                    float wave=.055*(sin(tileUv.x*6+time)+.6*cos(tileUv.y*8-time*.7))*edge;
                    positionInputs.positionWS+=_WispUp.xyz*wave;
                    positionInputs.positionCS=TransformWorldToHClip(positionInputs.positionWS);
                }
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
                half shadow = lerp(_ShadowFloor, 1.0h, mainLight.shadowAttenuation);
                half3 directLighting = mainLight.color *
                    (mainLight.distanceAttenuation * shadow);
                half3 ambientLighting = max(0.0h, SampleSH(half3(0.0h, 1.0h, 0.0h)));
                // Neutral white key light at intensity one reproduces the former
                // unlit material. Ambient fills shadows/night but cannot brighten
                // ordinary daylight dust past its authored albedo.
                // The gameplay ambient/key can be deliberately held bright at night.
                // Cosmetic dust follows the celestial exposure so it cannot glow white.
                half night = saturate(_ElementalNight01);
                half nightExposure=lerp(1.0h,.32h,night);
                // Weak night SH multiplied by .08 produced black cutouts. Keep a
                // restrained cool ambient floor, with the material albedo intact.
                half3 nightFill = half3(.78h,.92h,1.15h) * _NightVisibility * night;
                half3 lighting = max(saturate(ambientLighting + directLighting)*nightExposure, nightFill);

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                float particleDepth = -TransformWorldToView(input.positionWS).z;
                half softFade = saturate(
                    (sceneDepth - particleDepth - _SoftParticleNearDistance) *
                    _SoftParticleInvDistance);

                // Lighting changes radiance, not the authored cloud density/fade.
                // This preserves effect silhouette and opacity at dusk and night.
                half alpha = mote * input.color.a * _BaseColor.a * softFade;
                // Keep the complete legacy Particles/Unlit texture contract.
                // RumbleDustSoft is currently white RGB with authored alpha, but
                // sampling RGB prevents a future colored dust texture from being
                // silently flattened by this lighting shader.
                half3 color = baseSample.rgb * input.color.rgb * _BaseColor.rgb * lighting * _Brightness;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
