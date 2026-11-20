Shader "Vampire/TimeSand"
{
    Properties { _Strength("Strength",Float)=0 _Phase("Phase",Float)=0 _Release("Release",Float)=0 _AugmentOpacity("Augment opacity",Range(0,1))=1 }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
            float _Strength,_Phase,_Release,_AugmentOpacity;
            // Anchor the ring to the actual render target, even on a resized game window.
            v2f vert(appdata v){v2f o;o.pos=float4(v.vertex.x,v.vertex.y*_ProjectionParams.x,.5,1);o.uv=v.uv;return o;}
            // Pixel-sized sand builds into curved gusts along the four edges.
            float hash(float2 p) { return frac(52.9829189 * frac(dot(p, float2(.06711056, .00583715)))); }
            fixed4 frag(v2f i):SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 pixel = floor(i.uv * float2(aspect,1) * 320.0) / 320.0;
                float2 p = pixel - .5 * float2(aspect,1);
                p.y += _Release * _Release * .12;
                float2 rim = abs(i.uv - .5) * 2;
                float edge = max(smoothstep(.60, .98, rim.x), smoothstep(.55, .98, rim.y));
                // Leave the top bar, quest card and center item belt clear on every map.
                edge *= 1.0-smoothstep(.94,.965,i.uv.y);
                edge *= 1.0-smoothstep(.75,.80,i.uv.x)*smoothstep(.50,.55,i.uv.y);
                edge *= 1.0-(1.0-smoothstep(.065,.105,abs(i.uv.x-.5)))*smoothstep(.72,.79,i.uv.y);
                edge *= 1.0-(1.0-smoothstep(.175,.235,i.uv.y))*(1.0-smoothstep(.135,.20,abs(i.uv.x-.5)));
                float sweep = p.x * .93 + p.y * .35 - _Phase * .72;
                float bend = p.y + sin(p.x * 5.0 - _Phase * 2.2) * .055 + p.x * .17;
                float2 cells = float2(sweep*29.0, bend*105.0);
                float seed = hash(floor(cells));
                float2 grain = abs(frac(cells) - .5);
                float fleck = step(.60, seed) * step(grain.x, .14 + .25*seed) * step(grain.y,.31);
                float ribbon = .5 + .5*sin(bend * 93.0 + sin(sweep*7.0)*1.4);
                float streak = smoothstep(.78, .98, ribbon) * smoothstep(-.35,.65,sin(sweep*9.0+bend*16.0));
                float alpha = edge * (.045 + streak*.26 + fleck*.78) * _Strength * _AugmentOpacity;
                float3 color = lerp(float3(.69,.40,.15),float3(1,.88,.54), max(streak, fleck*seed));
                return fixed4(color,alpha);
            }
            ENDCG
        }
    }
}
