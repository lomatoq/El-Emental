using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.UI
{
    // Camera post-processing runs before overlay UI. Runtime-owned profile never edits world assets.
    public sealed class FrontendMenuVignette : MonoBehaviour
    {
        private FrontendFlowController _flow;
        private Volume _volume;
        private VolumeProfile _profile;
        public void Configure(FrontendFlowController flow)
        {
            _flow=flow; _volume=gameObject.AddComponent<Volume>();
            _volume.isGlobal=true; _volume.priority=100; _volume.weight=0;
            _profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var vignette=_profile.Add<Vignette>(true);
            vignette.intensity.Override(.48f); vignette.smoothness.Override(.55f);
            vignette.color.Override(new Color(.015f,.01f,.005f));
            _volume.sharedProfile=_profile;
        }
        private void LateUpdate()
        {
            if(_volume==null)return;
            // Main only: settings, pause, countdown and combat retain their original camera effects.
            _volume.weight=_flow!=null&&_flow.State==FrontendState.Main?1:0;
        }
        private void OnDisable(){if(_volume!=null)_volume.weight=0;}
        private void OnDestroy(){if(_profile!=null)Destroy(_profile);}
    }
}
