using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using UnityEngine;

namespace Elemental.Tests.PlayMode
{
    // Deterministic motor commands own this fixture's ingress. The physical
    // puppet otherwise restores saved controls even when setup disabled a bot.
    internal sealed class LocomotionProductionIngressLease : IDisposable
    {
        private readonly List<(ActiveRagdollPuppet puppet, Behaviour[] controls)> owners = new();
        private readonly List<(Behaviour control, bool enabled)> states = new();
        public LocomotionProductionIngressLease(IEnumerable<PlanetMotor> motors)
        {
            foreach (PlanetMotor motor in motors)
            {
                var controls = motor.GetComponents<Behaviour>().Where(b =>
                    b is EarthMvpBotController || b is MagicInputController || b is EarthActionRouterBehaviour).ToArray();
                var puppet = motor.GetComponent<ActiveRagdollPuppet>();
                if (puppet != null)
                {
                    var original = (Behaviour[])typeof(ActiveRagdollPuppet).GetField("disabledDuringRagdoll",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(puppet);
                    owners.Add((puppet, original));
                    puppet.ConfigureControlBehaviours(original.Where(b => !controls.Contains(b)).ToArray());
                }
                foreach (Behaviour control in controls)
                {
                    states.Add((control, control.enabled));
                    control.enabled = false;
                }
            }
        }
        public void Dispose()
        {
            foreach (var owner in owners) if (owner.puppet != null) owner.puppet.ConfigureControlBehaviours(owner.controls);
            foreach (var state in states) if (state.control != null) state.control.enabled = state.enabled;
        }
    }
}
