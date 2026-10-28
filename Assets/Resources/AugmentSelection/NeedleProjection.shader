Shader "24tu/UI/OrbitNeedle"
{
    Properties { [PerRendererData] _MainTex ("Needle", 2D) = "white" {} }
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
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                // Same -135 degree rotation and 256x40 crop as the approved
                // preview's 180x180 diagonal needle. No additional sprite atlas.
                float2 p=float2((i.uv.x-.5)*256,(.5-i.uv.y)*40);
                float2 uv=.5+float2(-.70710678*(p.x+p.y),-.70710678*(p.x-p.y))/180;
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                fixed4 c=tex2D(_MainTex,saturate(uv))*i.color;c.a*=inside;return c;
            }
            ENDCG
        }
    }
}
