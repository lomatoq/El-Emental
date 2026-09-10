using System;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Elemental.Simulation.Fields;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    public sealed partial class MagicInputController
    {
        [SerializeField] private FireStreamSession fireStream;
        private FireAbilityController fireAbilities;
        private FireWeaveControls fireWeave;
        public float FirePower01=>fireWeave.Power01;
        public string FireFormLabel=>fireWeave.Form.ToString().ToUpperInvariant();
        private bool fireJumpSuppressed,fireRingJumpConsumed;
                private bool fireStreamPrimarySuppressed,fireStreamRequested;
        private readonly RaycastHit[] fireGroundAimHits = new RaycastHit[64];
        public FireAbilityController FireAbilities => fireAbilities;
        public bool ConsumesFireJump => selectedElement == ElementId.Fire && fireAbilities != null;
        public void ConfigureFireAbilities(FireAbilityController value) { fireAbilities=value; fireWeave.Interrupt(); }
        private bool SchoolMouseHeld=>inputAdapter!=null&&(inputAdapter.BendPrimaryHeld||inputAdapter.BendForceHeld||inputAdapter.BendFieldHeld);
        private bool _schoolPrimarySuppressed;
        private bool _inputFocus = true;
        private bool _applicationPaused;
        private Func<Vector2,bool> _worldPointerEligibility;
        public bool SchoolInputContextAvailable=>_inputFocus&&!_applicationPaused&&Time.timeScale>0;
        public void ConfigureWorldPointerEligibility(Func<Vector2,bool> eligibility)=>_worldPointerEligibility=eligibility;
        public bool WorldPointerEligible=>inputAdapter==null||_worldPointerEligibility==null||_worldPointerEligibility(inputAdapter.PointerPixels);
        private int _schoolRoutingFrame = -1;
        private readonly RaycastHit[] _fireAimHits = new RaycastHit[32];
        public FireStreamSession FireStream => fireStream;
        public bool SchoolPrimarySuppressed => _schoolPrimarySuppressed;
        public event Action<ElementId> SelectedElementChanged;

        public void ConfigureFireStream(FireStreamSession configuredSession)
        {
            if (fireStream != configuredSession) StopFireInput(true);
            fireStream = configuredSession;
        }

        public bool IsElementAvailable(ElementId element)
        {
            if (element == ElementId.Earth) return executor != null;
            if (element == ElementId.Fire && fireStream != null) return fireStream.IsAvailable;
            // Lab executors are still usable in their own scenes, never advertised as duel capabilities.
            if (duelController != null) return false;
            return element == ElementId.Air ? airExecutor != null :
                (element == ElementId.Fire || element == ElementId.Water) && thermalWaterExecutor != null;
        }

        public bool TrySelectElement(ElementId element)
        {
            if (!IsElementAvailable(element))
            {
                ReportStatus(element.ToString().ToUpperInvariant() + " UNAVAILABLE");
                return false;
            }
            if (selectedElement == element) return true;
            CancelInteraction();
            actionRouter?.CancelForElementSwitch();
            selectedElement = element;
            _selectedAbility = element == ElementId.Fire ? FireAbilityIds.HeatJet :
                element == ElementId.Water ? WaterAbilityIds.GatherWater :
                element == ElementId.Air ? AirAbilityIds.GustCorridor : EarthAbilityIds.LineWall;
            _schoolPrimarySuppressed = inputAdapter != null && SchoolMouseHeld;
            if(element==ElementId.Fire)fireWeave.Interrupt();
            _suppressPrimaryUntilReleased = false;
            SelectedElementChanged?.Invoke(element);
            ReportStatus(element == ElementId.Fire ? "FIRE / SHIFT + MMB · WHEEL POWER" : element.ToString().ToUpperInvariant());
            return true;
        }

        public void PrepareElementRouting()
        {
            if (_schoolRoutingFrame == Time.frameCount || inputAdapter == null) return;
            _schoolRoutingFrame = Time.frameCount;
            if (_schoolPrimarySuppressed && !SchoolMouseHeld) _schoolPrimarySuppressed = false;
            if(inputAdapter.BendPrimaryHeld&&!WorldPointerEligible)
            {
                if(!_schoolPrimarySuppressed){CancelInteraction();actionRouter?.CancelForElementSwitch();}
                _schoolPrimarySuppressed=true;
            }
            if (AcceptsDuelCommands && SchoolInputContextAvailable) UpdateElementSelection();
        }

        private void StopFireInput(bool suppressHeld)
        {
            if (fireStream != null && fireStream.IsActive) fireStream.Stop();
            if(suppressHeld)
            {
                fireAbilities?.SetSelected(false); fireWeave.Interrupt();
                fireStreamPrimarySuppressed=false;fireStreamRequested=false;fireRingJumpConsumed=false;
                fireJumpSuppressed=inputAdapter!=null&&inputAdapter.JumpHeld;
            }
            if (suppressHeld && inputAdapter != null && SchoolMouseHeld)
                _schoolPrimarySuppressed = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            _inputFocus = focused;
            if (!focused) { CancelInteraction(); actionRouter?.CancelForElementSwitch(); }
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused=paused;
            if (paused) { CancelInteraction(); actionRouter?.CancelForElementSwitch(); }
        }

        private void UpdateFireInput()
        {
            if (!SchoolInputContextAvailable || !AcceptsDuelCommands || !fireStream.IsAvailable)
            { StopFireInput(true); return; }
            if(!WorldPointerEligible){StopFireInput(true);return;}
            if(!inputAdapter.BendFieldHeld)fireStreamPrimarySuppressed=false;
            if (_schoolPrimarySuppressed) return;
            if (castCamera == null) { StopFireInput(true); return; }
            _aimScreenPosition = inputAdapter.PointerPixels;
            Ray aimRay = castCamera.ScreenPointToRay(_aimScreenPosition);
            Vector3 aim = aimRay.GetPoint(projectionDistance);
            int count = UnityEngine.Physics.RaycastNonAlloc(aimRay, _fireAimHits, projectionDistance, ~0, QueryTriggerInteraction.Ignore);
            if(count==_fireAimHits.Length){StopFireInput(true);ReportStatus("FIRE AIM OVERLOADED / RELEASE BUTTONS");return;}
            float nearest = projectionDistance;RaycastHit aimedHit=default;
            for (int i = 0; i < count; i++)
            {
                var hit = _fireAimHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance; aim = hit.point;aimedHit=hit;
            }
            if(fireAbilities==null){StopFireInput(true);return;}
            fireAbilities.SetSelected(true);
            if(!inputAdapter.JumpHeld){fireJumpSuppressed=false;fireRingJumpConsumed=false;}
            var intent=fireWeave.Step(inputAdapter.BendPrimaryHeld,inputAdapter.BendForceHeld,inputAdapter.BendFieldHeld,
                inputAdapter.BendParameter,inputAdapter.JumpHeld&&!fireJumpSuppressed,inputAdapter.BendModifierHeld);
            if(intent.Ring&&fireAbilities.TryRing())fireRingJumpConsumed=true;
            if(intent.RingRelease)fireAbilities.ReleaseRing();
            fireAbilities.SetLiftHeld(intent.Flight&&!fireRingJumpConsumed);
            fireAbilities.SetLowFlightHeld(!fireWeave.IsBlocked &&
                Elemental.Simulation.Characters.FireLowFlightMotion.WantsFlight(inputAdapter.BendModifierHeld,inputAdapter.Move.y,
                    inputAdapter.BendPrimaryHeld,inputAdapter.BendForceHeld,inputAdapter.BendFieldHeld,inputAdapter.JumpHeld));
            fireAbilities.SetWeaveHeld(inputAdapter.BendFieldHeld&&inputAdapter.BendModifierHeld&&!fireWeave.IsBlocked,fireWeave.Form,fireWeave.Power01);
            if(intent.SphereBurst)fireAbilities.TryReleaseSphereWave();
            if(intent.ChargeCancel)fireAbilities.CancelBoltCharge();
            if(intent.ChargeBegin&&!fireAbilities.BeginBoltCharge(aim))ReportStatus("FIREBALL NOT READY");
            fireAbilities.SetChargedAim(aim);
            if(intent.ChargeRelease)fireAbilities.ReleaseBoltCharge();
            if(intent.RapidShot)fireAbilities.TryRapidShot(aim);
            if(intent.Drill)fireAbilities.TryDrill(aim);
            if(intent.DrawBegin)fireAbilities.BeginContour();
            if(intent.Draw)
            {
                if((aimedHit.collider==null||Vector3.Dot(aimedHit.normal,fireAbilities.LocalUp)>.45f)&&TryResolveFireGroundPointer(aimRay,out Vector3 point))fireAbilities.TraceContour(point);
                if(aimedHit.collider!=null&&Vector3.Dot(aimedHit.normal,fireAbilities.LocalUp)<=.45f)fireAbilities.PaintContact(aimedHit);
            }
            if(!intent.Stream){fireStreamRequested=false;StopFireInput(false);return;}
            fireStream.SetPower(FireWeaveTuning.StreamPower(fireWeave.Power01));
            if(fireStreamRequested&&!fireStream.IsActive)fireStreamPrimarySuppressed=true;
            fireStreamRequested=true;
            if(fireStreamPrimarySuppressed)return;
            if(fireStream.IsActive)fireStream.SetAim(aim);
            else if(!fireStream.TryBegin(aim))
            {fireStreamPrimarySuppressed=true;ReportStatus("FIRE NOT READY / RELEASE MMB");}
        }
        private bool TryResolveFireGroundPointer(Ray ray,out Vector3 point)
        {
            point=default;
            if(_motor==null) return false;
            Vector3 up=_motor.LocalUp.normalized;
            Vector3 feet=_motor.SupportFeetPoint(up);
            int count=UnityEngine.Physics.RaycastNonAlloc(ray,fireGroundAimHits,projectionDistance,
                _motor.GroundMask,QueryTriggerInteraction.Ignore);
            if(count==fireGroundAimHits.Length) return false;
            float closest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=fireGroundAimHits[i];
                if(!FireGroundHitAllowed(hit,up)||Vector3.Distance(hit.point,_motor.transform.position)>12f||hit.distance>=closest)continue;
                point=hit.point;closest=hit.distance;
            }
            if(float.IsFinite(closest)) return true;
            // A wall/sky aim is not an elevated 200m line endpoint. Project the
            // pointer onto the current tangent plane, then require real support.
            if(!new Plane(up,feet).Raycast(ray,out float travel)||!float.IsFinite(travel)) return false;
            Vector3 candidate=feet+Vector3.ClampMagnitude(Vector3.ProjectOnPlane(ray.GetPoint(travel)-feet,up),11.8f);
            count=UnityEngine.Physics.RaycastNonAlloc(candidate+up*5f,-up,fireGroundAimHits,12f,
                _motor.GroundMask,QueryTriggerInteraction.Ignore);
            if(count==fireGroundAimHits.Length) return false;
            closest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=fireGroundAimHits[i];
                if(!FireGroundHitAllowed(hit,up)||hit.distance>=closest)continue;
                point=hit.point;closest=hit.distance;
            }
            return float.IsFinite(closest);
        }

        private bool FireGroundHitAllowed(RaycastHit hit,Vector3 up)=>hit.collider!=null&&
            !hit.collider.transform.IsChildOf(transform)&&
            hit.collider.GetComponentInParent<Elemental.Runtime.Characters.PlanetMotor>()==null&&
            Vector3.Dot(hit.normal,up)>.45f;
    }
}
