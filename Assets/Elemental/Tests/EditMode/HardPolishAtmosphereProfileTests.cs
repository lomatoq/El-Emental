using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishAtmosphereProfileTests
    {
        [Test] public void InvalidAerialMultiplierCannotReachTheFogPublisher()
        {
            var profile=ScriptableObject.CreateInstance<ValleyAtmosphereProfile>();
            try
            {
                profile.CloudArt=Texture2D.whiteTexture;
                foreach(float value in new[]{.7f,.88f,1f}){profile.MidAerialOpacityMultiplier=value;Assert.That(profile.IsValid,Is.True);}
                foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,.69f,1.01f})
                {profile.MidAerialOpacityMultiplier=value;Assert.That(profile.IsValid,Is.False);}
            }
            finally{Object.DestroyImmediate(profile);}
        }
    }
}
