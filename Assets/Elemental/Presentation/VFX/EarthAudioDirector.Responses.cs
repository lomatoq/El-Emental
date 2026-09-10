using Elemental.Runtime.Fire;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using UnityEngine;
using Unity.Profiling;

namespace Elemental.Presentation.VFX
{
    public sealed partial class EarthAudioDirector
    {
        [SerializeField] private EarthMaterialFeedbackHub responseHub;
        [SerializeField] private FireStreamSession localResponseFire, botResponseFire;
        private readonly EarthResponseAdmission _impactAdmission = new EarthResponseAdmission();
        private readonly EarthResponseAdmission _cueAdmission = new EarthResponseAdmission();
        private AudioSource[] responseVoices, fireLoops;
        private AudioClip ignitionClip, tailClip, schoolClip, fireLoopClip;
        private int responseVoice;
        private bool responseAudioSubscribed;
        private bool responseCosmeticsForQa=true;
        public void SetCosmeticsForQa(bool enabled)
        {
            if(responseCosmeticsForQa==enabled)return;
            responseCosmeticsForQa=enabled;
            if(!enabled)UnsubscribeResponseAudio();else if(isActiveAndEnabled)SubscribeResponseAudio();
        }
        private static readonly ProfilerMarker ResponseAudioMarker = new ProfilerMarker("Elemental.Response.Audio");
        public int ResponseAudioEvents { get; private set; }
        public int ActiveFireLoops => fireLoops == null ? 0 : (fireLoops[0].isPlaying ? 1 : 0) + (fireLoops[1].isPlaying ? 1 : 0);
        public void ConfigureResponseAudio(EarthMaterialFeedbackHub hub, FireStreamSession local, FireStreamSession bot)
        {
            UnsubscribeResponseAudio(); responseHub = hub; localResponseFire = local; botResponseFire = bot;
            if (Application.isPlaying && isActiveAndEnabled) SubscribeResponseAudio();
        }
        private void SubscribeResponseAudio()
        {
            if (!responseCosmeticsForQa || responseAudioSubscribed || responseHub == null) return;
            EnsureResponseAudio(); responseHub.Presented += OnResponseAudio; responseAudioSubscribed = true;
        }
        private void UnsubscribeResponseAudio()
        {
            if (responseAudioSubscribed && responseHub != null) responseHub.Presented -= OnResponseAudio;
            responseAudioSubscribed = false; _impactAdmission.Reset(); _cueAdmission.Reset();
            if (responseVoices != null) foreach (var source in responseVoices) if (source != null) source.Stop();
            if (fireLoops != null) foreach (var source in fireLoops) if (source != null) { source.Stop(); source.volume = 0; }
        }
        private void EnsureResponseAudio()
        {
            if (responseVoices != null) return;
            responseVoices = new AudioSource[4]; fireLoops = new AudioSource[2];
            for (int i = 0; i < 4; i++) responseVoices[i] = CreateSource("Response transient " + i);
            ignitionClip = CreateClip("Fire ignition snap", 1, .085f);
            tailClip = CreateClip("Fire release hiss", 2, .22f);
            schoolClip = CreateClip("School select", 1, .06f);
            const int rate = 22050, samples = 11025;
            var data = new float[samples]; uint seed = 0x13579BDFu; float filtered = 0;
            for (int i = 0; i < samples; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                filtered = Mathf.Lerp(filtered, (seed & 65535) / 32767.5f - 1, .13f);
                float seam = Mathf.Min(1, Mathf.Min(i, samples - 1 - i) / 128f);
                data[i] = filtered * .3f * seam;
            }
            fireLoopClip = AudioClip.Create("Restrained fire channel", samples, 1, rate, false); fireLoopClip.SetData(data, 0);
            for (int i = 0; i < 2; i++)
            {
                fireLoops[i] = CreateSource("Fire channel " + i); fireLoops[i].loop = true;
                fireLoops[i].clip = fireLoopClip; fireLoops[i].volume = 0;
            }
        }
        private void OnResponseAudio(EarthMaterialFeedbackCue cue)
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.ResponseAudioCue);
            using var marker = ResponseAudioMarker.Auto();
            if (!EarthResponsePreset.IsSignal(cue.Kind) || cue.Kind == EarthMaterialFeedbackKind.FireContact) return;
            if (!_cueAdmission.Admit(cue.SourceId ^ ((uint)cue.Kind << 24), cue.Generation, Time.time, Time.frameCount)) return;
            EnsureResponseAudio();
            var voice = responseVoices[responseVoice++ % responseVoices.Length];
            voice.transform.position = cue.Point; voice.loop = false;
            voice.clip = cue.Kind == EarthMaterialFeedbackKind.FireIgnite ? ignitionClip :
                cue.Kind == EarthMaterialFeedbackKind.FireEnd ? tailClip : schoolClip;
            voice.pitch = .95f + (cue.Seed % 101) * .001f;
            voice.volume = masterVolume * (cue.Kind == EarthMaterialFeedbackKind.SchoolSwitch ? .14f : .23f);
            voice.Play(); ResponseAudioEvents++;
        }
        private void Update()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.ResponseAudioUpdate);
            using var marker = ResponseAudioMarker.Auto();
            if (!responseAudioSubscribed || fireLoops == null) return;
            UpdateFireLoop(localResponseFire, fireLoops[0]); UpdateFireLoop(botResponseFire, fireLoops[1]);
        }
        private void UpdateFireLoop(FireStreamSession session, AudioSource source)
        {
            // Audio lifecycle observes the authority even if the final transient missed a busy frame's budget.
            bool live = session != null && session.IsActive && Time.timeScale > 0;
            if (session != null) source.transform.position = session.MuzzlePosition;
            float target = live ? masterVolume * .18f : 0;
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 1.8f);
            if (live && !source.isPlaying) source.Play();
            else if (!live && source.volume <= .001f) source.Stop();
        }
        private void DestroyResponseAudio()
        {
            UnsubscribeResponseAudio();
            if (ignitionClip != null) Destroy(ignitionClip);
            if (tailClip != null) Destroy(tailClip);
            if (schoolClip != null) Destroy(schoolClip);
            if (fireLoopClip != null) Destroy(fireLoopClip);
        }
    }
}
