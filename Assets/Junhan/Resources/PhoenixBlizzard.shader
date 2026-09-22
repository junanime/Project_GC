Shader "Vampire/PhoenixBlizzard"
{
    Properties { _MainTex("Reference snowstorm",2D)="white" {} _Strength("Strength",Range(0,1))=0 _Phase("Phase",Float)=0 }
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
            sampler2D _MainTex;float _Strength,_Phase;
            fixed4 frag(v2f_img i):SV_Target
            {
                // Keep translating down-right throughout both the dense and clearing phases.
                float2 p=i.uv-float2(.22,-.32)*_Phase;
                p+=float2(sin(i.uv.y*7+_Phase)*.009,cos(i.uv.x*8+_Phase)*.007);
                p=1-abs(frac(p*.5)*2-1);
                fixed4 snow=tex2D(_MainTex,p);
                float fog=_Strength*_Strength*.92;
                float flakes=smoothstep(.85,.99,snow.r)*pow(max(0,_Strength),.65)*.85;
                float alpha=fog+flakes*(1-fog)*snow.a;
                return fixed4(snow.rgb,alpha);
            }
            ENDCG
        }
    }
}
