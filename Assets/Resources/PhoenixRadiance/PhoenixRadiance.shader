Shader "Vampire/PhoenixRadiance"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Frames ("Frames A B blend phase", Vector) = (0,0,0,0)
        _Motion ("Feather sway, reveal from right, ring density, canvas", Vector) = (0,1,0,1)
        _Anchors ("Per-frame anchor correction", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color, _Frames, _Motion, _Anchors;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            float4 cell(float2 uv, float frame)
            {
                float2 offset=float2(fmod(frame,4),2-floor(frame/4));
                // Never sample an adjacent cell, even during feather deformation.
                float2 p=clamp(uv,float2(.004,.004),float2(.996,.996));
                float4 c=tex2D(_MainTex,(p+offset)/float2(4,3));
                float edge=smoothstep(0,.012,min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)));
                c.a*=edge;
                c.rgb*=c.a;
                return c;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=(i.uv-.5)*max(1,_Motion.w)+.5;
                uv.x+=sin(uv.y*7+_Frames.w*2)*_Motion.x*uv.y*uv.y;
                float4 c=lerp(cell(uv+_Anchors.xy,_Frames.x),cell(uv+_Anchors.zw,_Frames.y),_Frames.z);
                float reveal=smoothstep(1-_Motion.y-.035,1-_Motion.y+.015,uv.x);
                if(_Motion.y>.999)reveal=1;
                if(_Motion.y<=0)reveal=0;
                // Extra fine filaments maintain density as a wave grows. Never punch holes
                // into the painted continuous ring or scale separated orbiting icons.
                float angle=atan2(uv.y-.5,uv.x-.5);
                float shimmer=1+.08*sin(angle*max(24,_Motion.z)+_Frames.w*3)*step(.1,_Motion.z);
                c.rgb*=_Color.rgb*shimmer;
                return c*(_Color.a*reveal);
            }
            ENDCG
        }
    }
}
