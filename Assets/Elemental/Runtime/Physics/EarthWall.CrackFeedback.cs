using Elemental.Simulation.Bending;
using Elemental.Runtime.World;
using UnityEngine;
namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private int _crackFeedbackPulse=3;
        private float _nextCrackFeedbackAt;
        private Vector3 _crackFeedbackCenter,_crackFeedbackUp,_crackFeedbackTangent,_crackFeedbackForward;
        private float _crackFeedbackWidth,_crackFeedbackHeight,_crackFeedbackDepth;
        private void ResetCrackFeedback(){_crackFeedbackPulse=3;_nextCrackFeedbackAt=0;}
        private void BeginCrackFeedback()
        {
            _crackFeedbackPulse=0;_nextCrackFeedbackAt=Time.time;
            _crackFeedbackCenter=_renderer!=null?_renderer.bounds.center:transform.position;
            _crackFeedbackUp=_up;_crackFeedbackTangent=_tangent;_crackFeedbackForward=_forward;
            _crackFeedbackWidth=_finalScale.x;_crackFeedbackHeight=Height;_crackFeedbackDepth=Thickness;
            TickCrackFeedback();
        }
        private void TickCrackFeedback()
        {
            if(_crackFeedbackPulse>=3||materialFeedback==null||Time.time<_nextCrackFeedbackAt)return;
            int pulse=_crackFeedbackPulse++;
            _nextCrackFeedbackAt=Time.time+.085f;
            int stations=Mathf.Clamp(Mathf.CeilToInt(_crackFeedbackWidth/1.8f),2,4);
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<stations;i++)
            {
                float along=(i+.5f)/stations-.5f;
                float height=((i+pulse)%3-1)*.27f;
                Vector3 point=_crackFeedbackCenter+_crackFeedbackTangent*(along*_crackFeedbackWidth)+
                    _crackFeedbackUp*(height*_crackFeedbackHeight)+_crackFeedbackForward*(side*(_crackFeedbackDepth*.5f+.025f));
                Vector3 normal=(_crackFeedbackUp*.7f+_crackFeedbackForward*side*.4f).normalized;
                materialFeedback.Emit(EarthMaterialFeedbackKind.Fracture,point,normal,1.1f,
                    Mathf.Clamp(_crackFeedbackWidth/stations*.55f,.5f,1.5f),WallId,_generation,
                    pulse==0?36:26,pulse==0?10:7);
            }
        }
    }
}
