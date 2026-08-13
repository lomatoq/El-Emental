using System;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class EarthStompContactPresenter : MonoBehaviour
    {
        [SerializeField] private EarthPillarMobility pillarMobility;
        [SerializeField] private ParticleSystem contactDust;
        public uint LastContactTick { get; private set; }
        public event Action<uint> ContactPresented;

        public void Configure(EarthPillarMobility configuredMobility, ParticleSystem configuredDust = null)
        {
            if (isActiveAndEnabled && pillarMobility != null) pillarMobility.PillarRaised -= OnPillarRaised;
            pillarMobility = configuredMobility;
            contactDust = configuredDust;
            if (isActiveAndEnabled && pillarMobility != null) pillarMobility.PillarRaised += OnPillarRaised;
        }

        private void OnEnable()
        {
            if (pillarMobility != null) pillarMobility.PillarRaised += OnPillarRaised;
        }

        private void OnDisable()
        {
            if (pillarMobility != null) pillarMobility.PillarRaised -= OnPillarRaised;
        }

        private void OnPillarRaised(EarthPillarLaunchEvent value)
        {
            LastContactTick = value.Tick;
            if (contactDust != null)
            {
                contactDust.transform.SetPositionAndRotation(
                    new Vector3(value.SurfaceBase.x, value.SurfaceBase.y, value.SurfaceBase.z),
                    Quaternion.FromToRotation(Vector3.up,
                        new Vector3(value.LocalUp.x, value.LocalUp.y, value.LocalUp.z)));
                contactDust.Play(true);
            }
            ContactPresented?.Invoke(value.Tick);
        }
    }
}
