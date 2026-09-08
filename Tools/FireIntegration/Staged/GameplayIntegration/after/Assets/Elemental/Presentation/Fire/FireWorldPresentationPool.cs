using System;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Fire
{
    // Scene-independent presentation seam. The injected world remains the only domain owner.
    // No emitter, input, damage, thermal command or network state is created here.
    [DisallowMultipleComponent]
    public sealed class FireWorldPresentationPool : MonoBehaviour
    {
        public const int MaximumPresentedGroups = 8;
        private static readonly ProfilerMarker PublishMarker = new ProfilerMarker("Elemental.Fire.WorldPresentation");
        [SerializeField] private FireWorldBehaviour source;
        [SerializeField] private FireVisualProfile profile;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField] private FireLightPool lights;
        private readonly FirePresentationController[] _views = new FirePresentationController[MaximumPresentedGroups];
        private readonly FirePresentationSnapshot[] _snapshots = new FirePresentationSnapshot[MaximumPresentedGroups];
        private readonly FireGroupHandle[] _bound = new FireGroupHandle[MaximumPresentedGroups];
        private int _capacity;
        private bool _initialized;
        public int PresentedGroupCount { get; private set; }
        public string BindingFailure { get; private set; }

        public void Configure(FireWorldBehaviour world, FireVisualProfile settings,
            UnityEngine.Camera camera, FireLightPool lightPool = null)
        {
            if (_initialized) throw new InvalidOperationException("Fire presentation pool bindings are immutable after initialization. Create a separate pool for another world.");
            source = world != null ? world : throw new ArgumentNullException(nameof(world));
            profile = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            lights = lightPool;
            BindingFailure = null;
        }

        private void Start()
        {
            if (source == null || profile == null || viewCamera == null)
                Fail("Bind a FireWorldBehaviour, FireVisualProfile and gameplay camera explicitly before Start.");
        }

        private bool EnsureInitialized()
        {
            if (_initialized) return true;
            if (BindingFailure != null || source == null || !source.IsReady) return false;
            if (profile == null || !profile.IsValid || viewCamera == null)
            { Fail("Fire presentation pool requires a valid visual profile and camera."); return false; }
            if (source.World.Capacity > MaximumPresentedGroups)
            { Fail("Fire presentation pool supports at most eight groups; configure the world admission budget accordingly."); return false; }
            if (profile.MaxLifetime > source.World.Settings.MaximumParticleLifetime ||
                profile.MaximumSpeed > source.World.Settings.MaximumSpeed)
            { Fail("Fire visual lifetime/speed exceed the world's drain/bounds settings. Configure the world from this visual profile."); return false; }
            _capacity = source.World.Capacity;
            for (int slot = 0; slot < _capacity; slot++)
            {
                var child = new GameObject("Fire presentation slot " + slot);
                child.transform.SetParent(transform, false);
                var view = child.AddComponent<FirePresentationController>();
                view.Configure(profile, viewCamera, lights);
                _views[slot] = view; _snapshots[slot] = new FirePresentationSnapshot();
            }
            _initialized = true;
            return true;
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized()) return;
            using (PublishMarker.Auto())
            {
                PresentedGroupCount = 0;
                for (int slot = 0; slot < _capacity; slot++)
                {
                    if (!source.IsReady || !source.World.TryGetHandle(slot, out FireGroupHandle group))
                    { RetireSlot(slot); continue; }
                    if (_bound[slot].IsValid && _bound[slot] != group) RetireSlot(slot);
                    if (!source.CopySnapshot(group, _snapshots[slot])) { RetireSlot(slot); continue; }
                    _bound[slot] = group;
                    _views[slot].Publish(_snapshots[slot]);
                    PresentedGroupCount++;
                }
            }
        }

        public bool TryGetView(FireGroupHandle group, out FirePresentationController view)
        {
            view = null;
            if (!group.IsValid || group.Slot >= _capacity || _bound[group.Slot] != group) return false;
            view = _views[group.Slot]; return view != null;
        }

        private void RetireSlot(int slot)
        {
            if (_bound[slot].IsValid && _views[slot] != null) _views[slot].Retire();
            _bound[slot] = default;
        }
        private void OnDisable()
        {
            for (int slot = 0; slot < _capacity; slot++)
            { RetireSlot(slot); if (_views[slot] != null) _views[slot].gameObject.SetActive(false); }
            PresentedGroupCount = 0;
        }
        private void OnEnable()
        {
            for (int slot = 0; slot < _capacity; slot++)
                if (_views[slot] != null) _views[slot].gameObject.SetActive(true);
        }
        private void OnDestroy()
        {
            for (int slot = 0; slot < _capacity; slot++)
                if (_views[slot] != null) Destroy(_views[slot].gameObject);
        }
        private void Fail(string reason)
        {
            if (BindingFailure == reason) return;
            BindingFailure = reason;
            Debug.LogError(reason, this);
        }
    }
}
