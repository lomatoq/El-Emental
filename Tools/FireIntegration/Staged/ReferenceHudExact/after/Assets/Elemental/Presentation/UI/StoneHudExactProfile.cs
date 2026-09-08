using UnityEngine;

namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName="Elemental/UI/Stone Exact HUD")]
    public sealed class StoneHudExactProfile : ScriptableObject
    {
        public bool enabled=true;
        // Reference pixels convert to the existing 1080-high UI panel, never its camera.
        public const float ReferenceWidth=1493, ReferenceHeight=840, UiScale=1080f/840;
        public Color gold=new Color(.84f,.70f,.43f,1);
        public Color selectedGold=new Color(1,.92f,.66f,1);
        [Range(0,1)] public float bottomScrimOpacity=.58f;
        public static readonly float[] ElementCenters={54,156,265,371};
        public static Rect ElementVisualRect(int index,bool active)
        {
            float size=active?72:60;
            return new Rect(ElementCenters[index]-size*.5f,46-size*.5f,size,size);
        }
        public static HudElementLayout Box(float x,float y,float w,float h)=>HudElementLayout.Box(x*UiScale,y*UiScale,w*UiScale,h*UiScale);
        public static void ApplyLayout(ElementalHudLayout layout)
        {
            float s=UiScale;
            layout.scoreboard=new HudElementLayout(new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(5*s,7*s),new Vector2(395*s,83*s));
            ApplyVital(layout.health,false); ApplyVital(layout.energy,true);
            layout.navigation.group=new HudElementLayout(Vector2.one,Vector2.one,new Vector2(-11*s,-37*s),new Vector2(218*s,245*s));
            layout.navigation.globe=Box(0,0,218,218);
            layout.navigation.caption=Box(0,225,218,18);
            layout.navigation.legend=Box(0,244,218,0);
            layout.pause.button=new HudElementLayout(Vector2.right,Vector2.right,new Vector2(-15*s,14*s),new Vector2(47*s,48*s));
            layout.pause.icon=new HudElementLayout(new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(16*s,18*s));
            layout.NotifyChanged();
        }
        private static void ApplyVital(HudVitalLayout vital,bool right)
        {
            float s=UiScale;
            vital.group=new HudElementLayout(new Vector2(right?1:0,272f/840),new Vector2(right?1:0,0),new Vector2((right?-8:8)*s,0),new Vector2(50*s,300*s));
            vital.bar=Box(0,0,50,222);
            vital.underBar=Box(0,228,50,72);
            vital.icon=Box(16,2,18,19);
            vital.value=Box(0,27,50,27);
        }
    }
}
