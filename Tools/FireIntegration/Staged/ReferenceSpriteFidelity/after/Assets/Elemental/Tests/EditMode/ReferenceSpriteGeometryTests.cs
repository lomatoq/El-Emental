using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Elemental.Presentation.UI;

namespace Elemental.Tests.EditMode
{
    public sealed class ReferenceSpriteGeometryTests
    {
        private const string Root="Assets/Elemental/Content/UI/Stone/ReferenceSprites/";
        [TestCase("selected-construction",2138,736)]
        [TestCase("passive-button",2172,724)]
        [TestCase("active-element-card",2135,736)]
        [TestCase("element-glyphs",2172,724)]
        [TestCase("air-glyph",1254,1254)]
        public void ReferenceArtPreservesNativePixelsAndActualAlpha(string name,int width,int height)
        {
            string path=Root+name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.maxTextureSize,Is.GreaterThanOrEqualTo(Mathf.Max(width,height)));
            var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(imported.width,Is.EqualTo(width));Assert.That(imported.height,Is.EqualTo(height));
            // Decode a temporary CPU texture; production textures stay non-readable.
            var probe=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(probe.LoadImage(File.ReadAllBytes(path)),Is.True);
                int transparent=0,opaque=0;
                foreach(var pixel in probe.GetPixels32()){if(pixel.a==0)transparent++;if(pixel.a>240)opaque++;}
                Assert.That(transparent,Is.GreaterThan(width*height/10),"A drawn checkerboard or black backdrop is not alpha.");
                Assert.That(opaque,Is.GreaterThan(width*height/10),"The intended UI foreground must survive extraction.");
            }
            finally{Object.DestroyImmediate(probe);}
        }
        [Test]
        public void WholeConstructionHasPositiveFaceAndOverflowInsets()
        {
            var skin=AssetDatabase.LoadAssetAtPath<ElementalStoneSkin>("Assets/Elemental/Content/UI/Stone/ElementalStoneSkin.asset");
            Assert.That(skin.referenceSelected,Is.Not.Null);Assert.That(skin.referenceNormal,Is.Not.Null);Assert.That(skin.referenceCard,Is.Not.Null);
            var r=skin.referenceSelected.rect;var i=skin.referenceSelectedInsets;
            Assert.That(i.x,Is.GreaterThan(100));Assert.That(i.y,Is.GreaterThan(100));
            Assert.That(r.width-i.x-i.z,Is.GreaterThan(500));Assert.That(r.height-i.y-i.w,Is.GreaterThan(100));
            float density=(r.height-i.y-i.w)/78;
            Assert.That(i.x/density,Is.InRange(40f,80f));Assert.That(i.y/density,Is.InRange(40f,90f));
        }
    }
}
