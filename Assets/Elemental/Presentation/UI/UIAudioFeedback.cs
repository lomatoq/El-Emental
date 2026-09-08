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
        private bool _panelPending;
        private float _panelCancelGain=1;
        public int PanelMovePlayCount{get;private set;}
        public float Volume { get; set; } = .7f;
        public void Configure(ElementalUITheme theme)
        {
            _theme = theme; _source = GetComponent<AudioSource>();
            _source.playOnAwake = false; _source.spatialBlend = 0; _source.ignoreListenerPause = true;
            if(_panel==null)
            {
                var go=new GameObject("Panel movement sound");go.transform.SetParent(transform,false);
                _panel=go.AddComponent<AudioSource>();_panel.playOnAwake=false;
                _panel.spatialBlend=0;_panel.ignoreListenerPause=true;_panel.volume=0;
            }
        }
        public void PlayPanelMove()
        {
            if(_panel==null || _theme==null || _theme.frontendAudio==null || _theme.frontendAudio.panelMove==null)return;
            if(_panelPending || _panel.isPlaying && AudioSettings.dspTime-_panelStarted<.075)return;
            _panelPending=true;
        }
        private void Update()
        {
            if(_panel==null || _theme==null || _theme.frontendAudio==null)return;
            var profile=_theme.frontendAudio;
            if(_panelPending)
            {
                if(_panel.isPlaying)_panelCancelGain=Mathf.MoveTowards(_panelCancelGain,0,Time.unscaledDeltaTime/.06f);
                if(!_panel.isPlaying || _panelCancelGain<=0)
                {
                    _panel.Stop();_panel.clip=profile.panelMove;_panel.volume=0;
                    _panelStarted=AudioSettings.dspTime;_panelCancelGain=1;_panelPending=false;
                    _panel.Play();PanelMovePlayCount++;
                }
            }
            if(_panel.clip!=null)
                _panel.volume=Mathf.Clamp01(Volume)*profile.panelVolume*_panelCancelGain*
                    FrontendAudioEnvelope.OneShotGain(AudioSettings.dspTime-_panelStarted,_panel.clip.length,
                        profile.panelAttackSeconds,profile.panelReleaseSeconds);
        }
        private void OnDisable(){if(_panel!=null)_panel.Stop();_panelPending=false;}
        public void Play(UIAudioCue cue)
        {
            if (_theme == null || _source == null) return;
            if (cue == UIAudioCue.Hover)
            {
                if (Time.unscaledTimeAsDouble < _nextHover) return;
                _nextHover = Time.unscaledTimeAsDouble + .1;
            }
            AudioClip clip = cue switch {
                UIAudioCue.Hover => _theme.hover, UIAudioCue.Press => _theme.press,
                UIAudioCue.Confirm => _theme.confirm, UIAudioCue.Back => _theme.back,
                UIAudioCue.Error => _theme.error, UIAudioCue.Copy => _theme.copy, _ => _theme.connect };
            if (clip != null) _source.PlayOneShot(clip, Mathf.Clamp01(Volume));
        }
    }
}
