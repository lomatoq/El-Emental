using Elemental.Simulation.Gravity;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class GravityWorldBehaviour : MonoBehaviour
    {
        [SerializeField] private PointPlanetGravitySource[] sources = System.Array.Empty<PointPlanetGravitySource>();

        public GravityWorld World { get; private set; }
        public bool IsReady => World != null;

        public void Configure(PointPlanetGravitySource[] configuredSources)
        {
            sources = configuredSources ?? System.Array.Empty<PointPlanetGravitySource>();
            if (isActiveAndEnabled)
            {
                Rebuild();
            }
        }

        private void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            GravityWorld rebuilt = new GravityWorld(sources.Length);
            for (int index = 0; index < sources.Length; index++)
            {
                PointPlanetGravitySource source = sources[index];
                if (source != null)
                {
                    rebuilt.Register(source.BuildField());
                }
            }

            World = rebuilt;
        }
    }
}
