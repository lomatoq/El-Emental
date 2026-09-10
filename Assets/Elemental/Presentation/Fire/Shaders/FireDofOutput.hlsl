#ifndef ELEMENTAL_FIRE_DOF_INCLUDED
#define ELEMENTAL_FIRE_DOF_INCLUDED
float4 _ElementalFireDofParams;
struct FireDofOutput {float4 color:SV_Target0;float4 depth:SV_Target1;};
void FireDepthAccumulate(inout float2 moments,float3 world,float weight)
{
 float eye=max(.001,-TransformWorldToView(world).z);weight=max(0,weight);
 moments+=float2(eye*weight,weight);
}
FireDofOutput FireDofPack(float4 color,float2 moments)
{
 FireDofOutput o;o.color=color;
 float coverage=saturate(max(color.a,max(color.r,max(color.g,color.b))*.25));
 float eye=moments.x/max(.00001,moments.y);
 float sharp=step(_ElementalFireDofParams.x,eye)*step(eye,_ElementalFireDofParams.y);
 o.depth=moments.y>.000001?float4(eye*coverage,coverage,coverage*sharp,0):0;return o;
}
#endif
