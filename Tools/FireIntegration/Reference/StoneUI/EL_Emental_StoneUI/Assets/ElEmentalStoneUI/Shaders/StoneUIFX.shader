Shader "Hidden/ElEmental/StoneUI/FX"
{
    Properties
    {
        _MainTex("Packed R rim / G cracks / B noise / A shape",2D)="white"{}
        _Tint("Tint",Color)=(0.73,0.86,0.45,1)
        _Intensity("Intensity",Range(0,2))=0.5
        _Clock("Unscaled clock",Float)=0
        _Mode("0 sheen / 1 aura / 2 crack / 3 dissolve",Float)=0
        _Progress("Dissolve progress",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Tint;float _Intensity,_Clock,_Mode,_Progress;
            struct V{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct F{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            F vert(V i){F o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
            float4 frag(F i):SV_Target
            {
                float2 uv=i.uv;float4 m=tex2D(_MainTex,uv);float a=0;
                if(_Mode<0.5)
                {
                    // One sweep per 4 seconds, no perpetual flashing. Do not include text in this mask.
                    float phase=frac(_Clock*.25);float pos=phase*2.5-.7;
                    float band=exp(-pow((uv.x+uv.y*.25-pos)/.07,2));
                    a=band*m.a*(m.r*.72+.035);
                }
                else if(_Mode<1.5)
                {
                    float r=length((uv-.5)*2);float pulse=.92+.08*sin(_Clock*1.745329);
                    a=(exp(-pow((r-.60)/.075,2))*.36+exp(-r*r*5)*.2)*pulse;
                    a*=1-smoothstep(.85,1,r);
                }
                else if(_Mode<2.5)
                {
                    float travel=.5+.5*sin(uv.x*5+uv.y*2-_Clock*.55);
                    a=(m.g*.4+m.r*.12)*(.72+.28*travel)*m.a;
                }
                else
                {
                    float visible=smoothstep(_Progress-.06,_Progress+.06,m.b);
                    float edge=1-smoothstep(0,.045,abs(m.b-_Progress));
                    a=m.a*edge*(1-visible);
                }
                // Straight RGBA. UI Toolkit composites once; no accidental double alpha multiplication.
                return float4(_Tint.rgb,saturate(a*_Intensity*_Tint.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
