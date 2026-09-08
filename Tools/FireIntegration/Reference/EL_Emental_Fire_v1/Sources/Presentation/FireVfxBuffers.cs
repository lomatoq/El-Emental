using System;
using UnityEngine;
using UnityEngine.VFX;

namespace Elemental.Presentation.Fire
{
    // Explicit ownership: one instance per live/draining FireGroup.
    // Call from Unity's main thread. No per-particle GPU readback.
    public sealed class FireVfxBuffers : IDisposable
    {
        public const int MaxNodes = 6;
        public const int MaxContacts = 8;
        public const int RowsPerRecord = 6;
        private static readonly int NodesId = Shader.PropertyToID("FireNodes");
        private static readonly int ContactsId = Shader.PropertyToID("FireContacts");
        private static readonly int NodeCountId = Shader.PropertyToID("FireNodeCount");
        private static readonly int ContactCountId = Shader.PropertyToID("FireContactCount");
        private static readonly int OriginId = Shader.PropertyToID("FireOriginWS");
        private static readonly int UpId = Shader.PropertyToID("FireFreeUpWS");
        private static readonly int TimeId = Shader.PropertyToID("FireTime");
        private readonly VisualEffect effect;
        private readonly Vector4[] nodeRows = new Vector4[MaxNodes * RowsPerRecord];
        private readonly Vector4[] contactRows = new Vector4[MaxContacts * RowsPerRecord];
        private GraphicsBuffer nodes;
        private GraphicsBuffer contacts;
        private bool disposed;

        public FireVfxBuffers(VisualEffect target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            effect = target;
            if (!SystemInfo.supportsComputeShaders || SystemInfo.maxComputeBufferInputsVertex <= 0)
                throw new NotSupportedException("This Fire VFX backend requires compute shaders and SSBO support.");
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new NotSupportedException("URP VFX Graph requires Linear colour space; do not silently change project settings.");
            if (!effect.HasGraphicsBuffer(NodesId) || !effect.HasGraphicsBuffer(ContactsId)
                || !effect.HasUInt(NodeCountId) || !effect.HasUInt(ContactCountId)
                || !effect.HasVector3(OriginId) || !effect.HasVector3(UpId)
                || !effect.HasFloat(TimeId))
                throw new InvalidOperationException("Fire VFX graph properties do not match the documented contract.");
            try
            {
                nodes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nodeRows.Length, 16);
                contacts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, contactRows.Length, 16);
                nodes.SetData(nodeRows);
                contacts.SetData(contactRows);
                Rebind();
                effect.SetUInt(NodeCountId, 0u);
                effect.SetUInt(ContactCountId, 0u);
            }
            catch
            {
                nodes?.Dispose();
                contacts?.Dispose();
                throw;
            }
        }

        // Rebind after graph reload/reinitialization; buffer references are not serialized.
        public void Rebind()
        {
            ThrowIfDisposed();
            if (effect == null) throw new InvalidOperationException("The VisualEffect has been destroyed.");
            effect.SetGraphicsBuffer(NodesId, nodes);
            effect.SetGraphicsBuffer(ContactsId, contacts);
        }

        public void Publish(FireGpuNode[] sourceNodes, int nodeCount,
            FireGpuContact[] sourceContacts, int contactCount,
            Vector3 originWS, Vector3 freeUpWS, float simulationEndTime)
        {
            ThrowIfDisposed();
            if (effect == null) throw new InvalidOperationException("The VisualEffect has been destroyed.");
            ValidateCount(sourceNodes, nodeCount, MaxNodes, nameof(sourceNodes));
            ValidateCount(sourceContacts, contactCount, MaxContacts, nameof(sourceContacts));
            if (!Finite(simulationEndTime) || !Finite(originWS.x) || !Finite(originWS.y)
                || !Finite(originWS.z) || !Finite(freeUpWS.x) || !Finite(freeUpWS.y) || !Finite(freeUpWS.z))
                throw new ArgumentException("Fire clock/frame contains NaN or Infinity.");
            Array.Clear(nodeRows, 0, nodeRows.Length);
            Array.Clear(contactRows, 0, contactRows.Length);
            for (int i = 0; i < nodeCount; ++i) sourceNodes[i].Write(nodeRows, i * RowsPerRecord);
            for (int i = 0; i < contactCount; ++i) sourceContacts[i].Write(contactRows, i * RowsPerRecord);
            ValidateRows(nodeRows);
            ValidateRows(contactRows);
            nodes.SetData(nodeRows);
            contacts.SetData(contactRows);
            effect.SetUInt(NodeCountId, (uint)nodeCount);
            effect.SetUInt(ContactCountId, (uint)contactCount);
            effect.SetVector3(OriginId, originWS);
            effect.SetVector3(UpId, freeUpWS.sqrMagnitude > 1e-10f ? freeUpWS.normalized : Vector3.up);
            effect.SetFloat(TimeId, simulationEndTime);
        }

        private static void ValidateCount<T>(T[] array, int count, int max, string name)
        {
            if (array == null) throw new ArgumentNullException(name);
            if (count < 0 || count > max || count > array.Length)
                throw new ArgumentOutOfRangeException(name, "Fire buffer capacity exceeded; use budget admission, not silent truncation.");
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static void ValidateRows(Vector4[] rows)
        {
            for (int i = 0; i < rows.Length; ++i)
                if (!Finite(rows[i].x) || !Finite(rows[i].y) || !Finite(rows[i].z) || !Finite(rows[i].w))
                    throw new ArgumentException("Fire buffer contains NaN or Infinity.");
        }
        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(FireVfxBuffers));
        }
        public void Dispose()
        {
            if (disposed) return;
            // Stop/disable the VisualEffect before disposal. Setting counts alone
            // does not make a still-bound disposed GPU resource valid.
            if (effect != null)
            {
                effect.enabled = false;
                effect.SetUInt(NodeCountId, 0u);
                effect.SetUInt(ContactCountId, 0u);
                effect.SetGraphicsBuffer(NodesId, null);
                effect.SetGraphicsBuffer(ContactsId, null);
            }
            nodes?.Dispose();
            contacts?.Dispose();
            nodes = null;
            contacts = null;
            disposed = true;
        }
    }
}
