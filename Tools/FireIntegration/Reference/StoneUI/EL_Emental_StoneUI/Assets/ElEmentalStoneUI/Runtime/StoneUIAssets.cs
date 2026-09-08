using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElEmental.StoneUI
{
    [CreateAssetMenu(menuName = "EL EMENTAL/Stone UI/Asset library")]
    public sealed class StoneUIAssets : ScriptableObject
    {
        [Serializable] public struct ArtEntry { public string id; public Texture2D texture; }
        [Serializable] public struct ScreenEntry { public string id; public VisualTreeAsset tree; }
        [Serializable] public struct SoundEntry { public string id; public AudioClip clip; }
        public ArtEntry[] art = Array.Empty<ArtEntry>();
        public ScreenEntry[] screens = Array.Empty<ScreenEntry>();
        public SoundEntry[] sounds = Array.Empty<SoundEntry>();
        public StyleSheet theme;
        public Font displayFont;
        public Font bodyFont;
        public Texture2D demoBackground;
        public Shader uiFxShader;
        public TextAsset animationPresets;
        private Dictionary<string, Texture2D> _art;
        private Dictionary<string, VisualTreeAsset> _screens;
        private Dictionary<string, AudioClip> _sounds;

        private void OnEnable() { _art = null; _screens = null; _sounds = null; }
        public Texture2D Texture(string id)
        {
            if (_art == null) { _art = new Dictionary<string, Texture2D>(StringComparer.Ordinal); foreach (var e in art) if (!string.IsNullOrEmpty(e.id)) _art[e.id] = e.texture; }
            return !string.IsNullOrEmpty(id) && _art.TryGetValue(id, out var a) ? a : null;
        }
        public VisualTreeAsset Screen(string id)
        {
            if (_screens == null) { _screens = new Dictionary<string, VisualTreeAsset>(StringComparer.Ordinal); foreach (var e in screens) if (!string.IsNullOrEmpty(e.id)) _screens[e.id] = e.tree; }
            return !string.IsNullOrEmpty(id) && _screens.TryGetValue(id, out var a) ? a : null;
        }
        public AudioClip Sound(string id)
        {
            if (_sounds == null) { _sounds = new Dictionary<string, AudioClip>(StringComparer.Ordinal); foreach (var e in sounds) if (!string.IsNullOrEmpty(e.id)) _sounds[e.id] = e.clip; }
            return !string.IsNullOrEmpty(id) && _sounds.TryGetValue(id, out var a) ? a : null;
        }
    }
}
