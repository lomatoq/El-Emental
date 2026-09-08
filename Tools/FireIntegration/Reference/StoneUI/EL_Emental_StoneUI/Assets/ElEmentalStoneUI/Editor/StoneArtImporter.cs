using UnityEditor;
using UnityEngine;

namespace ElEmental.StoneUI.Editor
{
    public sealed class StoneArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(StoneUIInstaller.Root+"/Art/",System.StringComparison.Ordinal))return;
            var importer=(TextureImporter)assetImporter;
            bool data=assetPath.Contains("/Masks/");bool world=assetPath.Contains("/Environment/");bool preview=assetPath.Contains("/Preview/");
            importer.textureType=world||data||preview?TextureImporterType.Default:TextureImporterType.Sprite;
            importer.sRGBTexture=!data;importer.alphaIsTransparency=!data&&!world&&!preview;importer.mipmapEnabled=world;
            importer.wrapMode=world||assetPath.EndsWith("stone_center_tile.png")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.isReadable=false;
            if(importer.textureType==TextureImporterType.Sprite)
            {
                importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                if(assetPath.Contains("/Buttons/"))importer.spriteBorder=new Vector4(64,25,64,25);
            }
        }
        private void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith(StoneUIInstaller.Root+"/Audio/",System.StringComparison.Ordinal))return;
            var importer=(AudioImporter)assetImporter;var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.preloadAudioData=true;
        }
    }
}
