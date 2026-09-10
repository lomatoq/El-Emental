using UnityEngine;

namespace Elemental.Presentation.UI
{
    public sealed class FrontendPreferences
    {
        private const string Prefix = "Elemental.UI.";
        public float MasterVolume { get; private set; } = 1f;
        public float UIVolume { get; private set; } = .7f;
        public float Sensitivity { get; private set; } = 1f;
        public bool ReducedMotion { get; private set; }
        public bool ReducedFlashes { get; private set; }
        public void SetReducedFlashes(bool reduced)=>ReducedFlashes=reduced;
        public void Load() { SetReducedFlashes(PlayerPrefs.GetInt(Prefix + "ReducedFlashes",0)!=0); Set(PlayerPrefs.GetFloat(Prefix + "Master", 1f),
            PlayerPrefs.GetFloat(Prefix + "UI", .7f), PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f),
            PlayerPrefs.GetInt(Prefix + "ReducedMotion", 0) != 0); }
        public void Set(float master, float ui, float sensitivity, bool reduced)
        { MasterVolume = Mathf.Clamp01(master); UIVolume = Mathf.Clamp01(ui); Sensitivity = Mathf.Clamp(sensitivity, .25f, 2f); ReducedMotion = reduced; }
        public void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "Master", MasterVolume); PlayerPrefs.SetFloat(Prefix + "UI", UIVolume);
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", Sensitivity); PlayerPrefs.SetInt(Prefix + "ReducedMotion", ReducedMotion ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "ReducedFlashes", ReducedFlashes ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
