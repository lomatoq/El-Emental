using UnityEngine;
using Unity.Profiling;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendMusicDirector : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker=new("Elemental.FrontendAudio.Tick");
        private FrontendAudioProfile _profile;
        private MusicBank _menu,_game;
        private FrontendState _context=FrontendState.Loading;
        private bool _gameSettings;
        public float MenuGain=>_menu?.Gain??0;
        public float GameGain=>_game?.Gain??0;
        public int ScheduledLoopCount=>(_menu?.ScheduledCount??0)+(_game?.ScheduledCount??0);
        public int AudibleVoiceCount=>(_menu?.AudibleVoices??0)+(_game?.AudibleVoices??0);
        public void Configure(FrontendAudioProfile profile)
        {
            _profile=profile;
            _menu??=new MusicBank(transform,"Menu music");
            _game??=new MusicBank(transform,"Game music");
        }
        public void SetContext(FrontendState value)
        {
            if(value==FrontendState.Settings && _context!=FrontendState.Settings)
                _gameSettings=_context is FrontendState.Starting or FrontendState.Combat or FrontendState.Paused;
            _context=value;
        }
        public void FadeOut()=>_context=FrontendState.Ending;
        private void Update()
        {
            if(_profile==null || _menu==null)return;
            using(Marker.Auto())
            {
                bool menu=_context is FrontendState.Main or FrontendState.Host or FrontendState.Join;
                bool game=_context is FrontendState.Starting or FrontendState.Combat or FrontendState.Paused;
                // A settings page retains the track of the place it was opened from.
                if(_context==FrontendState.Settings){game=_gameSettings;menu=!game;}
                float gameLevel=_profile.gameVolume*(_context is FrontendState.Paused or FrontendState.Settings?_profile.pausedMusicGain:1);
                _menu.Tick(_profile.mainMenu,menu?_profile.menuVolume:0,_profile,Time.unscaledDeltaTime);
                _game.Tick(_profile.game,game?gameLevel:0,_profile,Time.unscaledDeltaTime);
            }
        }
        private void OnDisable(){_menu?.Stop();_game?.Stop();}

        private sealed class MusicBank
        {
            private readonly AudioSource[] _voices=new AudioSource[2];
            private readonly double[] _starts={double.NegativeInfinity,double.NegativeInfinity};
            private AudioClip _clip;
            private bool _running;
            private double _nextStart,_duration,_overlap;
            private int _nextVoice;
            private float _from,_age;
            public float Gain{get;private set;}
            public float Target{get;private set;}
            public int ScheduledCount{get;private set;}
            public int AudibleVoices=>(_voices[0].volume>.0001f?1:0)+(_voices[1].volume>.0001f?1:0);
            public MusicBank(Transform owner,string name)
            {
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject(name+" "+(i+1));go.transform.SetParent(owner,false);
                    var source=go.AddComponent<AudioSource>();
                    source.playOnAwake=false;source.loop=false;source.spatialBlend=0;
                    source.ignoreListenerPause=true;source.volume=0;source.priority=64;
                    _voices[i]=source;
                }
            }
            public void Tick(AudioClip clip,float target,FrontendAudioProfile profile,float dt)
            {
                if(clip!=_clip){Stop();_clip=clip;}
                target=clip!=null?Mathf.Clamp01(target):0;
                if(!Mathf.Approximately(target,Target)){_from=Gain;Target=target;_age=0;}
                _age+=dt;Gain=FrontendAudioEnvelope.Smooth(_from,Target,_age,profile.musicFadeSeconds);
                if(_clip==null)return;
                if(Target<=0 && Gain<=.00001f){if(_running)Stop();return;}
                double now=AudioSettings.dspTime;
                if(!_running)
                {
                    _duration=(double)_clip.samples/_clip.frequency;
                    _overlap=FrontendAudioEnvelope.Overlap(_duration,profile.loopCrossfadeSeconds);
                    _nextVoice=0;_nextStart=now+.1;_running=true;
                }
                // Prepare the next independent stream before the previous tail fades.
                if(now+.5>=_nextStart && now>=_starts[_nextVoice]+_duration)
                {
                    double start=System.Math.Max(_nextStart,now+.02);
                    var source=_voices[_nextVoice];source.Stop();source.clip=_clip;source.volume=0;
                    _starts[_nextVoice]=start;source.PlayScheduled(start);
                    _nextVoice=1-_nextVoice;_nextStart=start+_duration-_overlap;ScheduledCount++;
                }
                for(int i=0;i<2;i++)
                    _voices[i].volume=Gain*FrontendAudioEnvelope.LoopGain(now-_starts[i],_duration,_overlap);
            }
            public void Stop()
            {
                for(int i=0;i<2;i++){if(_voices[i]!=null){_voices[i].Stop();_voices[i].volume=0;}_starts[i]=double.NegativeInfinity;}
                _running=false;Gain=Target=_from=_age=0;
            }
        }
    }
}
