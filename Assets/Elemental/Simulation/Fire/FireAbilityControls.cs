using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public enum FireAbilityEffectKind : byte { HandBolt, FootBolt, Impact, Ring, GroundFlame, HandWindup, FootWindup, RingWindup, SphereWave }
    public readonly struct FireAbilityCue
    {
        public readonly uint Id;
        public readonly FireAbilityEffectKind Kind;
        public readonly float3 Position, Direction, Up;
        public readonly float Power, Lifetime;
        public FireAbilityCue(uint id,FireAbilityEffectKind kind,float3 position,float3 direction,float3 up,float power,float lifetime)
        {Id=id;Kind=kind;Position=position;Direction=direction;Up=up;Power=power;Lifetime=lifetime;}
    }
    public readonly struct FireAbilityIntent
    {
        public readonly bool HandBolt,FootBolt,Ring,Flight,LineBegin,LineCommit,Drawing,RingReleased;
        public FireAbilityIntent(bool hand,bool foot,bool ring,bool flight,bool begin,bool commit,bool drawing,bool ringReleased=false)
        {HandBolt=hand;FootBolt=foot;Ring=ring;Flight=flight;LineBegin=begin;LineCommit=commit;Drawing=drawing;RingReleased=ringReleased;}
    }
    public struct FireAbilityControls
    {
        private double lastTap;
        private int taps;
        private bool ringLatched,drawing;
        private float2 lineStart;private float lineTravel;
        public FireAbilityIntent Step(double time,bool primaryPressed,bool primaryHeld,bool secondaryHeld,bool jumpHeld,bool shiftHeld,float2 pointer)
        {
            bool ring=false,begin=false,commit=false,hand=false,foot=false;
            bool chord=jumpHeld&&shiftHeld;
            if(chord&&!ringLatched)ring=true;
            bool ringReleased=ringLatched&&!chord;ringLatched=chord;
            bool both=primaryHeld&&secondaryHeld;
            if(both&&!drawing){drawing=true;lineStart=pointer;lineTravel=0;begin=true;taps=0;}
            else if(!both&&drawing){drawing=false;lineTravel=math.max(lineTravel,math.distance(pointer,lineStart));commit=lineTravel>=18;hand=!commit;taps=0;}
            else if(primaryPressed&&!both)
            {
                taps=time-lastTap<=.30&&time>=lastTap?taps+1:1;lastTap=time;
                hand=taps==2;foot=taps>=3;
            }
            if(both)lineTravel=math.max(lineTravel,math.distance(pointer,lineStart));
            return new FireAbilityIntent(hand,foot,ring,jumpHeld&&!shiftHeld,begin,commit,both,ringReleased);
        }
        public void Reset(){this=default;}
    }
    public static class FireAbilityTuning
    {
        public const int MaximumProjectiles=8,MaximumGroundNodes=12;
        public const float ProjectileSpeed=24,ProjectileLife=2.5f,RingRadius=8,RingSeconds=.65f,LineSeconds=.7f;
        public const float GroundPulseSeconds=.28f,GroundNodeDelay=.035f;
        public const float RingChargeSeconds=.6f, HandChargeSeconds=.25f, FootChargeSeconds=.35f;
        public static float LiftCharge(float heldSeconds)=>math.saturate(heldSeconds/2.5f);
        public static float RingExtent(float elapsed)
        {float t=math.saturate(elapsed/RingSeconds);return RingRadius*(1.6f*t-.6f*t*t);}
        public static float RingSpeed(float elapsed)=>elapsed>=RingSeconds?0:RingRadius/RingSeconds*(1.6f-1.2f*math.saturate(elapsed/RingSeconds));
    }
}
