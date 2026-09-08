using System;
using UnityEditor;
using UnityEngine;
using Elemental.Presentation.UI;

namespace Elemental.Authoring.Editor
{
    public static class StoneReferenceSpriteInstaller
    {
        private const string Root="Assets/Elemental/Content/UI/Stone/ReferenceSprites/";
        public static void Apply(ElementalStoneSkin skin)
        {
            if(!System.IO.File.Exists(Root+"selected-construction.png"))return;
            // Insets describe the cream face in source pixels. The green tail is visual overflow.
            skin.referenceSelected=Prepare("selected-construction",2138,736,new Rect(0,34,2125,570),new Vector4(350,250,220,40));
            skin.referenceSelectedInsets=new Vector4(210,237,2,21);
            skin.referenceNormal=Prepare("passive-button",2172,724,new Rect(54,214,2056,285),new Vector4(125,12,125,12));
            skin.referenceCard=Prepare("active-element-card",2135,736,new Rect(30,110,2071,465),Vector4.zero);
            skin.fire=Prepare("element-glyphs",2172,724,new Rect(90,100,430,530),Vector4.zero,"reference-fire");
            skin.earth=Prepare("element-glyphs",2172,724,new Rect(585,120,470,470),Vector4.zero,"reference-earth");
            skin.water=Prepare("element-glyphs",2172,724,new Rect(1170,115,400,500),Vector4.zero,"reference-water");
            skin.air=Prepare("air-glyph",1254,1254,new Rect(190,104,970,1070),Vector4.zero,"reference-air");
        }
        public static Sprite Prepare(string name,int width,int height,Rect rect,Vector4 border,string spriteName=null)
        {
            string texturePath=Root+name+".png",spritePath=Root+(spriteName??name)+".asset";
            var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Import reference artwork before installing: "+texturePath);
            string previous=EditorJsonUtility.ToJson(importer);
            importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;
            importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.isReadable=false;
            if(previous!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture.width!=width||texture.height!=height)throw new InvalidOperationException("Reference sprite native dimensions changed: "+texturePath);
            var replacement=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
            replacement.name=spriteName??name;
            var existing=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if(existing==null){AssetDatabase.CreateAsset(replacement,spritePath);return replacement;}
            bool same=existing.rect==rect&&existing.border==border&&existing.texture==texture;
            var a=existing.uv;var b=replacement.uv;same&=a.Length==b.Length;
            for(int i=0;same&&i<a.Length;i++)same=a[i]==b[i];
            if(!same){EditorUtility.CopySerialized(replacement,existing);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
            UnityEngine.Object.DestroyImmediate(replacement);return existing;
        }
    }
}
