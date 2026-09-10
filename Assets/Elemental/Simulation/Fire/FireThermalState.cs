using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public struct FireThermalState
    {
        public float Heat, Char, Remaining;
        public void Ignite(float energy)
        {
            if(!math.isfinite(energy)||energy<=0)return;
            Char=math.saturate(Char+math.min(energy,1)*.10f);
            Heat=math.min(1,Heat+energy*.16f);Remaining=math.max(Remaining,1.2f);
        }
        public void Step(float dt)
        {
            if(!math.isfinite(dt)||dt<=0)return;
            Remaining=math.max(0,Remaining-dt);
            Heat=math.max(0,Heat-dt*(Remaining>0?.09f:.4f));
            Char=Remaining>0?math.saturate(Char+Heat*dt*.35f):math.max(0,Char-dt*.25f);
        }
    }
}
