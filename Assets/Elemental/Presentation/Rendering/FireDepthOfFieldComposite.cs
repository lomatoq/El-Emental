using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
namespace Elemental.Presentation.Rendering
{
 internal static class FireDepthOfFieldComposite
 {
  private static readonly int Before=Shader.PropertyToID("_FireBefore"),Moments=Shader.PropertyToID("_FireMoments"),Params=Shader.PropertyToID("_ElementalFireDofParams"),Texel=Shader.PropertyToID("_FireDofTexelRadius"),Depth=Shader.PropertyToID("_CameraDepthTexture");
  public static TextureHandle Record(RenderGraph graph,Material material,TextureHandle before,TextureHandle after,TextureHandle moments,TextureHandle sceneDepth,Vector4 parameters,float radius)
  {
   var desc=graph.GetTextureDesc(after);desc.name="Fire contribution bokeh at actual fire depth";desc.clearBuffer=false;var output=graph.CreateTexture(desc);
   using(var builder=graph.AddRasterRenderPass<Data>("Elemental actual-depth fire bokeh",out var data))
   {
    data.before=before;data.after=after;data.moments=moments;data.depth=sceneDepth;data.material=material;data.parameters=parameters;data.texel=new Vector4(1f/desc.width,1f/desc.height,Mathf.Min(10,radius),0);
    builder.UseTexture(before,AccessFlags.Read);builder.UseTexture(after,AccessFlags.Read);builder.UseTexture(moments,AccessFlags.Read);builder.UseTexture(sceneDepth,AccessFlags.Read);builder.SetRenderAttachment(output,0,AccessFlags.Write);builder.AllowGlobalStateModification(true);
    builder.SetRenderFunc(static(Data p,RasterGraphContext context)=>{context.cmd.SetGlobalTexture(Before,p.before);context.cmd.SetGlobalTexture(Moments,p.moments);context.cmd.SetGlobalTexture(Depth,p.depth);context.cmd.SetGlobalVector(Params,p.parameters);context.cmd.SetGlobalVector(Texel,p.texel);Blitter.BlitTexture(context.cmd,p.after,new Vector4(1,1,0,0),p.material,0);});
   }
   return output;
  }
  private sealed class Data{public TextureHandle before,after,moments,depth;public Material material;public Vector4 parameters,texel;}
 }
}
