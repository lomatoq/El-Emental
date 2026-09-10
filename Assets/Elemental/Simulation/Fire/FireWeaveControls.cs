using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public enum FireWeaveForm : byte { Jet, Flood, Orbit, Sphere }
    public readonly struct FireWeaveIntent
    {
        public readonly bool Stream, RapidShot, Drill, Draw, DrawBegin, ChargeBegin, ChargeRelease, ChargeCancel, Ring, RingRelease, Flight, SphereBurst;
        public FireWeaveIntent(bool stream,bool rapid,bool drill,bool draw,bool drawBegin,bool chargeBegin,bool chargeRelease,bool chargeCancel,bool ring,bool ringRelease,bool flight,bool sphereBurst=false)
        {Stream=stream;RapidShot=rapid;Drill=drill;Draw=draw;DrawBegin=drawBegin;ChargeBegin=chargeBegin;ChargeRelease=chargeRelease;ChargeCancel=chargeCancel;Ring=ring;RingRelease=ringRelease;Flight=flight;SphereBurst=sphereBurst;}
    }
    // Mouse ownership is latched on an edge. Changing context cannot turn a held
    // charge into a drill, or a released middle button into an accidental drawing.
    public struct FireWeaveControls
    {
        private bool initialized, left, right, ring, charging, drawing, blocked;
        private int level;
        public int Level=>initialized?level:4;
        public bool IsBlocked=>blocked;
        public float Power01=>Level/12f;
        public FireWeaveForm Form=>Level<3?FireWeaveForm.Jet:Level<7?FireWeaveForm.Flood:Level<10?FireWeaveForm.Orbit:FireWeaveForm.Sphere;
        public FireWeaveIntent Step(bool primary,bool secondary,bool field,float scroll,bool jump,bool shift)
        {
            if(!initialized){initialized=true;level=4;}
            if(blocked)
            {
                if(!primary&&!secondary&&!field&&!jump)blocked=false;
                left=primary;right=secondary;ring=false;charging=drawing=false;
                return default;
            }
            bool lp=primary&&!left,rp=secondary&&!right,weave=field&&shift;
            bool sphereBurst=weave&&level==12&&math.isfinite(scroll)&&scroll>.001f;
            if(weave&&math.isfinite(scroll)&&math.abs(scroll)>.001f)
                level=math.clamp(level+(scroll>0?1:-1)*math.clamp((int)math.ceil(math.abs(scroll)/120f),1,3),0,12);
            bool cancel=charging&&field;
            bool release=charging&&!secondary&&!field;
            if(cancel||release)charging=false;
            bool begin=rp&&!field;
            if(begin)charging=true;
            bool drawBegin=lp&&!field&&!secondary;
            if(drawBegin)drawing=true;
            if(!primary||field||secondary)drawing=false;
            bool chord=jump&&shift;
            var result=new FireWeaveIntent(weave&&Form<FireWeaveForm.Orbit,weave&&lp,weave&&rp,drawing,drawBegin,begin,release,cancel,chord&&!ring,ring&&!chord,jump&&!shift,sphereBurst);
            left=primary;right=secondary;ring=chord;
            return result;
        }
        public void Interrupt(){blocked=true;charging=drawing=ring=false;}
    }
    public static class FireWeaveTuning
    {
        public const int SourceCapacity=12;
        public const float SourceLife=1.35f,SourceSpacing=.65f,ChargeSeconds=1.2f,ReactionCooldown=.65f;
        public static float StreamPower(float power01)=>power01<=1f/3?math.lerp(.38f,1,math.saturate(power01*3)):math.lerp(1,2.5f,math.saturate((power01-1f/3)*6));
        public static float SegmentDistanceSquared(float3 point,float3 a,float3 b)
        {float3 d=b-a;float t=math.saturate(math.dot(point-a,d)/math.max(1e-8f,math.lengthsq(d)));return math.lengthsq(point-(a+d*t));}
        public static bool CanReact(float now,float last,float3 point,float3 from,float3 to,float radius)
            =>math.isfinite(now)&&math.all(math.isfinite(point))&&math.all(math.isfinite(from))&&math.all(math.isfinite(to))&&radius>0&&now-last>=ReactionCooldown&&SegmentDistanceSquared(point,from,to)<=radius*radius;
    }
}
