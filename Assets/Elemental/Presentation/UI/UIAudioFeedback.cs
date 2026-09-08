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
        public float Volume { get; set; } = .7f;
        public void Configure(ElementalUITheme theme)
        {
            _theme = theme; _source = GetComponent<AudioSource>();
            _source.playOnAwake = false; _source.spatialBlend = 0; _source.ignoreListenerPause = true;
        }
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
