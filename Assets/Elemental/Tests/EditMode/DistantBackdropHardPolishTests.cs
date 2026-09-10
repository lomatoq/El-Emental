using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class DistantBackdropHardPolishTests
    {
        [TestCase(8f)]
        [TestCase(35f)]
        [TestCase(120f)]
        [TestCase(240f)]
        public void LevitationUsesActualBodyHeightAndStaysInsideTwoPercent(float height)
        {
            float amplitude=DistantBackdrop.FloatingAmplitude(height,new Vector2(1.5f,3.5f));
            Assert.That(amplitude/height,Is.InRange(.005f,.020001f));
            Assert.That(amplitude,Is.LessThanOrEqualTo(3.5f));
        }
        [Test]
        public void FullMotionEnvelopeContainsEveryCornerAcrossYawRockAndRadialDrift()
        {
            var bounds=new Bounds(new Vector3(.1f,.35f,-.05f),new Vector3(.9f,1.2f,.7f));
            const float scale=150,amplitude=2.7f,horizontal=.3f,angle=.65f;
            Vector3 position=new Vector3(230,90,-270);
            Quaternion original=Quaternion.Euler(0,23,0);
            Bounds envelope=DistantBackdrop.FloatingMotionEnvelope(bounds,scale,position,original,amplitude,horizontal,angle);
            Vector3 up=new Vector3(.2f,.9f,.3f).normalized;
            Vector3 tangent=Vector3.Cross(up,Vector3.forward).normalized,bitangent=Vector3.Cross(up,tangent).normalized;
            for(int sample=0;sample<720;sample++)
            {
                float t=sample*.73f;
                Quaternion rotation=Quaternion.Euler(0,sample*.5f,0)*original*Quaternion.Euler(Mathf.Sin(t*.7f)*angle,0,Mathf.Cos(t)*angle);
                Vector3 shift=up*(Mathf.Sin(t)*amplitude)+
                    (tangent*Mathf.Sin(t*.61f)+bitangent*Mathf.Sin(t*.37f))*amplitude*horizontal;
                Vector3 pivot=position+shift+original*(bounds.center*scale)-rotation*(bounds.center*scale);
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 local=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    Assert.That(envelope.SqrDistance(pivot+rotation*(local*scale)),Is.LessThan(1e-7f),$"sample {sample}, corner {corner}");
                }
            }
        }
    }
}
