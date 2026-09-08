using System;
using System.Collections;
using System.IO;
using Elemental.Presentation.UI;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class FrontendAudioRuntimeTests
    {
        private GameObject _root;
        private FrontendAudioProfile _profile;
        private ElementalUITheme _theme;
        private AudioClip _menu, _game, _panel;
        private float _timeScale;
        private bool _listenerPaused;

        [SetUp] public void Setup()
        {
            _timeScale = Time.timeScale; _listenerPaused = AudioListener.pause;
            Time.timeScale = 0f; AudioListener.pause = false;
            _root = new GameObject("Frontend audio focused fixture");
            _root.AddComponent<AudioListener>();
            _profile = ScriptableObject.CreateInstance<FrontendAudioProfile>();
            _profile.musicFadeSeconds = .35f; _profile.loopCrossfadeSeconds = .15f;
            _menu = Tone("Menu test tone", .7f, 220);
            _game = Tone("Game test tone", .8f, 330);
            _panel = Tone("Panel test tone", .5f, 440);
            _profile.mainMenu = _menu; _profile.game = _game; _profile.panelMove = _panel;
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(_root);
            if (_theme != null) Object.DestroyImmediate(_theme);
            Object.DestroyImmediate(_profile);
            Object.DestroyImmediate(_menu); Object.DestroyImmediate(_game); Object.DestroyImmediate(_panel);
            Time.timeScale = _timeScale; AudioListener.pause = _listenerPaused;
        }
        private static AudioClip Tone(string name, float seconds, float frequency)
        {
            const int rate = 44100;
            var samples = new float[Mathf.CeilToInt(seconds * rate)];
            for (int i = 0; i < samples.Length; i++) samples[i] = .001f * Mathf.Sin(2f * Mathf.PI * frequency * i / rate);
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            Assert.That(clip.SetData(samples, 0), Is.True);
            return clip;
        }
        private static IEnumerator WaitReal(float seconds)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < end) yield return null;
        }

        [UnityTest] public IEnumerator MusicLoopsAndChangesContextWhileScaledTimeIsFrozen()
        {
            var director = _root.AddComponent<FrontendMusicDirector>(); director.Configure(_profile);
            director.SetContext(FrontendState.Main);
            double dspStart = AudioSettings.dspTime, deadline = Time.realtimeSinceStartupAsDouble + 2.4;
            int maxVoices = 0, samples = 0; bool played = false;
            long totalNs = 0, peakNs = 0;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.FrontendAudio.Tick", 512);
            AudioSource[] sources = _root.GetComponentsInChildren<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(4));
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                maxVoices = Math.Max(maxVoices, director.AudibleVoiceCount);
                foreach (var source in sources) played |= source.isPlaying;
                if (recorder.Valid && recorder.LastValue > 0)
                { totalNs += recorder.LastValue; peakNs = Math.Max(peakNs, recorder.LastValue); samples++; }
            }
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioSettings.dspTime - dspStart, Is.GreaterThan(1), "The DSP clock must advance for scheduled playback.");
            Assert.That(played, Is.True, "At least one real AudioSource must play the generated clip.");
            Assert.That(director.ScheduledLoopCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(maxVoices, Is.GreaterThanOrEqualTo(2), "Independent outgoing/incoming streams must overlap.");
            Assert.That(director.MenuGain, Is.EqualTo(_profile.menuVolume).Within(.001f));
            Assert.That(director.GameGain, Is.Zero);
            foreach (var source in sources) { Assert.That(source.loop, Is.False); Assert.That(source.ignoreListenerPause, Is.True); }
            director.SetContext(FrontendState.Combat);
            yield return WaitReal(.13f);
            Assert.That(director.MenuGain, Is.GreaterThan(0)); Assert.That(director.GameGain, Is.GreaterThan(0));
            yield return WaitReal(.4f);
            Assert.That(director.MenuGain, Is.Zero);
            Assert.That(director.GameGain, Is.EqualTo(_profile.gameVolume).Within(.001f));
            director.SetContext(FrontendState.Paused); yield return WaitReal(.4f);
            Assert.That(director.GameGain, Is.EqualTo(_profile.gameVolume * _profile.pausedMusicGain).Within(.001f));
            director.SetContext(FrontendState.Settings); yield return WaitReal(.4f);
            Assert.That(director.MenuGain, Is.Zero);
            Assert.That(director.GameGain, Is.EqualTo(_profile.gameVolume * _profile.pausedMusicGain).Within(.001f));
            director.SetContext(FrontendState.Main); yield return WaitReal(.4f);
            Assert.That(director.MenuGain, Is.EqualTo(_profile.menuVolume).Within(.001f));
            Assert.That(director.GameGain, Is.Zero);
            director.FadeOut(); yield return WaitReal(.4f);
            Assert.That(director.MenuGain, Is.Zero); Assert.That(director.GameGain, Is.Zero);
            foreach (var source in sources) Assert.That(source.isPlaying, Is.False);
            Directory.CreateDirectory("BuildReports/FrontendAudio");
            File.WriteAllText("BuildReports/FrontendAudio/runtime.txt", $"UTC={DateTime.UtcNow:O}; loops={director.ScheduledLoopCount}; maxOverlapVoices={maxVoices}; markerSamples={samples}; meanMs={(samples>0?totalNs/(double)samples/1000000d:0):F5}; peakMs={peakNs/1000000d:F5}");
        }

        [UnityTest] public IEnumerator PanelUsesQuietSinglePlaybackAndFadesOutWhilePaused()
        {
            _theme = ScriptableObject.CreateInstance<ElementalUITheme>();
            _theme.frontendAudio = _profile; _theme.confirm = _panel;
            var feedback = _root.AddComponent<UIAudioFeedback>(); feedback.Configure(_theme); feedback.Volume = .7f;
            feedback.PlayPanelMove(); feedback.PlayPanelMove();
            Assert.That(feedback.PanelMovePlayCount,Is.EqualTo(1),"Playback must be dispatched in the initiating frame, without waiting for Update.");
            Assert.That(_root.transform.Find("Panel movement sound").GetComponent<AudioSource>().volume,Is.GreaterThan(0),"First audio buffer must not wait for the next rendered frame's envelope update.");
            yield return null;
            Assert.That(feedback.PanelMovePlayCount, Is.EqualTo(1), "Same-frame layout events must coalesce into one sound.");
            var panel = _root.transform.Find("Panel movement sound").GetComponent<AudioSource>();
            yield return WaitReal(.08f);
            Assert.That(panel.isPlaying, Is.True);
            Assert.That(panel.volume, Is.GreaterThan(0));
            Assert.That(panel.volume, Is.LessThanOrEqualTo(feedback.Volume * _profile.panelVolume + .0001f));
            Assert.That(_profile.panelVolume, Is.LessThan(_profile.menuVolume));
            feedback.Play(UIAudioCue.Confirm);
            yield return null;
            Assert.That(_root.GetComponent<AudioSource>().isPlaying, Is.True, "The ordinary confirm one-shot remains active alongside panel movement.");
            yield return WaitReal(.65f);
            Assert.That(feedback.PanelMovePlayCount, Is.EqualTo(1));
            Assert.That(panel.volume, Is.Zero); Assert.That(panel.isPlaying, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
        }
        [UnityTest] public IEnumerator PanelReversalStartsWhilePreviousVoiceRetires()
        {
            _theme=ScriptableObject.CreateInstance<ElementalUITheme>();_theme.frontendAudio=_profile;
            var feedback=_root.AddComponent<UIAudioFeedback>();feedback.Configure(_theme);
            feedback.PlayPanelMove();yield return WaitReal(.08f);
            var oldVoice=_root.transform.Find("Panel movement sound").GetComponent<AudioSource>();
            Assert.That(oldVoice.isPlaying,Is.True);
            feedback.PlayPanelMove();
            Assert.That(feedback.PanelMovePlayCount,Is.EqualTo(2),"A reversal must not wait for the outgoing voice's 60 ms fade.");
            Assert.That(oldVoice.isPlaying,Is.True);Assert.That(oldVoice.volume,Is.GreaterThan(0));
            var newVoice=_root.transform.Find("Panel movement tail").GetComponent<AudioSource>();
            Assert.That(newVoice.volume,Is.GreaterThan(0));
            Assert.That(newVoice.timeSamples,Is.GreaterThanOrEqualTo(Mathf.FloorToInt(_profile.panelStartOffsetSeconds*_panel.frequency)));
            yield return WaitReal(.09f);Assert.That(oldVoice.isPlaying,Is.False);
        }
        [UnityTest] public IEnumerator PointerAudioStartsImmediatelyAndActionDoesNotAddLateConfirm()
        {
            _theme=ScriptableObject.CreateInstance<ElementalUITheme>();_theme.hover=_panel;_theme.press=_panel;_theme.confirm=_panel;
            var audio=_root.AddComponent<UIAudioFeedback>();audio.Configure(_theme);
            var go=new GameObject("Audio button",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
            go.transform.SetParent(_root.transform,false);
            go.GetComponent<UnityEngine.UI.Button>().targetGraphic=go.GetComponent<UnityEngine.UI.Image>();
            var button=go.AddComponent<FrontendButton>();button.Configure(_theme,audio,Color.white,Color.yellow);
            var pointer=new UnityEngine.EventSystems.PointerEventData(null){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            button.OnPointerEnter(pointer);Assert.That(audio.CuePlayCount,Is.EqualTo(1));Assert.That(audio.LastPlayedCue,Is.EqualTo(UIAudioCue.Hover));
            button.OnPointerDown(pointer);Assert.That(audio.CuePlayCount,Is.EqualTo(2));Assert.That(audio.LastPlayedCue,Is.EqualTo(UIAudioCue.Press));
            audio.InvokeButtonAction(()=>audio.Play(UIAudioCue.Confirm));Assert.That(audio.CuePlayCount,Is.EqualTo(2));
            audio.Play(UIAudioCue.Confirm);Assert.That(audio.CuePlayCount,Is.EqualTo(3),"Non-button confirmations remain available.");
            yield return null;
        }
        [UnityTest] public IEnumerator InteractionOnsetIsPreparedOnceWithoutChangingSourceAndReleasedOnDestroy()
        {
            const int rate=48000,frames=9600,channels=2,onset=1920;
            var data=new float[frames*channels];
            for(int i=onset;i<frames;i++)data[i*channels+1]=.25f*Mathf.Sin((i-onset)*.1f);
            var source=AudioClip.Create("Silent preroll stereo cue",frames,channels,rate,false);source.SetData(data,0);
            try
            {
                _theme=ScriptableObject.CreateInstance<ElementalUITheme>();_theme.hover=source;_theme.press=source;
                var feedback=_root.AddComponent<UIAudioFeedback>();feedback.Configure(_theme);
                feedback.Play(UIAudioCue.Press);
                Assert.That(feedback.CuePlayCount,Is.EqualTo(1),"Press playback is issued before yielding a frame.");
                var immediate=feedback.LastPlayedClip;
                Assert.That(immediate,Is.Not.SameAs(source));Assert.That(immediate.channels,Is.EqualTo(channels));
                var rendered=new float[immediate.samples*channels];Assert.That(immediate.GetData(rendered,0),Is.True);
                int first=-1;for(int i=0;i<rendered.Length;i++)if(Mathf.Abs(rendered[i])>.001f){first=i/channels;break;}
                Assert.That(first,Is.InRange(1,rate/250),"Retain at most 4 ms including safe attack/preroll, not the source's 40 ms silence.");
                feedback.Configure(_theme);feedback.Play(UIAudioCue.Press);
                Assert.That(feedback.LastPlayedClip,Is.SameAs(immediate),"Configure and subsequent presses reuse the prepared clip.");
                var unchanged=new float[data.Length];source.GetData(unchanged,0);CollectionAssert.AreEqual(data,unchanged);
                Object.Destroy(_root);yield return null;yield return null;
                Assert.That(immediate==null,Is.True,"Owned onset clips must be released with the feedback owner.");
                Assert.That(source!=null,Is.True);
            }
            finally{Object.DestroyImmediate(source);}
        }
    }
}
