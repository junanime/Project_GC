Shader "Vampire/Boss Remnant Swim Fins"
{
    Properties{[PerRendererData]_MainTex("Suit atlas",2D)="white"{} _SpriteRect("Atlas rect",Vector)=(0,0,1,1) _Kick("Kick time",Float)=0 _Strength("Fin amplitude",Float)=1}
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _SpriteRect;float _Kick,_Strength;
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v vert(a i){v o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            fixed4 frag(v i):SV_Target
            {
                float2 p=(i.uv-_SpriteRect.xy)/_SpriteRect.zw;
                float fin=pow(saturate((.31-p.y)/.31),2);
                float side=p.x<.5?-1:1;
                p.y+=sin(_Kick*16+side*1.57)*.065*fin*_Strength;
                p.x+=sin(_Kick*16+side*1.57)*side*.035*fin*_Strength;
                if(any(p<0)||any(p>1))return 0;
                return tex2D(_MainTex,_SpriteRect.xy+p*_SpriteRect.zw)*i.color;
            }
            ENDCG
        }
    }
}
