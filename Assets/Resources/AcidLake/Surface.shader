Shader "Vampire/Acid Lake Surface"
{
    Properties { _MainTex("Fine acid liquid",2D)="white"{} }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _DistanceTex;
            float4 _MapRect;
            float _SurfaceY, _Clock, _PixelSize;
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 position:SV_POSITION;float2 world:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
            float2 mirror(float2 p){return 1-abs(frac(p*.5)*2-1);}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(floor(i.world/_PixelSize)+.5)*_PixelSize;
                float2 uv=(p-_MapRect.xy)/_MapRect.zw;
                float distance=tex2D(_DistanceTex,uv*(511.0/512.0)+.5/512.0).r;
                clip(distance-.06);
                // Small ripples are visual only; the bright foam band marks the shared damage line.
                float wave=sin(p.x*5.1+_Clock*1.7)*.045+sin(p.x*12.7-_Clock*2.3)*.022;
                float depth=_SurfaceY+wave-p.y;
                clip(depth);
                float2 flow=p+float2(sin(p.y*2+_Clock)*.018+_Clock*.014,sin(p.x+_Clock*.6)*.015);
                float3 color=tex2D(_MainTex,mirror(flow/4.5)).rgb;
                color*=lerp(1,.91,saturate(depth*.5));
                float foam=.055+.018*sin(p.x*24+_Clock*2);
                if(depth<foam)color=float3(.93,.98,.51);
                if(depth<.016)color=float3(1,.98,.72);
                if(depth>foam && depth<foam+.035)color=lerp(color,float3(.7,.84,.22),.75);
                // Many independently cycling bubbles in the surface band, no scrolling seams.
                float cell=floor(p.x/.21), phase=frac(_Clock*.72+sin(cell*14.13)*31.7);
                float2 bubble=float2(frac(p.x/.21)-.5,(depth-.055)/.14);
                float radius=.07+phase*.25;
                float ring=abs(length(bubble)-radius);
                if(depth<.16 && ring<.043 && phase<.87)color=lerp(color,float3(.98,1,.67),.85);
                return fixed4(color,1);
            }
            ENDCG
        }
    }
}
