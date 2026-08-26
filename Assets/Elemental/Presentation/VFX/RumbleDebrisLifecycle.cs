using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>
    /// Visible-debris lifecycle: physical settle, dust pause, gradual sink and
    /// dither fade. Pool/destruction happens only after the renderer is effectively
    /// invisible, never as an abrupt mid-air SetActive(false).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RumbleDebrisLifecycle : MonoBehaviour
    {
        [SerializeField] private float minimumPhysicalSeconds = 1.4f;
        [SerializeField] private float sleepConfirmSeconds = 0.75f;
        [SerializeField] private float sinkSeconds = 0.9f;
        [SerializeField] private float sinkDistance = 0.22f;
        [SerializeField] private float maximumLifetime = 8f;

        private Rigidbody _body;
        private Renderer[] _renderers;
        private readonly List<MaterialPropertyBlock> _blocks = new List<MaterialPropertyBlock>(4);
        private float _age;
        private float _sleepTime;
        private float _sinkTime;
        private bool _sinking;
        private Vector3 _sinkStart;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < _renderers.Length; index++)
                _blocks.Add(new MaterialPropertyBlock());
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (!_sinking)
            {
                bool sleeping = _body == null || _body.IsSleeping() ||
                                (_body.linearVelocity.sqrMagnitude < 0.012f &&
                                 _body.angularVelocity.sqrMagnitude < 0.04f);
                _sleepTime = sleeping ? _sleepTime + Time.deltaTime : 0f;
                if ((_age >= minimumPhysicalSeconds && _sleepTime >= sleepConfirmSeconds) ||
                    _age >= maximumLifetime)
                    BeginSink();
                return;
            }

            _sinkTime += Time.deltaTime;
            float amount = Mathf.Clamp01(_sinkTime / Mathf.Max(0.05f, sinkSeconds));
            float eased = amount * amount * (3f - 2f * amount);
            transform.position = _sinkStart - Vector3.up * sinkDistance * eased;
            float visible = 1f - amount;
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null) continue;
                MaterialPropertyBlock block = _blocks[index];
                renderer.GetPropertyBlock(block);
                block.SetFloat("_Fade", visible);
                renderer.SetPropertyBlock(block);
            }
            if (amount >= 1f) Destroy(gameObject);
        }

        private void BeginSink()
        {
            if (_sinking) return;
            _sinking = true;
            _sinkStart = transform.position;
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.detectCollisions = false;
            }
        }
    }
}
