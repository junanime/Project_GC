Shader "OralStage/PalateArena"
{
    Properties { [PerRendererData] _MainTex("Palate",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"}
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;fixed4 _Color;
            struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            fixed4 frag(vary i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                // Feather only the unused outer margin; the playable lip stays opaque.
                float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                c.a*=smoothstep(0,.045,edge);return c;
            }
            ENDCG
        }
    }
}
