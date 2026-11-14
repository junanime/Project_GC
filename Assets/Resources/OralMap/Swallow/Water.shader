Shader "OralStage/SwallowWater"
{
    Properties
    {
        _MainTex("Approved flooded mouth",2D)="white"{}
        _ExitTex("Approved esophagus current",2D)="white"{}
        _Water("Water level",Range(0,1))=0
        _Swallow("Swallow progression",Range(0,1))=0
        _Clock("Paused event clock",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_ExitTex;
            float _Water,_Swallow,_Clock;
            struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(vary i):SV_Target
            {
                float2 uv=i.uv;
                float exitBlend=smoothstep(0,.38,_Swallow);
                fixed4 original=lerp(tex2D(_MainTex,uv),tex2D(_ExitTex,uv),exitBlend);
                // Only turquoise water refracts. Teeth, gums and the original
                // tongue silhouette remain aligned with navigation at every frame.
                float liquid=smoothstep(.015,.12,min(original.g,original.b)-original.r);
                float2 drift=float2(sin(uv.y*39-_Clock*1.6),cos(uv.x*35+_Clock*1.1))*.0012*liquid;
                drift.y-=exitBlend*.0016*sin(_Clock*5+uv.y*14);
                fixed4 c=lerp(tex2D(_MainTex,uv+drift),tex2D(_ExitTex,uv+drift),exitBlend);
                float glint=pow(saturate(.5+.5*sin(uv.y*210+sin(uv.x*32)*2-_Clock*3.1)),28);
                glint*=pow(saturate(.5+.5*sin(uv.x*103+uv.y*13+_Clock*.65)),9);
                c.rgb=lerp(c.rgb,fixed3(.84,1,.98),glint*liquid*.15);
                // This is the preview's soft rising waterline in the same source
                // pixel coordinates (bottom y950 to top y350), not a hard wipe.
                float frontier=lerp(1-950.0/1121,1-350.0/1121,_Water);
                float wave=sin(uv.x*31+_Clock*1.2)*.004*_Water;
                c.a=saturate(_Water*10)*(1-smoothstep(frontier-.06+wave,frontier+.06+wave,uv.y));
                c.a=lerp(c.a,1,exitBlend);
                return c;
            }
            ENDCG
        }
    }
}
