using System.Collections;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest]
        public IEnumerator BotTelegraphDelegatesOnlyDuringFireAndReturnsAfterRecoveryOrDisable()
        {
            yield return EnterCombat();
            var actor=duel.BotTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var telegraph=duel.BotTransform.GetComponentsInChildren<EarthMvpBotPresenter>(true).Single();
            var controller=duel.BotTransform.GetComponent<EarthMvpBotController>();Assert.That(controller,Is.Not.Null);
            var driver=actor.GetComponent<EarthAnimationDriver>();var session=binding.BotSession;
            Assert.That(actor.PoseController,Is.Not.Null,"Cold preparation must not wait forever for the bot's intentionally external Earth owner.");
            Assert.That(actor.OwnsExternalFirePresentation,Is.False);
            Assert.That((bool)typeof(HumanoidCharacterPresentation).GetField("driveMagicPresentation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor),Is.False);
            // Disabling a MonoBehaviour does not stop PlanetMotor calling its cached input interface.
            // Freeze only planner input; keep production motor/support and both presentation owners running.
            var motor=duel.BotTransform.GetComponent<PlanetMotor>();Assert.That(motor,Is.Not.Null);
            var previousInput=motor.ConfiguredInputSource;
            Assert.That(previousInput,Is.SameAs(controller),"Fixture must detach the actual bot planner input.");
            motor.ConfigureInputSource(null);
            try
            {
            typeof(EarthMvpBotController).GetField("_plannerState",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,new EarthMvpBotPlannerState(EarthMvpBotPhase.Windup,.1f,new float3(0,0,1)));
            int before=telegraph.MagicLayerWriteCount;yield return WaitSeconds(.25f);
            Assert.That(controller.Phase,Is.EqualTo(EarthMvpBotPhase.Windup),"No fixed tick may advance the fixture planner while the input lease is detached.");
            Assert.That(telegraph.MagicLayerWriteCount,Is.GreaterThan(before));Assert.That(driver.GetBool(Animator.StringToHash("Cast")),Is.True);
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.BotTransform.up*8),Is.True);
            Assert.That(actor.OwnsExternalFirePresentation,Is.True);int delegated=telegraph.MagicLayerWriteCount;
            uint generation=session.Generation;
            for(int frame=0;frame<100;frame++)
            {
                session.SetAim(session.MuzzlePosition+duel.BotTransform.up*8);yield return null;
                Assert.That(session.IsActive,Is.True);Assert.That(telegraph.MagicLayerWriteCount,Is.EqualTo(delegated),"Two owners wrote the same magic layer.");
                Assert.That(actor.PoseController.FireChannelSessionGeneration,Is.EqualTo(generation));
            }
            Assert.That(actor.MagicClipTime,Is.GreaterThan(0));Assert.That(actor.LivingHoldWeight,Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(session.MuzzlePosition,actor.Animator.GetBoneTransform(HumanBodyBones.RightHand).position),Is.LessThan(.1f));
            session.Stop();double deadline=Time.realtimeSinceStartupAsDouble+3;
            while(actor.OwnsExternalFirePresentation&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(actor.OwnsExternalFirePresentation,Is.False);yield return WaitSeconds(.25f);
            Assert.That(telegraph.MagicLayerWriteCount,Is.GreaterThan(delegated));Assert.That(driver.GetBool(Animator.StringToHash("Cast")),Is.True);
            Assert.That(actor.PoseController.enabled,Is.False,"The prepared bot pose owner must stay dormant outside Fire.");
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.BotTransform.up*8),Is.True);yield return null;
            binding.enabled=false;Assert.That(session.IsActive,Is.False);Assert.That(actor.OwnsExternalFirePresentation,Is.False);
            int resumed=telegraph.MagicLayerWriteCount;yield return WaitSeconds(.15f);Assert.That(telegraph.MagicLayerWriteCount,Is.GreaterThan(resumed));
            binding.enabled=true;yield return null;Assert.That(session.IsActive,Is.False);
            }
            finally{motor.ConfigureInputSource(previousInput);}
        }
    }
}
