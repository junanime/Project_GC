Shader "Vampire/PhoenixWrapFire"
{
    Properties { _MainTex("Frames",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) _EdgePixels("Flame edge width",Float)=12 }
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
            sampler2D _MainTex; float4 _MainTex_ST, _MainTex_TexelSize; fixed4 _Color;float _EdgePixels; float _FlameTime;
            struct V { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct F { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            F vert(V v) { F o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color*_Color;return o; }
            fixed4 frag(F i):SV_Target
            {
                float2 flow=float2(sin(i.uv.y*110-_FlameTime*9)+.45*sin(i.uv.y*213-_FlameTime*17),cos(i.uv.x*95+_FlameTime*7))*.0025;
                i.uv+=flow; fixed4 c=tex2D(_MainTex,i.uv);
                // Generated fire is painted over a low-green orange glow. Retain the hot
                // core and its neighbouring red edge, without a rectangular backdrop.
                float g=c.g;
                g=max(g,tex2D(_MainTex,i.uv+float2(_MainTex_TexelSize.x*_EdgePixels,0)).g);
                g=max(g,tex2D(_MainTex,i.uv-float2(_MainTex_TexelSize.x*_EdgePixels,0)).g);
                g=max(g,tex2D(_MainTex,i.uv+float2(0,_MainTex_TexelSize.y*_EdgePixels)).g);
                g=max(g,tex2D(_MainTex,i.uv-float2(0,_MainTex_TexelSize.y*_EdgePixels)).g);
                g=max(g,tex2D(_MainTex,i.uv+float2(_MainTex_TexelSize.x*_EdgePixels*.5,0)).g);
                g=max(g,tex2D(_MainTex,i.uv-float2(_MainTex_TexelSize.x*_EdgePixels*.5,0)).g);
                float flicker=sin(i.uv.y*170-_FlameTime*13+i.uv.x*60);
                c.a*=smoothstep(.32+.035*flicker,.55,g); c.rgb*=1+.09*flicker; return c*i.color;
            }
            ENDCG
        }
    }
}
