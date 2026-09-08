#ifndef ELEMENTAL_VALLEY_ATMOSPHERE_V2
#define ELEMENTAL_VALLEY_ATMOSPHERE_V2
float _ElementalValleyEnabled,_ElementalValleyDebug;
float4x4 _ElementalWorldToValley;
float4 _ElementalValleyFog,_ElementalValleyFar;
float4 _ElementalValleyDay,_ElementalValleyNight,_ElementalValleyDusk;
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
    float3 tint=lerp(float3(0.22,0.28,0.38),float3(0.98,0.99,1.0),day);
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
    if(hasGeometry)source.rgb=ApplySunDust(source.rgb,ray,distanceMetres);
    float3 localOrigin=mul(_ElementalWorldToValley,float4(origin,1)).xyz;
    float3 localRay=mul((float3x3)_ElementalWorldToValley,ray);
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
    if(hasGeometry)
    {
        // Preserve arena, character, held rocks and near islands exactly. Continuous
        // distant attenuation uses path length, not a noisy midpoint altitude estimate.
        float protect=smoothstep(_ElementalValleyFar.x,_ElementalValleyFar.x+100,distanceMetres);
        float radius=max(0,_ElementalPlanetCenterRadius.w);
        float planetProtect=smoothstep(radius+80,radius+140,length(localOrigin+localRay*distanceMetres));
        cloudProtection=protect*planetProtect;
        alpha=min(_ElementalValleyFar.z,1-(1-veil)*(1-aerial))*cloudProtection;
    }
    float day=saturate(1-_ElementalNight01);
    float3 fog=lerp(_ElementalValleyNight.rgb,_ElementalValleyDay.rgb,day);
    fog=lerp(fog,_ElementalValleyDusk.rgb,saturate(_ElementalTwilight01)*0.6);
    float3 color=lerp(source.rgb,fog,saturate(alpha));
    if(_ElementalValleyDebug>0.5 && _ElementalValleyDebug<1.5)return float4(alpha.xxx,source.a);
    if(_ElementalValleyDebug>1.5)return float4(hasGeometry?float3(saturate(distanceMetres/3000),0,0):float3(0,0,1),source.a);
    if(_ElementalValleyFar.w>0.001)
    {
        float travel[4];float4 art[4];
        [unroll]for(int i=0;i<4;i++)art[i]=ValleyCloudSample(i,localOrigin,localRay,distanceMetres,fog,day,travel[i]);
        // Sort these four analytical banks far-to-near for side/reverse viewpoints.
        [unroll]for(int a=0;a<3;a++)[unroll]for(int b=a+1;b<4;b++)
        {
            if(travel[a]<travel[b]){float t=travel[a];travel[a]=travel[b];travel[b]=t;float4 c=art[a];art[a]=art[b];art[b]=c;}
        }
        [unroll]for(int k=0;k<4;k++)color=lerp(color,art[k].rgb,art[k].a*cloudProtection);
    }
    return float4(color,source.a);
}
#endif
