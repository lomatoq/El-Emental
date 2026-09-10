using System;
using System.Linq;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using UnityEngine;

namespace Elemental.Tests.PlayMode
{
    // Direct-session presentation QA leases raw ingress out of the physical owner's
    // enabled-state list. The physical puppet, support simulation and motor remain live.
    internal sealed class DirectFireInputLease : IDisposable
    {
        private readonly ActiveRagdollPuppet puppet;
        private readonly Behaviour[] originalControls;
        private readonly MagicInputController input;
        private readonly EarthActionRouterBehaviour router;
        private readonly bool inputEnabled, routerEnabled;
        internal DirectFireInputLease(MagicInputController input)
        {
            this.input=input;
            router=input.GetComponent<EarthActionRouterBehaviour>();
            puppet=input.GetComponent<ActiveRagdollPuppet>();
            if(router==null||puppet==null)throw new InvalidOperationException("Expected explicit local input/router/physical puppet owner on player root.");
            originalControls=(Behaviour[])typeof(ActiveRagdollPuppet).GetField("disabledDuringRagdoll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(puppet);
            inputEnabled=input.enabled;routerEnabled=router.enabled;
            puppet.ConfigureControlBehaviours(originalControls.Where(b=>b!=input&&b!=router).ToArray());
            router.enabled=false;input.enabled=false;
        }
        public void Dispose()
        {
            if(puppet!=null)puppet.ConfigureControlBehaviours(originalControls);
            if(router!=null)router.enabled=routerEnabled;
            if(input!=null)input.enabled=inputEnabled;
        }
    }
}
