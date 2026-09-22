Shader "Vampire/PhoenixBlizzard"
{
    Properties { _MainTex("Unused",2D)="white" {} _Strength("Strength",Range(0,1))=0 _Phase("Phase",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent+80" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Strength,_Phase;
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            fixed4 frag(v2f_img i):SV_Target
            {
                // UV y is up. Subtracting (+x,-y) moves every layer down-right,
                // including the release envelope; phase never reverses or stops.
                float2 p=i.uv-float2(.45,-.65)*_Phase;
                float snow=0;
                for(int k=0;k<3;k++)
                {
                    float2 q=p*(float2(45,27)+k*13);
                    float2 id=floor(q),f=frac(q)-.5;
                    f.x+=f.y*.5;
                    float r=hash(id+k*21);
                    snow+=smoothstep(.15,0,length(f*float2(2,.75)))*step(.45,r);
                }
                float ribbons=pow(saturate(.5+.5*sin((p.x+p.y*.8)*32+sin(p.y*13-_Phase*2)*1.3)),5);
                float a=saturate(_Strength*(.7+ribbons*.26+snow*.4));
                return fixed4(.78+snow*.2,.91+snow*.09,1,a);
            }
            ENDCG
        }
    }
}
