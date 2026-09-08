using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Presentation.UI
{
    public enum MenuScreenId { Sidebar, Main, Settings, Host, Join, Pause, Victory, Defeat, Draw, Countdown, Returning }

    [Serializable]
    public sealed class MenuElementLayout
    {
        public string path;
        public string displayName;
        [Tooltip("Offset in reference-resolution pixels. Applied on top of the existing animation.")]
        public Vector2 offset;
        [Tooltip("Zero keeps the authored width/height.")]
        public Vector2 size;
        public Vector2 scale = Vector2.one;
        public float rotation;
        [Min(0), Tooltip("Zero keeps the authored type size.")]
        public float fontSize;
        [Tooltip("Left, top, right, bottom. Negative values keep authored padding.")]
        public Vector4 padding = new Vector4(-1,-1,-1,-1);
    }

    [CreateAssetMenu(menuName="Elemental/UI/Menu Screen Layout")]
    public sealed class MenuScreenLayout : ScriptableObject
    {
        public MenuScreenId screen;
        public List<MenuElementLayout> elements = new List<MenuElementLayout>();
        [Header("Local glow and particles")]
        [Range(0,2)] public float glowStrength = .85f;
        [Range(1,40)] public float glowRadius = 18;
        [Range(0,64)] public int particleCount = 28;
        public Vector2 particleSize = new Vector2(1.1f,3.2f);
        [Min(0)] public float particleTravel = 135;
        [Range(0,4)] public float chromaticPixels = 2.2f;
        [Range(0,1)] public float chromaticOpacity = .48f;
        public Color positiveFringe = new Color(.65f,1,.24f,1);
        public Color negativeFringe = new Color(.7f,.23f,1,1);
        [NonSerialized] public int Revision;
        private void OnValidate()
        {
            particleSize.x=Mathf.Max(.1f,particleSize.x);particleSize.y=Mathf.Max(particleSize.x,particleSize.y);
            foreach(var e in elements) if(e!=null)
            {e.size=Vector2.Max(Vector2.zero,e.size);e.scale=Vector2.Max(Vector2.one*.05f,e.scale);}
            Revision++;
        }
        public MenuElementLayout Find(string path)
        {
            MenuElementLayout best=null;
            foreach(var e in elements)if(e!=null&&!string.IsNullOrEmpty(e.path))
            {
                if(e.path==path)return e;
                if(path.EndsWith("/"+e.path,StringComparison.Ordinal)&&(best==null||e.path.Length>best.path.Length))best=e;
            }
            return best;
        }
    }

}
