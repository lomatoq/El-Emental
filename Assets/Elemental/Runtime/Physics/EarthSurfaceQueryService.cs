using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public interface IEarthSurfaceProvider
    {
        bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample);
        bool IsCurrent(in EarthSurfaceHandle handle);
    }

    [DisallowMultipleComponent]
    public sealed class EarthSurfaceQueryService : MonoBehaviour
    {
        private static readonly ProfilerMarker QueryMarker =
            new ProfilerMarker("Elemental.Earth.Surface.Query");
        private const int MaximumProviders = 32;

        private readonly IEarthSurfaceProvider[] _providers = new IEarthSurfaceProvider[MaximumProviders];
        private int _providerCount;

        public int ProviderCount => _providerCount;

        public bool Register(IEarthSurfaceProvider provider)
        {
            if (provider == null) return false;
            for (int index = 0; index < _providerCount; index++)
                if (ReferenceEquals(_providers[index], provider)) return true;
            if (_providerCount >= _providers.Length)
            {
                Debug.LogError($"Earth surface provider budget exceeded ({MaximumProviders}).", this);
                return false;
            }
            _providers[_providerCount++] = provider;
            return true;
        }

        public void Unregister(IEarthSurfaceProvider provider)
        {
            if (provider == null) return;
            for (int index = 0; index < _providerCount; index++)
            {
                if (!ReferenceEquals(_providers[index], provider)) continue;
                int move = _providerCount - index - 1;
                if (move > 0) System.Array.Copy(_providers, index + 1, _providers, index, move);
                _providers[--_providerCount] = null;
                return;
            }
        }

        public bool TrySample(in EarthSurfaceQuery query, out EarthSurfaceSample sample)
        {
            using (QueryMarker.Auto())
            {
                sample = default;
                if (!query.IsValid) return false;
                for (int index = 0; index < _providerCount; index++)
                {
                    IEarthSurfaceProvider provider = _providers[index];
                    if (provider == null || !provider.TrySample(in query, out EarthSurfaceSample candidate))
                        continue;
                    if (!EarthSurfaceSelection.IsBetter(
                            in candidate, in sample, query.RequiredCapabilities)) continue;
                    sample = candidate;
                }
                return sample.IsValid;
            }
        }

        public bool IsCurrent(in EarthSurfaceHandle handle)
        {
            if (!handle.IsValid) return false;
            for (int index = 0; index < _providerCount; index++)
            {
                IEarthSurfaceProvider provider = _providers[index];
                if (provider != null && provider.IsCurrent(in handle)) return true;
            }
            return false;
        }

        internal static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
