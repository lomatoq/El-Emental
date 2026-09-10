using Unity.Mathematics;
namespace Elemental.Simulation.Characters
{
    // A finite root-local pose offset, not contact ownership or a world anchor.
    public struct EarthFootReleaseTransition
    {
        public const float Duration = .14f;
        private float3 _offset;
        private float _elapsed;
        private quaternion _rotationOffset;
        public bool Active { get; private set; }
        public void Begin(float3 previousRenderedLocal, float3 currentAuthoredLocal)
        {
            _rotationOffset = quaternion.identity;
            _offset = previousRenderedLocal - currentAuthoredLocal;
            _elapsed = 0f;
            Active = math.all(math.isfinite(_offset)) && math.lengthsq(_offset) > 1e-10f;
        }
        public void Begin(float3 previousRenderedLocal,float3 currentAuthoredLocal,
            quaternion previousRenderedRotation,quaternion currentAuthoredRotation)
        {
            Begin(previousRenderedLocal,currentAuthoredLocal);
            if(!math.all(math.isfinite(previousRenderedRotation.value)) ||
                !math.all(math.isfinite(currentAuthoredRotation.value)) ||
                math.lengthsq(previousRenderedRotation.value)<1e-8f || math.lengthsq(currentAuthoredRotation.value)<1e-8f) return;
            quaternion previous=math.normalize(previousRenderedRotation),current=math.normalize(currentAuthoredRotation);
            _rotationOffset=math.mul(previous,math.inverse(current));
            Active |= math.abs(math.dot(previous.value,current.value))<.999999f;
        }
        public quaternion ResolveRotation(quaternion currentAuthoredRotation)
        {
            if(!Active) return currentAuthoredRotation;
            float t=math.saturate(_elapsed/Duration),weight=1f-t*t*(3f-2f*t);
            return math.normalize(math.mul(math.slerp(quaternion.identity,_rotationOffset,weight),currentAuthoredRotation));
        }
        public void Advance(float deltaTime)
        {
            if (!Active) return;
            _elapsed += math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (_elapsed >= Duration) Active = false;
        }
        public float3 Resolve(float3 currentAuthoredLocal)
        {
            if (!Active) return currentAuthoredLocal;
            float t = math.saturate(_elapsed / Duration);
            return currentAuthoredLocal + _offset * (1f - t*t*(3f-2f*t));
        }
    }
}
