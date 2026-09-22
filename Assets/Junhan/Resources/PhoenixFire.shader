Shader "Vampire/PhoenixFire"
{
    Properties { _MainTex("Frames",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) _EdgePixels("Flame edge width",Float)=2 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST, _MainTex_TexelSize; fixed4 _Color;float _EdgePixels;
            struct V { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct F { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            F vert(V v) { F o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color*_Color;return o; }
            fixed4 frag(F i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                // Generated fire is painted over a low-green orange glow. Retain the hot
                // core and its neighbouring red edge, without a rectangular backdrop.
                float g=c.g;
                g=max(g,tex2D(_MainTex,i.uv+float2(_MainTex_TexelSize.x*_EdgePixels,0)).g);
                g=max(g,tex2D(_MainTex,i.uv-float2(_MainTex_TexelSize.x*_EdgePixels,0)).g);
                g=max(g,tex2D(_MainTex,i.uv+float2(0,_MainTex_TexelSize.y*_EdgePixels)).g);
                g=max(g,tex2D(_MainTex,i.uv-float2(0,_MainTex_TexelSize.y*_EdgePixels)).g);
                g=max(g,tex2D(_MainTex,i.uv+float2(_MainTex_TexelSize.x*_EdgePixels*.5,0)).g);
                g=max(g,tex2D(_MainTex,i.uv-float2(_MainTex_TexelSize.x*_EdgePixels*.5,0)).g);
                c.a*=smoothstep(.64,.88,g);return c*i.color;
            }
            ENDCG
        }
    }
}
