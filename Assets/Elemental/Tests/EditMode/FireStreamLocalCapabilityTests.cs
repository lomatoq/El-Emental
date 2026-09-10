using System;
using Elemental.Presentation.Fire;
using Elemental.Presentation.UI;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class FireStreamLocalCapabilityTests
    {
        [Test] public void NetworkAuthorityNeverEnablesUnreplicatedFireAndOnlyLocalCombatCanOfferIt()
        {
            foreach(FrontendState state in Enum.GetValues(typeof(FrontendState)))
            {
                Assert.That(FireStreamPresentationBinding.SupportsLocalStream(state,true),Is.False,$"Online {state}");
                Assert.That(FireStreamPresentationBinding.SupportsLocalStream(state,false),Is.EqualTo(state==FrontendState.Combat),$"Local {state}");
            }
        }
    }
}
