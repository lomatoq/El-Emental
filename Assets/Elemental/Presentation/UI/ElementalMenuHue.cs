using UnityEngine;
using Elemental.Simulation.Magic;
namespace Elemental.Presentation.UI
{
    // Two owner-local materials recolor existing artwork; neutral detail and alpha survive.
    public static class ElementalMenuHue
    {
        public static Material Create()
        {
            var shader=Resources.Load<Shader>("ElementalMenuHue");
            if(shader==null)throw new System.InvalidOperationException("ElementalMenuHue shader is required for elemental menu plates.");
            return new Material(shader){name="Elemental menu plate hue",hideFlags=HideFlags.DontSave};
        }
        public static Color Accent(ElementId element)=>element==ElementId.Fire?new Color(1,.36f,.06f):
            element==ElementId.Water?new Color(.22f,.72f,1):element==ElementId.Air?new Color(.82f,.9f,1):new Color(.78f,1,.38f);
        public static void Apply(Material material,ElementId element,float sourceHue)
        {
            if(material==null)return;
            float target=element==ElementId.Fire?.045f:element==ElementId.Water?.56f:.57f;
            material.SetFloat("_HueShift",element==ElementId.Earth?0:target-sourceHue);
            material.SetFloat("_SourceHue",sourceHue);
            material.SetFloat("_Saturation",element==ElementId.Air?.12f:1);
        }
    }
}
