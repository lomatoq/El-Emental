using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    // Per explicit attack owner; stable ID/generation, bounded storage, no Unity object authority.
    public sealed class FireStoneExposure
    {
        private struct Slot { public uint Id,Generation; public float Dose,Last,Next; }
        private readonly Slot[] slots=new Slot[64];
        public bool LastStepAccepted {get;private set;}
        public int Saturations {get;private set;}
        public void Clear(){System.Array.Clear(slots,0,slots.Length);Saturations=0;}
        public static float RequiredSeconds(float radius,float small,float large)=>radius<=small?.08f:radius<=large?.2f:.4f;
        public static float PushImpulse(float mass,float delta,float energy)=>math.min(24000f,mass*36f)*math.clamp(delta,0,.1f)*math.clamp(energy,0,4);
        public bool Step(uint id,uint generation,float now,float delta,float energy,float radius,float small,float large)
        {
            LastStepAccepted=false;
            if(id==0||!math.isfinite(now)||!math.isfinite(delta)||!math.isfinite(energy)||!math.isfinite(radius)||delta<=0||energy<=0||radius<=0)return false;
            int found=-1,free=-1;
            for(int i=0;i<slots.Length;i++)
            {
                if(slots[i].Id==id&&slots[i].Generation==generation){found=i;break;}
                if(slots[i].Id==0||now-slots[i].Last>.6f)free=i;
            }
            if(found<0){if(free<0){Saturations++;return false;}found=free;slots[found]=new Slot{Id=id,Generation=generation,Last=now-1};}
            ref var slot=ref slots[found];
            if(now<=slot.Last)return false; // Compound colliders cannot multiply one fixed-step exposure.
            if(now-slot.Last>.6f)slot.Dose=0;
            slot.Last=now;LastStepAccepted=true;
            if(now<slot.Next)return false;
            slot.Dose+=math.clamp(delta,0,.1f)*math.clamp(energy,0,4);
            if(slot.Dose+1e-6f<RequiredSeconds(radius,small,large))return false;
            slot.Dose=0;slot.Next=now+.3f;return true;
        }
    }
}
