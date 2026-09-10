using Elemental.Input.Gestures;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Fire;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    public sealed partial class EarthMagicFeedback
    {
        [SerializeField] private MagicInputController responseLocalInput, responseBotInput;
        [SerializeField] private FireStreamSession responseLocalFire, responseBotFire;
        [SerializeField] private Transform responseLocalHand, responseBotHand;
        [SerializeField] private Elemental.Presentation.UI.FrontendFlowController responseFrontend;
        private uint localSchoolGeneration, botSchoolGeneration;
        private float nextLocalContact, nextBotContact;
        private bool responsesSubscribed;
        private bool responseCosmeticsForQa=true;
        public void SetCosmeticsForQa(bool enabled)
        {
            if(responseCosmeticsForQa==enabled)return;
            responseCosmeticsForQa=enabled;
            if(!enabled)UnsubscribePolishResponses();else if(isActiveAndEnabled)SubscribePolishResponses();
        }
        private uint pendingLocalIgnition, pendingBotIgnition;
        private bool localIgnited, botIgnited;
        public int PolishSparkEvents { get; private set; }

        public void ConfigurePolishResponses(MagicInputController localInput, MagicInputController botInput,
            FireStreamSession localFire, FireStreamSession botFire, Transform localHand, Transform botHand,
            Elemental.Presentation.UI.FrontendFlowController frontend)
        {
            UnsubscribePolishResponses();
            responseLocalInput = localInput; responseBotInput = botInput;
            responseLocalFire = localFire; responseBotFire = botFire;
            responseLocalHand = localHand; responseBotHand = botHand;
            responseFrontend = frontend;
            if (isActiveAndEnabled) SubscribePolishResponses();
        }
        private void SubscribePolishResponses()
        {
            if (!responseCosmeticsForQa || responsesSubscribed || materialFeedback == null) return;
            materialFeedback.Presented += OnPolishCue;
            if (responseLocalInput != null) responseLocalInput.SelectedElementChanged += LocalSchool;
            if (responseBotInput != null) responseBotInput.SelectedElementChanged += BotSchool;
            if (responseLocalFire != null) { responseLocalFire.Began += LocalIgnite; responseLocalFire.Ended += LocalEnd; }
            if (responseBotFire != null) { responseBotFire.Began += BotIgnite; responseBotFire.Ended += BotEnd; }
            responsesSubscribed = true;
        }
        private void UnsubscribePolishResponses()
        {
            if (!responsesSubscribed) return;
            if (materialFeedback != null) materialFeedback.Presented -= OnPolishCue;
            if (responseLocalInput != null) responseLocalInput.SelectedElementChanged -= LocalSchool;
            if (responseBotInput != null) responseBotInput.SelectedElementChanged -= BotSchool;
            if (responseLocalFire != null) { responseLocalFire.Began -= LocalIgnite; responseLocalFire.Ended -= LocalEnd; }
            if (responseBotFire != null) { responseBotFire.Began -= BotIgnite; responseBotFire.Ended -= BotEnd; }
            responsesSubscribed = false; nextLocalContact = nextBotContact = 0;
            pendingLocalIgnition = pendingBotIgnition = 0; localIgnited = botIgnited = false;
        }
        private void LocalSchool(ElementId school) => EmitSchool(school, 1, ++localSchoolGeneration, responseLocalHand);
        private void BotSchool(ElementId school) => EmitSchool(school, 2, ++botSchoolGeneration, responseBotHand);
        private void EmitSchool(ElementId school, uint source, uint generation, Transform hand)
        {
            if (hand != null) materialFeedback?.Emit(EarthMaterialFeedbackKind.SchoolSwitch, hand.position, hand.up,
                .5f, .08f, source, generation, 0, 0, school);
        }
        private void LocalIgnite(uint generation, FireGroupHandle group) => pendingLocalIgnition = generation;
        private void BotIgnite(uint generation, FireGroupHandle group) => pendingBotIgnition = generation;
        private void LocalEnd(uint generation)
        {
            pendingLocalIgnition = 0;
            if (localIgnited) EmitFire(EarthMaterialFeedbackKind.FireEnd, responseLocalFire, 1, generation);
            localIgnited = false;
        }
        private void BotEnd(uint generation)
        {
            pendingBotIgnition = 0;
            if (botIgnited) EmitFire(EarthMaterialFeedbackKind.FireEnd, responseBotFire, 2, generation);
            botIgnited = false;
        }
        private void EmitFire(EarthMaterialFeedbackKind kind, FireStreamSession session, uint source, uint generation)
        {
            if (session != null) materialFeedback?.Emit(kind, session.MuzzlePosition, session.transform.up,
                .4f, .08f, source, generation, 0, 0, ElementId.Fire);
        }
        private void UpdatePolishResponses()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.ResponsePoll);
            if (!responseCosmeticsForQa) return;
            ConfirmIgnition(responseLocalFire, 1, ref pendingLocalIgnition, ref localIgnited);
            ConfirmIgnition(responseBotFire, 2, ref pendingBotIgnition, ref botIgnited);
            PollContact(responseLocalFire, 1, ref nextLocalContact);
            PollContact(responseBotFire, 2, ref nextBotContact);
        }
        private void ConfirmIgnition(FireStreamSession session, uint source, ref uint pending, ref bool ignited)
        {
            if (pending == 0) return;
            // A later Began subscriber can reject a lower-priority pose synchronously.
            if (session != null && session.IsActive && session.Generation == pending)
            { EmitFire(EarthMaterialFeedbackKind.FireIgnite, session, source, pending); ignited = true; }
            pending = 0;
        }
        private void PollContact(FireStreamSession session, uint source, ref float next)
        {
            if (session == null || !session.IsActive || !session.HasCoverContact || Time.time < next || Time.deltaTime <= 0) return;
            next = Time.time + EarthResponsePreset.FireContactCooldown;
            materialFeedback?.Emit(EarthMaterialFeedbackKind.FireContact, session.CoverPoint, session.CoverNormal,
                .35f, .08f, source, session.Generation, 0, 0, ElementId.Fire);
        }
        private void OnPolishCue(EarthMaterialFeedbackCue cue)
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.ResponseCue);
            if (sparks == null || !isActiveAndEnabled) return;
            int count = EarthResponsePreset.SparkCount(cue.Kind, cue.Seed);
            if (responseFrontend != null && responseFrontend.Preferences.ReducedMotion) count = Mathf.Min(count, 3);
            // Cosmetic satellites lose admission before any gameplay query at slow frame rates.
            if (Time.unscaledDeltaTime > .045f && count > 1) count = (count + 1) / 2;
            if (count == 0) return;
            // Preserve one existing bounded particle pool, material and camera writer.
            uint random = cue.Seed == 0 ? 1u : cue.Seed;
            Vector3 up = cue.Normal;
            Vector3 tangent = Vector3.Cross(up, Mathf.Abs(up.y) < .9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 side = Vector3.Cross(up, tangent);
            Color color = cue.Element == ElementId.Fire ? new Color(1,.45f,.08f) :
                cue.Element == ElementId.Water ? new Color(.3f,.65f,1) :
                cue.Element == ElementId.Air ? new Color(.7f,.94f,1) : new Color(.76f,.88f,.47f);
            for (int i = 0; i < count; i++)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                float angle = (random & 65535) * (Mathf.PI * 2 / 65536f);
                bool flash = cue.Kind == EarthMaterialFeedbackKind.Impact;
                var p = new ParticleSystem.EmitParams {
                    position = (Vector3)cue.Point + up * .025f,
                    velocity = flash ? Vector3.zero : (up + tangent * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * .55f,
                    startColor = flash ? new Color(1,.8f,.48f,.65f) : color,
                    startSize = flash ? .11f : .027f,
                    startLifetime = flash ? EarthResponsePreset.ImpactFlashSeconds : .24f,
                    randomSeed = random == 0 ? 1u : random
                };
                sparks.Emit(p, 1);
            }
            PolishSparkEvents++;
        }
    }
}
