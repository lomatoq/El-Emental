using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>Explicit camera-local perception source and hostile character meshes.</summary>
    [DisallowMultipleComponent]
    public sealed class EarthSeismicCameraTargets : MonoBehaviour
    {
        public readonly struct Target
        {
            public readonly Renderer Renderer;
            public readonly int SubmeshCount;
            public Target(Renderer renderer, int submeshCount)
            { Renderer = renderer; SubmeshCount = submeshCount; }
        }

        private EarthSeismicVision _vision;
        private Target[] _targets = Array.Empty<Target>();
        private readonly List<SkinnedMeshRenderer> _skins = new();
        private readonly List<bool> _oldUpdateOffscreen = new();
        public Target[] Targets => _targets;
        public float Blend => _vision != null && _vision.isActiveAndEnabled ? _vision.VisualBlend : 0f;

        public void Configure(EarthSeismicVision vision, IReadOnlyList<Renderer> renderers)
        {
            RestoreSkins();
            _vision = vision;
            var targets = new List<Target>(renderers.Count);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = skin.sharedMesh;
                    _skins.Add(skin);
                    _oldUpdateOffscreen.Add(skin.updateWhenOffscreen);
                    skin.updateWhenOffscreen = true;
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;
                if (mesh != null) targets.Add(new Target(renderer, mesh.subMeshCount));
            }
            _targets = targets.ToArray();
        }

        private void RestoreSkins()
        {
            for (int i = 0; i < _skins.Count; i++)
                if (_skins[i] != null) _skins[i].updateWhenOffscreen = _oldUpdateOffscreen[i];
            _skins.Clear();
            _oldUpdateOffscreen.Clear();
        }

        private void OnDestroy() => RestoreSkins();
    }
}
