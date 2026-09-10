#ifndef ELEMENTAL_VALLEY_ATMOSPHERE_V2
#define ELEMENTAL_VALLEY_ATMOSPHERE_V2
float _ElementalValleyEnabled,_ElementalValleyDebug,_ElementalValleyChromaticPixels;
float _ElementalValleyMidAerial;
float4x4 _ElementalWorldToValley;
float4 _ElementalValleyFog,_ElementalValleyFar,_ElementalValleyFarArt;
float4 _ElementalValleyClosure;
float4 _ElementalValleyStorm;
float4 _ElementalValleyDay,_ElementalValleyBottom,_ElementalValleyNight,_ElementalValleyDusk;
float4 _ElementalValleyTimeFogTop,_ElementalValleyTimeFogBottom,_ElementalValleyTimeCloudTop,_ElementalValleyTimeCloudBottom;
float4 _ElementalValleyBanks4[4],_ElementalValleyBankSizes4[4];
TEXTURE2D(_ElementalValleyCloudArt);SAMPLER(sampler_ElementalValleyCloudArt);
float ValleyAboveIntegral(float low,float high,float lengthMetres,float falloff)
{
    float x=(high-low)/falloff;
    float ratio=x<0.001?1-x*0.5+x*x/6:(1-exp(-x))/max(x,1e-7);
    return lengthMetres*exp(-low/falloff)*ratio;
}
float ValleyIntegral(float h0,float h1,float lengthMetres,float falloff)
{
    float low=min(h0,h1),high=max(h0,h1);
    if(high<=0)return lengthMetres;
    if(low>=0)return ValleyAboveIntegral(low,high,lengthMetres,falloff);
    float below=lengthMetres*(-low)/max(high-low,1e-7);
    return below+ValleyAboveIntegral(0,high,lengthMetres-below,falloff);
}
float4 ValleyCloudSample(int index,float3 origin,float3 ray,float surfaceDistance,float3 fogColor,float day,out float travel)
{
    float3 centre=_ElementalValleyBanks4[index].xyz;
    float3 normal=float3(-centre.x,0,-centre.z);normal/=max(length(normal),1e-4);
    float denominator=dot(ray,normal);travel=-1;
    float safeDenominator=(denominator<0?-1:1)*max(abs(denominator),1e-5);
    float t=dot(centre-origin,normal)/safeDenominator;
    float3 offset=origin+ray*t-centre;
    float3 right=cross(float3(0,1,0),normal);
    float2 uv=float2(dot(offset,right),offset.y)/_ElementalValleyBankSizes4[index].xy+0.5;
    if(_ElementalValleyBankSizes4[index].z>0.5)uv.x=1-uv.x;
    // Derivatives execute for the full pixel quad before any divergent rejection.
    // Explicit gradients select the authored mip chain rather than forcing LOD0.
    float2 gradientX=ddx(uv),gradientY=ddy(uv);
    if(abs(denominator)<1e-5 || t<=0 || t>=surfaceDistance || t<_ElementalValleyFar.x || any(uv<=0) || any(uv>=1))return 0;
    float4 art=SAMPLE_TEXTURE2D_GRAD(_ElementalValleyCloudArt,sampler_ElementalValleyCloudArt,uv,gradientX,gradientY);
    float2 edge=min(uv,1-uv);
    float alpha=art.a*_ElementalValleyFar.w*smoothstep(0,0.08,edge.x)*smoothstep(0,0.08,edge.y);
    alpha*=saturate((surfaceDistance-t)/30);
    float startHeight=origin.y-_ElementalValleyFog.x;
    float endHeight=startHeight+ray.y*t;
    float bankOptical=ValleyIntegral(startHeight,endHeight,t,_ElementalValleyFog.y)*_ElementalValleyFog.z;
    alpha*=exp(-max(0,bankOptical));
    float3 tint=lerp(_ElementalValleyTimeCloudBottom.rgb,_ElementalValleyTimeCloudTop.rgb,smoothstep(.15,.85,uv.y));
    float haze=1-exp(-max(t-_ElementalValleyFar.x,0)/6000);
    travel=t;return float4(lerp(art.rgb*tint,fogColor,haze*0.45),alpha);
}
float4 ApplyValleyAtmosphere(float4 source,float2 uv,float rawDepth,bool hasGeometry)
{
    #if UNITY_REVERSED_Z
    float nearDepth=1,farDepth=0;
    #else
    float nearDepth=UNITY_NEAR_CLIP_VALUE,farDepth=1;
    #endif
    float3 nearWorld=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
    float3 farWorld=ComputeWorldSpacePosition(uv,farDepth,UNITY_MATRIX_I_VP);
    float3 origin=unity_OrthoParams.w>0.5?nearWorld:_WorldSpaceCameraPos;
    float3 ray=normalize(farWorld-nearWorld);
    float distanceMetres=_ElementalValleyFog.w;
    if(hasGeometry)
    {
        float depth=rawDepth;
        #if !UNITY_REVERSED_Z
        depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
        #endif
        float3 surface=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
        distanceMetres=max(0,dot(surface-origin,ray));
    }
    float3 localOrigin=mul(_ElementalWorldToValley,float4(origin,1)).xyz;
    float3 localRay=mul((float3x3)_ElementalWorldToValley,ray);
    // Evaluate derivatives before divergent masks/debug exits. Reconstructed
    // geometry gives each planar face its own projected halftone direction.
    float3 artSurfaceWS=origin+ray*distanceMetres;
    float3 faceCross=cross(ddx(artSurfaceWS),ddy(artSurfaceWS));
    float3 artNormalWS=faceCross*rsqrt(max(dot(faceCross,faceCross),1e-12));
    float3 faceVertical=float3(0,1,0)-artNormalWS*artNormalWS.y;
    if(dot(faceVertical,faceVertical)<.01)
        faceVertical=float3(0,0,1)-artNormalWS*artNormalWS.z;
    float2 rowUp=mul((float3x3)UNITY_MATRIX_V,faceVertical).xy;
    rowUp.y*=_ProjectionParams.x;
    rowUp=dot(rowUp,rowUp)>.0001?normalize(rowUp):float2(0,1);
    float2 rowAcross=float2(rowUp.y,-rowUp.x);
    float h0=localOrigin.y-_ElementalValleyFog.x;
    float h1=h0+localRay.y*distanceMetres;
    float optical=ValleyIntegral(h0,h1,distanceMetres,_ElementalValleyFog.y)*_ElementalValleyFog.z;
    float veil=1-exp(-max(0,optical));
    if(!hasGeometry)
    {
        // Sky has no finite surface. Integrate the halfspace to infinity instead
        // of terminating at camera far clip and exposing a blue hole at shallow angles.
        float slope=localRay.y;
        float skyIntegral=(max(-h0,0)+_ElementalValleyFog.y*exp(-max(h0,0)/_ElementalValleyFog.y))/max(slope,1e-7);
        veil=slope<=0?step(1e-7,_ElementalValleyFog.z):1-exp(-skyIntegral*_ElementalValleyFog.z);
    }
    float aerial=(1-exp(-max(0,distanceMetres-_ElementalValleyFar.x)/_ElementalValleyFar.y))*_ElementalValleyFar.z;
    float alpha=veil;
    float cloudProtection=1;
    float farLightDiffusion=0,farArtMask=0,closure=0;
    if(hasGeometry)
    {
        // Preserve arena, character, held rocks and near islands exactly. Continuous
        // distant attenuation uses path length, not a noisy midpoint altitude estimate.
        float protect=smoothstep(_ElementalValleyFar.x,_ElementalValleyFar.x+100,distanceMetres);
        float radius=max(0,_ElementalPlanetCenterRadius.w);
        float planetProtect=smoothstep(radius+80,radius+140,length(localOrigin+localRay*distanceMetres));
        float3 surfaceLocal=localOrigin+localRay*distanceMetres;
        // Latest art requirement: protect the playable upper cap, not the entire
        // spherical underside. Lower terrain joins the opaque veil continuously.
        float playableWindow=smoothstep(radius*0.45,radius*0.75,surfaceLocal.y);
        // The underside seal closes distant terrain, not nearby fighters or
        // held stones. Preserve the immediate interaction volume at every normal;
        // restore the original underside treatment smoothly beyond 24 metres.
        float nearSurfaceFogWeight=smoothstep(12,24,distanceMetres);
        cloudProtection=lerp(1,protect*planetProtect,playableWindow)*nearSurfaceFogWeight;
        // The forced underside seal belongs only to the planet. Applying it to
        // all distant columns painted a shared narrow horizontal opacity band.
        float lowerTerrain=(1-smoothstep(-radius*0.25,radius*0.60,surfaceLocal.y))*(1-planetProtect);
        // Only aerial haze is capped. Physical height fog must reach full opacity
        // continuously below the sea instead of stopping at the aerial cap.
        closure=smoothstep(_ElementalValleyClosure.x,_ElementalValleyClosure.y,distanceMetres);
        // Middle aerial attenuation only; retain lower height fog and far closure.
        float midWindow=smoothstep(_ElementalValleyFar.x,_ElementalValleyFar.x+200,distanceMetres)*
            (1-smoothstep(_ElementalValleyClosure.x-600,_ElementalValleyClosure.x,distanceMetres));
        aerial*=lerp(1,clamp(_ElementalValleyMidAerial,.7,1),midWindow);
        alpha=max(lowerTerrain,1-(1-veil)*(1-aerial)*(1-closure))*cloudProtection;
        farArtMask=smoothstep(_ElementalValleyFarArt.y,_ElementalValleyFarArt.z,distanceMetres)*protect*planetProtect*_ElementalValleyFarArt.x;
        farLightDiffusion=(1-exp(-max(0,distanceMetres-400)/1400))*0.24*cloudProtection*playableWindow;
    }
    float day=saturate(1-_ElementalNight01);
    float colorHeight=localOrigin.y+localRay.y*(hasGeometry?distanceMetres:500);
    float heightColor=smoothstep(-_ElementalPlanetCenterRadius.w*0.6,_ElementalPlanetCenterRadius.w*0.5,colorHeight);
    float3 dayFog=lerp(_ElementalValleyBottom.rgb,_ElementalValleyDay.rgb,heightColor);
    float3 fog=lerp(_ElementalValleyTimeFogBottom.rgb,_ElementalValleyTimeFogTop.rgb,heightColor);
    // Closed scenery shares one ray-based palette, independent of its hidden depth.
    float skyHeight=smoothstep(-_ElementalPlanetCenterRadius.w*0.6,_ElementalPlanetCenterRadius.w*0.5,localOrigin.y+localRay.y*500);
    fog=lerp(fog,lerp(_ElementalValleyTimeFogBottom.rgb,_ElementalValleyTimeFogTop.rgb,skyHeight),closure*cloudProtection);
    float transmittance=1-saturate(alpha);
    farArtMask*=transmittance;
    farLightDiffusion*=transmittance;
    // One distant cloud-region pulse inside the existing fog scattering owner.
    // Direction/height/distance masks exclude near geometry and most of the sky.
    float stormDirection=smoothstep(.94,.995,dot(normalize(localRay),(_ElementalValleyStorm.xyz/max(length(_ElementalValleyStorm.xyz),1e-5))));
    float stormHeight=(1-smoothstep(.03,.18,abs(localRay.y-.03)));
    float stormDistance=smoothstep(700,1100,distanceMetres);
    fog+=float3(.76,.71,.93)*(_ElementalValleyStorm.w*stormDirection*stormHeight*stormDistance*cloudProtection);
    float3 color=lerp(source.rgb,fog,saturate(alpha));
    // Pale cool air perspective only on distant upper stone; never global RGB fringes.
    color=lerp(color,float3(0.85,0.91,0.98),farLightDiffusion*day);
    if(_ElementalValleyDebug>0.5 && _ElementalValleyDebug<1.5)return float4(alpha.xxx,source.a);
    if(_ElementalValleyDebug>2.5)return float4(farArtMask.xxx,source.a);
    if(_ElementalValleyDebug>1.5)return float4(hasGeometry?float3(saturate(distanceMetres/3000),0,0):float3(0,0,1),source.a);
    if(hasGeometry && farArtMask>0.001 && day>0.001 && _ElementalValleyChromaticPixels>0.001)
    {
        // Lightward colour fringe: only distant upper stone, never sky,
        // nearby gameplay or UI. Depth guards reject samples from near silhouettes.
        float2 lightward=mul((float3x3)UNITY_MATRIX_V,_ElementalSunDirection.xyz).xy;
        lightward.y*=_ProjectionParams.x;
        lightward=length(lightward)>0.001?normalize(lightward):float2(1,0);
        float2 shift=lightward*_BlitTexture_TexelSize.xy*(_BlitTexture_TexelSize.w/1080)*min(3.0,_ElementalValleyChromaticPixels*4.5);
        float2 positive=saturate(uv+shift),negative=saturate(uv-shift);
        float positiveDepth=LinearEyeDepth(SampleSceneDepth(positive),_ZBufferParams);
        float negativeDepth=LinearEyeDepth(SampleSceneDepth(negative),_ZBufferParams);
        if(unity_OrthoParams.w>0.5)
        {
            float2 rawPair=float2(SampleSceneDepth(positive),SampleSceneDepth(negative));
            #if UNITY_REVERSED_Z
            rawPair=1-rawPair;
            #endif
            float2 eyePair=lerp(_ProjectionParams.y,_ProjectionParams.z,rawPair);
            positiveDepth=eyePair.x;negativeDepth=eyePair.y;
        }
        if(positiveDepth>_ElementalValleyFarArt.y && negativeDepth>_ElementalValleyFarArt.y)
        {
            float3 lightSample=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,positive).rgb;
            float3 oppositeSample=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,negative).rgb;
            float3 luminance=float3(.2126,.7152,.0722);
            float violetEdge=max(0,dot(lightSample-source.rgb,luminance));
            float greenEdge=max(0,dot(oppositeSample-source.rgb,luminance));
            // Edge luminance, rather than the sandstone's weak blue/green channels,
            // gives both complementary sides equal visibility.
            color+=(violetEdge*float3(.66,.12,.95)+greenEdge*float3(.24,.88,.36))*.72*farArtMask*day;
        }
    }
    // Fine halftone: the lit surface controls dot area before fog washes out its
    // contrast. Stable staggered rows, no random polarity or screen-wide gradient.
    // Restricted to distant opaque scenery; the existing near/UI guards remain.
    float2 artPixel=uv*_BlitTexture_TexelSize.zw*(1080/max(1,_BlitTexture_TexelSize.w));
    // Slow surface-space variation, no per-frame noise or hard scale boundaries.
    float scaleField=.5+.5*sin(dot(artSurfaceWS,float3(.0031,.0047,.0023)));
    float spacing=lerp(2.8,4.4,scaleField);
    float2 cell=float2(dot(artPixel,rowAcross),dot(artPixel,rowUp))/spacing;
    cell.x+=frac(floor(cell.y)*.5);
    float2 delta=frac(cell)-.5;
    float distanceToDot=length(delta);
    float litLuminance=dot(max(source.rgb,0),float3(.2126,.7152,.0722));
    float shade=1-smoothstep(.18,.85,litLuminance);
    float dotRadius=lerp(.07,.29,shade);
    float footprint=max(fwidth(distanceToDot)*.5,.02);
    float dotShape=1-smoothstep(dotRadius-footprint,dotRadius+footprint,distanceToDot);
    float ink=min(.10,_ElementalValleyFarArt.w*2.2)*farArtMask;
    float highlight=smoothstep(.72,1.15,litLuminance);
    // Dark ink in shade; fine pale dots on bright glints, using the same rows.
    color*=1-dotShape*lerp(.45,1.0,shade)*ink*(1-highlight);
    float lightDot=1-smoothstep(.12-footprint,.12+footprint,distanceToDot);
    color+=lightDot*highlight*ink*max(color,float3(.2,.2,.2))*.8;
    if(_ElementalValleyFar.w>0.001)
    {
        float travel[4]={-1,-1,-1,-1};
        float4 art[4]={float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0),float4(0,0,0,0)};
        [unroll]for(int i=0;i<4;i++)art[i]=ValleyCloudSample(i,localOrigin,localRay,distanceMetres,fog,day,travel[i]);
        // Sort these four analytical banks far-to-near for side/reverse viewpoints.
        [unroll]for(int a=0;a<3;a++)[unroll]for(int b=a+1;b<4;b++)
        {
            if(travel[a]<travel[b]){float t=travel[a];travel[a]=travel[b];travel[b]=t;float4 c=art[a];art[a]=art[b];art[b]=c;}
        }
        [unroll]for(int k=0;k<4;k++)color=lerp(color,art[k].rgb,art[k].a*cloudProtection);
    }
    color=ApplySunDust(color,ray,hasGeometry?distanceMetres:_SunDustDistance);
    return float4(color,source.a);
}
#endif
