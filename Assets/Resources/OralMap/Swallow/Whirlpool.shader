Shader "OralStage/SwallowWhirlpool"
{
    Properties
    {
        [PerRendererData] _MainTex("Approved whirlpool",2D)="white"{}
        [PerRendererData] _SecondTex("Counter flowing foam",2D)="white"{}
        _TongueCenter("Tongue center",Vector)=(0,0,0,0)
        _TongueRadii("Tongue radii",Vector)=(1,1,0,0)
        _SouthFloor("Tongue south edge",Float)=0
        _Clock("Event clock",Float)=0
        _Angle("Gameplay rotation",Float)=0
        _Phase("Individual variation",Float)=0
        _Opacity("Opacity",Range(0,1))=0
        _Preview("Warning ripples",Range(0,1))=0
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
            sampler2D _MainTex,_SecondTex;
            float4 _TongueCenter,_TongueRadii;
            float _SouthFloor,_Clock,_Angle,_Phase,_Opacity,_Preview;
            struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;};
            vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
            float2 rotate(float2 p,float a){float s=sin(a),c=cos(a);return float2(c*p.x-s*p.y,s*p.x+c*p.y);}
            fixed4 frag(vary i):SV_Target
            {
                float2 q=(i.uv-.5)*2;
                float radius=length(q);
                clip(1-radius);
                // The art is circular and every transparent corner stays clear.
                // Shared tongue limits prevent water discs covering walkable teeth.
                float2 local=i.world-_TongueCenter.xy;
                float ellipse=length(local/_TongueRadii.xy);
                float tongue=i.world.y>=_TongueCenter.y
                    ? 1-smoothstep(.99,1.0,ellipse)
                    : (1-smoothstep(_TongueRadii.x-.06,_TongueRadii.x,abs(local.x)))*smoothstep(_SouthFloor,_SouthFloor+.06,i.world.y);
                clip(tongue-.005);
                float angle=_Angle+.022*sin(_Clock*2.3+radius*7+_Phase);
                float2 p=rotate(q,angle);
                float bulge=1+.012*sin(_Clock*2.6+_Phase+radius*9)*(1-radius);
                fixed4 c=tex2D(_MainTex,p*.5/bulge+.5);
                float2 counter=rotate(q,_Angle-_Clock*.48-_Phase)/.91;
                fixed4 foam=tex2D(_SecondTex,counter*.5+.5);
                float foamMask=(1-smoothstep(.87,.91,radius))*foam.a*.085;
                c.rgb=lerp(c.rgb,foam.rgb,foamMask);
                c.a=saturate(c.a+foamMask*(1-c.a));
                float theta=atan2(q.y,q.x);
                float ripple=pow(saturate(.5+.5*sin(radius*24-_Clock*2.8)),24);
                ripple*=smoothstep(.20,.32,radius)*(1-smoothstep(.9,1,radius));
                ripple*=smoothstep(-.3,.3,sin(theta*2+_Clock*.7+_Phase));
                c.rgb=lerp(c.rgb,fixed3(.77,1,.99),_Preview);
                c.a=lerp(c.a,ripple*.6,_Preview)*_Opacity*tongue;
                c.a*=1-smoothstep(.975,1,radius);
                return c;
            }
            ENDCG
        }
    }
}
