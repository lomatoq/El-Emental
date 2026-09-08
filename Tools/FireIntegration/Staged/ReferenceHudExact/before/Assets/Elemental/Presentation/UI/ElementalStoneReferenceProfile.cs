using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName="Elemental/UI/Stone Reference Profile")]
    public sealed class ElementalStoneReferenceProfile : ScriptableObject
    {
        public bool enabled=true;
        public TMP_FontAsset menuFont;
        public Font hudFont;
        public Font hudReadableFont;
        public ElementalHudLayout hudLayout;
        public TextAsset animationPresets;
        [Serializable] private sealed class Values
        {
            public float x,y,glow,focus,phase,offset;
            public float scale=1,alpha=1,opacity=1;
            public float[] xKeys;
            public float Read(string channel)
            {
                switch(channel){case "x":return x;case "y":return y;case "scale":return scale;case "alpha":return alpha;case "opacity":return opacity;
                    case "glow":return glow;case "focus":return focus;case "phase":return phase;case "offset":return offset;default:throw new ArgumentException(channel);}
            }
        }
        [Serializable] private sealed class Preset {public string id,ease;public float duration;public bool loop;public Values start,end;}
        [Serializable] private sealed class Catalog {public Preset[] presets;}
        private Catalog _catalog;
        private TextAsset _loaded;
        private Preset Find(string id)
        {
            if(_loaded!=animationPresets||_catalog==null)
            {
                _loaded=animationPresets;
                // The source has a scalar x everywhere except the authored error key sequence.
                // Preserve the file; map that union to a dedicated deserialization field in memory.
                string json=animationPresets!=null?Regex.Replace(animationPresets.text,"\"x\"\\s*:\\s*\\[","\"xKeys\":["):"{}";
                _catalog=JsonUtility.FromJson<Catalog>(json);
            }
            if(_catalog?.presets!=null)foreach(var p in _catalog.presets)if(p.id==id)return p;
            return null;
        }
        public float Duration(string id,float fallback,bool reduced=false)
        {float value=Find(id)?.duration??fallback;return Mathf.Max(.001f,reduced?Mathf.Min(.08f,value):value);}
        public float Evaluate(string id,float t)
        {
            t=Mathf.Clamp01(t);
            switch(Find(id)?.ease){case "linear":return t;case "inQuad":return t*t;case "outQuad":return 1-(1-t)*(1-t);case "sine":return .5f-.5f*Mathf.Cos(t*Mathf.PI);case "smoothstep":return t*t*(3-2*t);default:return OutCubic(t);}
        }
        public float Sample(string id,string channel,float normalizedTime,float fallback=0)
        {
            var preset=Find(id);if(preset?.start==null||preset.end==null)return fallback;
            float t=Evaluate(id,normalizedTime);
            if(channel=="x"&&preset.end.xKeys!=null&&preset.end.xKeys.Length>1)
            {
                var keys=preset.end.xKeys;float index=t*(keys.Length-1);int left=Mathf.Min(keys.Length-2,Mathf.FloorToInt(index));
                return Mathf.Lerp(keys[left],keys[left+1],index-left);
            }
            return Mathf.Lerp(preset.start.Read(channel),preset.end.Read(channel),t);
        }
        public float LoopSample(string id,string channel,float seconds,float fallback=0)
        {
            var preset=Find(id);if(preset==null)return fallback;
            float t=preset.loop?Mathf.PingPong(seconds/Duration(id,1),1):Mathf.Clamp01(seconds/Duration(id,1));
            return Sample(id,channel,t,fallback);
        }
        // Meter endpoints are semantic current/target strings in the source, supplied by the live caller.
        public float SampleValue(string id,float from,float target,float elapsed,bool reduced=false)
            => Mathf.Lerp(from,target,Evaluate(id,elapsed/Duration(id,.14f,reduced)));
        public int PresetCount {get{Find("panel_enter");return _catalog?.presets?.Length??0;}}
        public static float OutCubic(float t)=>1-Mathf.Pow(1-Mathf.Clamp01(t),3);
    }
}
