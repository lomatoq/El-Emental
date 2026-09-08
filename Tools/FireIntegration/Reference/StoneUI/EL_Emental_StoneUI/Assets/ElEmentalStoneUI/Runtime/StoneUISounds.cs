using UnityEngine;
namespace ElEmental.StoneUI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class StoneUISounds : MonoBehaviour
    {
        public StoneUIAssets assets;
        [Range(0,1)] public float gain=.4f;
        [Tooltip("Connect to the project's existing UI mixer group on the AudioSource.")]
        public bool enabledSounds=true;
        private AudioSource _source;
        private float _lastHover=-10;
        private void Awake(){_source=GetComponent<AudioSource>();_source.playOnAwake=false;_source.spatialBlend=0;_source.ignoreListenerPause=true;}
        public void Play(string id)
        {
            if(!enabledSounds||assets==null||!isActiveAndEnabled)return;
            if(id=="hover"&&Time.unscaledTime-_lastHover<.06f)return;
            if(id=="hover")_lastHover=Time.unscaledTime;
            if(_source==null)_source=GetComponent<AudioSource>();
            var clip=assets.Sound(id);if(clip!=null)_source.PlayOneShot(clip,gain);
        }
    }
}
