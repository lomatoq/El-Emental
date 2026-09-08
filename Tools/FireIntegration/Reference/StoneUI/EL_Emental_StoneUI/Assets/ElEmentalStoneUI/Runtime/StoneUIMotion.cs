using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElEmental.StoneUI
{
    public sealed class StoneUIMotion : MonoBehaviour
    {
        [Serializable] private sealed class Preset {public string id;public float duration;public string ease;}
        [Serializable] private sealed class PresetFile {public Preset[] presets;}
        private sealed class Track {public VisualElement view;public float start,duration,from,to;public int kind;public string ease;}
        private readonly List<Track> _tracks=new List<Track>();
        private readonly Dictionary<string,Preset> _presets=new Dictionary<string,Preset>();
        public void Configure(TextAsset file)
        {
            _presets.Clear();if(file==null)return;
            try{var data=JsonUtility.FromJson<PresetFile>(file.text);if(data?.presets!=null)foreach(var p in data.presets)if(!string.IsNullOrEmpty(p.id))_presets[p.id]=p;}
            catch(Exception e){Debug.LogWarning("StoneUI animation preset parse: "+e.Message,this);}
        }
        private void Add(VisualElement v,float from,float to,float fallback,int kind,string preset,bool reduced=false)
        {
            for(int i=_tracks.Count-1;i>=0;--i)if(_tracks[i].view==v&&_tracks[i].kind==kind)_tracks.RemoveAt(i);
            _presets.TryGetValue(preset,out var p);
            _tracks.Add(new Track{view=v,start=Time.unscaledTime,duration=Mathf.Max(.01f,reduced?.08f:p?.duration??fallback),from=from,to=to,kind=kind,ease=p?.ease??"outCubic"});
        }
        public void Reveal(VisualElement v,bool reduced,bool result=false)
        {
            v.style.opacity=0;Add(v,0,1,.28f,0,result?"success":"panel_enter",reduced);
            if(!reduced){v.style.translate=new Translate(0,8,0);Add(v,8,0,.20f,1,"content_enter");}
        }
        public void Press(VisualElement v,bool reduced){if(!reduced)Add(v,v.resolvedStyle.scale.value.x,.984f,.07f,2,"button_press");}
        public void Release(VisualElement v,bool reduced){if(!reduced)Add(v,v.resolvedStyle.scale.value.x,1,.12f,2,"button_release");}
        public void CancelAll()
        {
            foreach(var t in _tracks)if(t.view!=null){t.view.style.opacity=1;t.view.style.translate=new Translate(0,0,0);t.view.style.scale=new Scale(Vector3.one);}_tracks.Clear();
        }
        private void Update()
        {
            for(int i=_tracks.Count-1;i>=0;--i)
            {
                Track t=_tracks[i];if(t.view==null||t.view.panel==null){_tracks.RemoveAt(i);continue;}
                float u=Mathf.Clamp01((Time.unscaledTime-t.start)/t.duration);
                float e=t.ease=="linear"?u:t.ease=="inQuad"?u*u:t.ease=="outQuad"?1-(1-u)*(1-u):t.ease=="sine"?.5f-.5f*Mathf.Cos(u*Mathf.PI):1-Mathf.Pow(1-u,3);
                float v=Mathf.Lerp(t.from,t.to,e);
                if(t.kind==0)t.view.style.opacity=v;else if(t.kind==1)t.view.style.translate=new Translate(0,v,0);else t.view.style.scale=new Scale(new Vector3(v,v,1));
                if(u>=1)_tracks.RemoveAt(i);
            }
        }
        private void OnDisable()=>CancelAll();
    }
}
