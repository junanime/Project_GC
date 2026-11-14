Shader "ConstipationKing/FloorSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _ColonClip("Colon walls", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../FoodBosses/ColonFloorClip.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct app { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct vary { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 color:COLOR; };
            vary vert(app v)
            {
                vary o; o.position=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            fixed4 frag(vary i):SV_Target
            {
                ClipColonFloor(i.world);
                fixed4 color=tex2D(_MainTex,i.uv)*i.color;
                clip(color.a-.002);
                return color;
            }
            ENDCG
        }
    }
}
