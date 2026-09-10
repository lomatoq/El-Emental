using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    public sealed partial class CelestialSystemBehaviour
    {
        private Volume _readabilityVolume;
        private VolumeProfile _readabilityProfile;
        private ColorAdjustments _readabilityColor;
        private ShadowsMidtonesHighlights _readabilityShadows;
        private float _authoredContrast;
        private bool _authoredContrastOverride, _createdReadabilityShadows;
        private string _authoredShadows;
        private Vector4 _authoredShadowValue;
        private bool _authoredShadowsActive, _authoredShadowOverride;

        // The existing camera clarity owner supplies its actual runtime Volume.
        // Celestial remains the only clock; Main may disable charge camera effects.
        public void BindReadabilityVolume(Volume configuredVolume)
        {
            if (_readabilityVolume == configuredVolume && _readabilityProfile != null) return;
            RestoreReadabilityGrading(); _readabilityVolume = configuredVolume;
            if (!Application.isPlaying || configuredVolume == null) return;
            _readabilityProfile = configuredVolume.profile;
            if (_readabilityProfile == null || !_readabilityProfile.TryGet(out _readabilityColor))
            {
                Debug.LogError("Celestial readability requires ColorAdjustments in the existing camera Volume.", this);
                _readabilityProfile = null; return;
            }
            _authoredContrast = _readabilityColor.contrast.value;
            _authoredContrastOverride = _readabilityColor.contrast.overrideState;
            _createdReadabilityShadows = !_readabilityProfile.TryGet(out _readabilityShadows);
            if (_createdReadabilityShadows)
            {
                _readabilityShadows = _readabilityProfile.Add<ShadowsMidtonesHighlights>(true);
                _readabilityShadows.shadowsStart.Override(0);
                _readabilityShadows.shadowsEnd.Override(.3f);
                _readabilityShadows.active = false;
            }
            _authoredShadows = JsonUtility.ToJson(_readabilityShadows);
            _authoredShadowValue = _readabilityShadows.shadows.value;
            _authoredShadowsActive = _readabilityShadows.active;
            _authoredShadowOverride = _readabilityShadows.shadows.overrideState;
            ApplyReadabilityGrading();
        }
        private void ApplyReadabilityGrading()
        {
            if (_readabilityProfile == null)
            {
                if (_readabilityVolume != null) BindReadabilityVolume(_readabilityVolume);
                return;
            }
            float night = Mathf.Clamp01(Snapshot.Night01);
            // The solar key fades before the sky reaches full night. Recover
            // shadows as that actual key disappears; waiting for Night01 alone
            // leaves a dark interval between sunset and established moon fill.
            // Full daylight and full night retain the authored endpoint grading.
            float solarLoss = profile != null && sunLight != null && profile.DaylightIntensity > .001f
                ? 1f - Mathf.Clamp01(sunLight.intensity / profile.DaylightIntensity)
                : night;
            float recovery = Mathf.Max(night, solarLoss);
            _readabilityColor.contrast.value = Mathf.Lerp(_authoredContrast, 0, recovery);
            _readabilityColor.contrast.overrideState = _authoredContrastOverride || recovery > 0;
            Vector4 shadows = _authoredShadowValue;
            // URP's shadow trackball w contributes 4*w to the linear multiplier.
            // Threefold deep-shadow gain at full night; day and highlights retain authored grading.
            shadows.w += .5f * recovery;
            _readabilityShadows.shadows.value = shadows;
            _readabilityShadows.shadows.overrideState = _authoredShadowOverride || recovery > 0;
            _readabilityShadows.active = _authoredShadowsActive || recovery > 0;
        }
        private void RestoreReadabilityGrading()
        {
            if (_readabilityProfile == null) return;
            if (_readabilityColor != null)
            {
                _readabilityColor.contrast.value = _authoredContrast;
                _readabilityColor.contrast.overrideState = _authoredContrastOverride;
            }
            if (_readabilityShadows != null)
            {
                if (_createdReadabilityShadows)
                {
                    _readabilityProfile.Remove<ShadowsMidtonesHighlights>();
                    Destroy(_readabilityShadows);
                }
                else JsonUtility.FromJsonOverwrite(_authoredShadows, _readabilityShadows);
            }
            _readabilityProfile = null; _readabilityColor = null; _readabilityShadows = null;
        }
    }
}
