using UnityEngine;

namespace Elemental.Presentation.UI
{
    public enum UIAudioCue { Hover, Press, Confirm, Back, Error, Copy, Connect }
    [RequireComponent(typeof(AudioSource))]
    public sealed class UIAudioFeedback : MonoBehaviour
    {
        private AudioSource _source;
        private ElementalUITheme _theme;
        private double _nextHover;
        private AudioSource _panel;
        private double _panelStarted;
        private AudioSource _panelTail;
        private float _panelTailGain;
        private double _panelTailStarted;
        private int _panelStartFrame=-1, _buttonActionDepth;
        private double _panelDuration, _envelopeLead;
        private readonly System.Collections.Generic.Dictionary<AudioClip,AudioClip> _onsetClips=new();
        public int PanelMovePlayCount{get;private set;}
        public int CuePlayCount{get;private set;}
        public UIAudioCue LastPlayedCue{get;private set;}
        public AudioClip LastPlayedClip{get;private set;}
        public float Volume { get; set; } = .7f;
        public void Configure(ElementalUITheme theme)
        {
            _theme = theme; _source = GetComponent<AudioSource>();
            AudioSettings.GetDSPBufferSize(out int bufferFrames,out _);
            _envelopeLead=bufferFrames*.5/System.Math.Max(1,AudioSettings.outputSampleRate);
            _source.playOnAwake = false; _source.spatialBlend = 0; _source.ignoreListenerPause = true;
            PrepareOnsetClip(theme!=null?theme.hover:null);PrepareOnsetClip(theme!=null?theme.press:null);
            if(_panel==null)
            {
                var go=new GameObject("Panel movement sound");go.transform.SetParent(transform,false);
                _panel=go.AddComponent<AudioSource>();_panel.playOnAwake=false;
                _panel.spatialBlend=0;_panel.ignoreListenerPause=true;_panel.volume=0;
                var tail=new GameObject("Panel movement tail");tail.transform.SetParent(transform,false);
                _panelTail=tail.AddComponent<AudioSource>();_panelTail.playOnAwake=false;
                _panelTail.spatialBlend=0;_panelTail.ignoreListenerPause=true;_panelTail.volume=0;
            }
        }
        public void PlayPanelMove()
        {
            if(_panel==null || _theme==null || _theme.frontendAudio==null || _theme.frontendAudio.panelMove==null)return;
            if(_panelStartFrame==Time.frameCount)return;
            _panelStartFrame=Time.frameCount;
            if(_panel.isPlaying)
            {
                _panelTail.Stop();
                var idle=_panelTail;_panelTail=_panel;_panel=idle;
                _panelTailGain=_panelTail.volume;_panelTailStarted=AudioSettings.dspTime;
            }
            var profile=_theme.frontendAudio;
            _panel.clip=profile.panelMove;_panelStarted=AudioSettings.dspTime;
            int firstSample=Mathf.Clamp(Mathf.RoundToInt(profile.panelStartOffsetSeconds*_panel.clip.frequency),0,Mathf.Max(0,_panel.clip.samples-1));
            _panel.timeSamples=firstSample;
            _panelDuration=(_panel.clip.samples-firstSample)/(double)_panel.clip.frequency;
            _panel.volume=Mathf.Clamp01(Volume)*profile.panelVolume*
                FrontendAudioEnvelope.OneShotGain(_envelopeLead,_panelDuration,profile.panelAttackSeconds,profile.panelReleaseSeconds);
            _panel.Play();PanelMovePlayCount++;
        }
        private void Update()
        {
            if(_panel==null || _theme==null || _theme.frontendAudio==null)return;
            var profile=_theme.frontendAudio;
            if(_panelTail!=null && _panelTail.isPlaying)
            {
                float gain=1-Mathf.Clamp01((float)(AudioSettings.dspTime-_panelTailStarted)/.06f);
                _panelTail.volume=_panelTailGain*gain;
                if(gain<=0)_panelTail.Stop();
            }
            if(_panel.clip!=null)
                _panel.volume=Mathf.Clamp01(Volume)*profile.panelVolume*
                    FrontendAudioEnvelope.OneShotGain(AudioSettings.dspTime-_panelStarted+_envelopeLead,_panelDuration,
                        profile.panelAttackSeconds,profile.panelReleaseSeconds);
        }
        private void OnDisable(){if(_panel!=null)_panel.Stop();if(_panelTail!=null)_panelTail.Stop();_panelStartFrame=-1;}
        private void PrepareOnsetClip(AudioClip source)
        {
            if(source==null||_onsetClips.ContainsKey(source))return;
            // Only the two short interaction cues are prepared, once per assigned clip.
            // Never modify their files or interrupt a one-shot during Configure.
            var samples=new float[source.samples*source.channels];
            if(!source.GetData(samples,0)){_onsetClips.Add(source,source);return;}
            int limit=Mathf.Min(source.samples,Mathf.CeilToInt(source.frequency*.08f));
            int onset=-1;
            for(int frame=0;frame<limit&&onset<0;frame++)
                for(int channel=0;channel<source.channels;channel++)
                    if(Mathf.Abs(samples[frame*source.channels+channel])>.001f){onset=frame;break;}
            int preroll=Mathf.CeilToInt(source.frequency*.002f);
            int first=Mathf.Max(0,onset-preroll);
            if(first==0){_onsetClips.Add(source,source);return;}
            int frames=source.samples-first;
            var data=new float[frames*source.channels];
            System.Array.Copy(samples,first*source.channels,data,0,data.Length);
            for(int frame=0;frame<Mathf.Min(frames,preroll);frame++)
                for(int channel=0;channel<source.channels;channel++)data[frame*source.channels+channel]*=frame/(float)preroll;
            var clip=AudioClip.Create(source.name+" (immediate UI onset)",frames,source.channels,source.frequency,false);
            clip.hideFlags=HideFlags.HideAndDontSave;clip.SetData(data,0);_onsetClips.Add(source,clip);
        }
        private void OnDestroy()
        {
            if(_source!=null)_source.Stop();
            foreach(var pair in _onsetClips)
                if(pair.Value!=null&&pair.Value!=pair.Key){if(Application.isPlaying)Destroy(pair.Value);else DestroyImmediate(pair.Value);}
            _onsetClips.Clear();
        }
        public void InvokeButtonAction(UnityEngine.Events.UnityAction action)
        {
            _buttonActionDepth++;
            try{action?.Invoke();}finally{_buttonActionDepth--;}
        }
        public void Play(UIAudioCue cue)
        {
            if (_theme == null || _source == null) return;
            // PointerDown / Submit owns the button sound. A resulting flow transition
            // must not add another confirm after the press animation has already begun.
            if(cue==UIAudioCue.Confirm && _buttonActionDepth>0)return;
            if (cue == UIAudioCue.Hover)
            {
                if (Time.unscaledTimeAsDouble < _nextHover) return;
                _nextHover = Time.unscaledTimeAsDouble + .1;
            }
            AudioClip clip = cue switch {
                UIAudioCue.Hover => _theme.hover, UIAudioCue.Press => _theme.press,
                UIAudioCue.Confirm => _theme.confirm, UIAudioCue.Back => _theme.back,
                UIAudioCue.Error => _theme.error, UIAudioCue.Copy => _theme.copy, _ => _theme.connect };
            if(clip!=null&&(cue==UIAudioCue.Hover||cue==UIAudioCue.Press)&&_onsetClips.TryGetValue(clip,out var immediate))clip=immediate;
            if (clip != null){_source.PlayOneShot(clip, Mathf.Clamp01(Volume));CuePlayCount++;LastPlayedCue=cue;LastPlayedClip=clip;}
        }
    }
}
