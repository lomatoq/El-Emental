using System.Collections;
using System.IO;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishSchoolInputTests
    {
        [UnityTest,Timeout(240000)] public IEnumerator ActualMouseWeavesPowerShootsChargesAndCancelsHeldInput()
        {
            var settings=InputSystem.settings;var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            float capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;
            try
            {
                yield return KeyTap(Key.Digit1);yield return null;yield return new WaitForEndOfFrame();var ability=binding.PlayerAbilities;
                Assert.That(magic.SelectedElement,Is.EqualTo(Elemental.Simulation.Magic.ElementId.Fire),InputStateEvidence);
                string lastStatus="";magic.StatusChanged+=s=>lastStatus=s;
                var inputCamera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>()[0].TargetCamera;
                var playerMotor=duel.PlayerTransform.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();
                Vector2 pointer=inputCamera.WorldToScreenPoint(inputCamera.transform.position+playerMotor.LocalUp*30+playerMotor.FacingForward*15);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return null;yield return new WaitForEndOfFrame();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();yield return new WaitForFixedUpdate();yield return new WaitForEndOfFrame();
                Assert.That(binding.PlayerSession.IsActive,Is.True,$"MMB owns hand stream; selected={magic.SelectedElement} field={duel.PlayerTransform.GetComponentInChildren<Elemental.Input.Actions.EarthInputAdapter>(true).BendFieldHeld} suppressed={magic.SchoolPrimarySuppressed} eligible={magic.WorldPointerEligible} available={binding.PlayerSession.IsAvailable} weave={ability.WeaveHeld} status={lastStatus}");Assert.That(binding.PlayerSession.Power,Is.EqualTo(1).Within(.01));
                for(int i=0;i<3;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();}
                Assert.That(ability.WeaveForm,Is.EqualTo(FireWeaveForm.Orbit),InputStateEvidence+" power="+magic.FirePower01);Assert.That(binding.PlayerSession.IsActive,Is.False);Assert.That(ability.WeaveHeld,Is.True);
                int shots=ability.Shots;InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle).WithButton(MouseButton.Left));yield return null;yield return new WaitForEndOfFrame();
                Assert.That(ability.Shots,Is.EqualTo(shots+1));Assert.That(ability.IsBoltCharging,Is.False);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle));for(int i=0;i<10;i++)yield return null;yield return new WaitForEndOfFrame();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Middle).WithButton(MouseButton.Right));yield return null;yield return new WaitForEndOfFrame();
                Assert.That(ability.Shots,Is.EqualTo(shots+2));bool drill=false;for(int i=0;i<ability.ProjectileCapacity;i++)drill|=ability.GetProjectile(i).Drill;Assert.That(drill,Is.True);
                for(int i=0;i<3;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer,scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));yield return null;yield return new WaitForEndOfFrame();}
                Assert.That(ability.WeaveForm,Is.EqualTo(FireWeaveForm.Sphere));
                flow.Pause();yield return null;yield return new WaitForEndOfFrame();Assert.That(ability.WeaveHeld,Is.False);flow.Resume();yield return null;yield return new WaitForEndOfFrame();
                Assert.That(ability.WeaveHeld,Is.False,"Holding through pause cannot rearm the shield");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<15;i++)yield return null;yield return new WaitForEndOfFrame();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer}.WithButton(MouseButton.Right));yield return null;yield return new WaitForEndOfFrame();Assert.That(ability.IsBoltCharging,Is.True);
                for(int i=0;i<90;i++)yield return null;yield return new WaitForEndOfFrame();Assert.That(ability.BoltCharge01,Is.EqualTo(1).Within(.02),$"Held charge available={ability.IsAvailable} state={flow.State} status={lastStatus}");
                shots=ability.Shots;InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});yield return null;yield return new WaitForEndOfFrame();
                Assert.That(ability.IsBoltCharging,Is.False);Assert.That(ability.Shots,Is.EqualTo(shots+1));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;yield return new WaitForEndOfFrame();Assert.That(ability.IsLifting,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return new WaitForEndOfFrame();Assert.That(ability.IsLifting,Is.False);
            }
            finally{Time.captureDeltaTime=capture;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;}
        }
    }
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)] public IEnumerator OrbitalMuzzleChecksActualProjectileWidthAndStartingOverlap()
        {
            GameObject wall=null;
            try
            {
                yield return ReadyFireAbilities();Vector3 up=flightMotor.LocalUp,forward=flightMotor.FacingForward,side=Vector3.Cross(up,forward).normalized;
                Vector3 from=flightAbility.OwnerRoot.position+up*12,to=from+forward*2;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=from+forward+side*.5f;wall.transform.localScale=Vector3.one*.16f;
                UnityEngine.Physics.SyncTransforms();
                var check=typeof(Elemental.Runtime.Fire.FireAbilityController).GetMethod("ClearMuzzle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Assert.That((bool)check.Invoke(flightAbility,new object[]{from,to,.12f}),Is.True,"The centerline is intentionally clear");
                Assert.That((bool)check.Invoke(flightAbility,new object[]{from,to,.65f}),Is.False,"Actual large drill envelope touches the wall");
                wall.transform.position=from;UnityEngine.Physics.SyncTransforms();
                Assert.That((bool)check.Invoke(flightAbility,new object[]{from,to,.12f}),Is.False,"Starting inside a wall cannot relocate beyond it");
            }
            finally{if(wall!=null)Object.Destroy(wall);ReleaseFireAbilityFixture();}
        }
        [UnityTest,Timeout(240000)] public IEnumerator ShortContourExpiresFromOldestAnchorWithoutResidualGroundIgnition()
        {
            float capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;
            try
            {
                yield return ReadyFireAbilities();Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
                Vector3 origin=flightMotor.SupportFeetPoint(up)+side*1.4f;uint first=0,last=0;
                flightAbility.Effect+=c=>{if(c.Kind==FireAbilityEffectKind.GroundFlame){if(first==0)first=c.Id;last=c.Id;}};
                flightAbility.BeginContour();Assert.That(flightAbility.TraceContour(origin),Is.True);
                for(int i=0;i<48;i++)yield return null;yield return new WaitForEndOfFrame();
                Assert.That(flightAbility.TraceContour(origin+side*.9f),Is.True);Assert.That(last,Is.Not.EqualTo(first));
                for(int i=0;i<40;i++)yield return null;yield return new WaitForEndOfFrame();
                Assert.That(flightAbility.TryGetGroundPose(first,out _,out _),Is.False,"The old tail expires first");
                Assert.That(flightAbility.TryGetGroundPose(last,out _,out _),Is.True,"The newer head still burns briefly");
                for(int i=0;i<48;i++)yield return null;yield return new WaitForEndOfFrame();
                Assert.That(flightAbility.SourceCount,Is.Zero,"The complete drawn trace expires in order");
            }
            finally{Time.captureDeltaTime=capture;ReleaseFireAbilityFixture();}
        }
        [UnityTest,Timeout(240000)] public IEnumerator DrawnFireSourcesAmplifyActualProjectileOnceAndExpire()
        {
            float capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;
            try
            {
                yield return ReadyFireAbilities();Vector3 up=flightMotor.LocalUp;
                Vector3 side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
                Vector3 ground=flightMotor.SupportFeetPoint(up)+side*1.7f;
                flightAbility.BeginContour();bool admitted=flightAbility.TraceContour(ground);
                if(!admitted){ground=flightMotor.SupportFeetPoint(up)-side*1.7f;flightAbility.BeginContour();admitted=flightAbility.TraceContour(ground);}
                Assert.That(admitted,Is.True,"Actual arena surface supports drawing");
                flightAbility.TraceContour(ground+flightMotor.FacingForward*.8f);Assert.That(flightAbility.SourceCount,Is.GreaterThan(0));
                for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();
                int before=flightAbility.Amplifications;flightAbility.SetWeaveHeld(true,FireWeaveForm.Flood,.33f);
                Assert.That(flightAbility.TryRapidShot(ground+up*.35f),Is.True);
                for(int i=0;i<40;i++)yield return null;yield return new WaitForEndOfFrame();
                Assert.That(flightAbility.Amplifications,Is.EqualTo(before+1),"One passing fireball catalyses exactly one bounded burst");
                flightAbility.SetWeaveHeld(false,FireWeaveForm.Flood,.33f);
                for(int i=0;i<330;i++)yield return null;yield return new WaitForEndOfFrame();
                Assert.That(flightAbility.SourceCount,Is.Zero,"No detached permanent source after expiry");
                Assert.That(flightAbility.Amplifications,Is.EqualTo(before+1),"Residual fire does not recursively multiply itself");
            }
            finally{Time.captureDeltaTime=capture;ReleaseFireAbilityFixture();}
        }
        [UnityTest,Timeout(240000)] public IEnumerator FireSphereDeflectsIncomingPhysicalBodyAndCancelClearsState()
        {
            GameObject stone=null;float capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;
            try
            {
                yield return ReadyFireAbilities();Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
                stone=GameObject.CreatePrimitive(PrimitiveType.Sphere);stone.name="Fire guard incoming physical regression";stone.transform.localScale=Vector3.one*.3f;
                Vector3 center=flightAbility.OwnerRoot.position+up;
                stone.transform.position=center+side*3.4f;var body=stone.AddComponent<Rigidbody>();body.useGravity=false;body.mass=4;body.linearVelocity=-side*120;
                flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);UnityEngine.Physics.SyncTransforms();
                for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
                Assert.That(flightAbility.GuardDeflections,Is.GreaterThan(0));Assert.That(Vector3.Dot(body.linearVelocity,side),Is.GreaterThan(0));
                flightAbility.CancelAll();Assert.That(flightAbility.WeaveHeld||flightAbility.IsBoltCharging,Is.False);Assert.That(flightAbility.SourceCount,Is.Zero);
            }
            finally{if(stone!=null)Object.Destroy(stone);Time.captureDeltaTime=capture;ReleaseFireAbilityFixture();}
        }
    }
}
