Shader "Vampire/PhoenixIce"
{
    Properties { _MainTex("Frames",2D)="white" {} _WindPalette("Wind palette",Float)=0 }
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
            sampler2D _MainTex; float _WindPalette;
            struct V {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct F {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            F vert(V v){F o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(F i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                // Preserve source silhouette, shading and neutral highlights; rotate the blue ice palette to pale green.
                float gray=min(c.r,min(c.g,c.b));
                float3 wind=float3(lerp(gray,c.b,.65),c.b,lerp(gray,c.g,.72));
                c.rgb=lerp(c.rgb,wind,_WindPalette);
                float2 cell=frac(i.uv*4);float2 edge=min(cell,1-cell);
                c.a*=smoothstep(0,.07,min(edge.x,edge.y));return c;
            }
            ENDCG
        }
    }
}
