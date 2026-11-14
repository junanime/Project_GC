Shader "Vampire/TimeSand"
{
    Properties { _Strength("Strength",Float)=0 _Phase("Phase",Float)=0 _Aspect("Aspect",Float)=1.7778 _Calm("Reduced motion",Float)=0 _AugmentOpacity("Augment opacity",Range(0,1))=1 }
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
            float _Strength,_Phase,_Aspect,_Calm,_AugmentOpacity;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*float2(_Aspect,1);float phase=_Phase*(1-_Calm);
                float cloud=noise(p*3+float2(phase*.055,-phase*.035))*.6+noise(p*8+float2(-phase*.08,phase*.05))*.4;
                float edge=smoothstep(.12,.8,length(p));
                // Quiet, coherent drifting grains rather than per-frame random noise.
                float2 cells=(p+float2(phase*.025,-phase*.016))*float2(250,250);
                float grain=step(.989,hash(floor(cells)))*(1-smoothstep(.08,.2,length(frac(cells)-.5)));
                float alpha=(.035+cloud*.09+edge*.025+grain*.10*(1-_Calm*.7))*_Strength*_AugmentOpacity;
                return fixed4(lerp(float3(.80,.72,.48),float3(1,.94,.72),cloud),alpha);
            }
            ENDCG
        }
    }
}
