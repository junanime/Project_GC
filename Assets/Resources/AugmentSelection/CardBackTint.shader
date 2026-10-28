Shader "24tu/UI/EmbroideredCardBack"
{
    Properties { [PerRendererData] _MainTex ("Card", 2D) = "white" {} _Accent ("Grade border", Color) = (0.32,0.65,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex; fixed4 _Accent;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                // The original approved image includes a dark presentation margin.
                // Crop in UV space; no extra copy for each grade or baked animation.
                float2 uv=float2(33.0/984.0,38.0/1598.0)+i.uv*float2(918.0/984.0,1523.0/1598.0);
                fixed4 c=tex2D(_MainTex,uv);
                float edgeX=min(i.uv.x,1-i.uv.x), edgeY=min(i.uv.y,1-i.uv.y);
                float border=max(1-step(.030,edgeX),1-step(.023,edgeY));
                border=max(border,(1-step(.140,edgeX))*(1-step(.087,edgeY)));
                float blue=smoothstep(.07,.23,c.b-c.r)*border;
                fixed3 tinted=saturate(c.rgb+_Accent.rgb-fixed3(.32,.65,1));
                c.rgb=lerp(c.rgb,tinted,blue);
                float2 p=abs(i.uv-float2(.5,.5))*float2(1,1.659);
                float2 d=p-(float2(.5,.8295)-.035);
                float sdf=length(max(d,0))+min(max(d.x,d.y),0)-.035;
                c.a*=1-smoothstep(-.001,.001,sdf);
                return c*i.color;
            }
            ENDCG
        }
    }
}
