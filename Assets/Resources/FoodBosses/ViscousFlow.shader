Shader "FoodBosses/ViscousFlow"
{
    Properties
    {
        _ColonClip("Colon walls",Float)=0
        _MainTex("Texture",2D)="white"{}
        _Clock("Flow clock",Float)=0
        _Mode("Cream / liquid rim / liquid sheet",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "ColonFloorClip.cginc"
            struct appdata
            {
                float4 vertex:POSITION; float4 color:COLOR;
                float2 uv:TEXCOORD0; float2 section:TEXCOORD1; float2 normalXY:TEXCOORD2;
            };
            struct v2f
            {
                float4 vertex:SV_POSITION; float4 color:COLOR;
                float2 uv:TEXCOORD0; float2 section:TEXCOORD1;
                float2 normalXY:TEXCOORD2; float2 world:TEXCOORD3;
            };
            float _Clock,_Mode;
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;
                o.uv=v.uv;o.section=v.section;o.normalXY=v.normalXY;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                ClipColonFloor(i.world);
                float s=i.uv.x;
                float3 color;
                float alpha=i.color.a;
                if(_Mode<1.5)
                {
                    // The same hemispherical normal spans the sides AND the end caps.
                    // Fixed world lighting prevents highlights flipping around every spiral bend.
                    float radius=saturate(length(i.section));
                    float z=sqrt(saturate(1-radius*radius));
                    float3 normal=normalize(float3(i.normalXY,z)+float3(0,0,.0001));
                    float light=saturate(dot(normal,normalize(float3(-.32,.55,.83))));
                    // Four hand-painted value bands, rather than a smooth plastic tube.
                    // The outline stays warm like the original pastry illustration.
                    color=i.color.rgb*float3(.70,.53,.34);
                    color=lerp(color,i.color.rgb*float3(.91,.81,.64),step(.35,light));
                    color=lerp(color,i.color.rgb,step(.60,light));
                    color=lerp(color,float3(1,.99,.87),step(.88,light));
                    color=lerp(color,i.color.rgb*float3(.48,.32,.19),step(.96,radius)*.85);
                    // Short, crisp glazing marks follow arc length. No long glossy stripe.
                    float dash=step(.58,frac(floor(s*32)/32*1.8));
                    float glint=step(.94,light)*dash*step(radius,.74);
                    color=lerp(color,float3(1,1,.95),glint*.72);
                    if(_Mode>.5)
                    {
                        // A liquid crest is translucent foam rather than outlined icing.
                        color=lerp(i.color.rgb*.94,float3(1,1,.83),step(.66,light)*.55);
                        alpha*=lerp(.72,1,1-radius);
                    }
                }
                else
                {
                    // A shallow sheet of juice, not a broad striped tube. UV.y follows
                    // the fan, while section.y follows its advancing/draining front.
                    float across=saturate(i.uv.y),forward=saturate(i.section.y);
                    float edge=step(.91,abs(across*2-1));
                    float drift=sin(s*2.8-_Clock*5+across*7);
                    float tone=.94+.035*step(-.2,drift)+.025*step(.6,drift);
                    color=i.color.rgb*tone;
                    color=lerp(color,float3(1,.99,.68),edge*.22);
                    // Sparse pixel-sized bubbles travel with the outflow. Their white
                    // rims, clear centres and visible floor read as juice, not gold fabric.
                    float2 flowUV=float2(s-_Clock*2.2,(across-.5)*max(s,.3)*1.1)*2.7;
                    float2 cell=floor(flowUV),p=floor((frac(flowUV)-.5)*12)/12;
                    float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    float bubble=step(.89,seed)*step(.13,length(p))*(1-step(.23,length(p)));
                    float shine=step(.97,seed)*(1-step(.13,length(p)));
                    color=lerp(color,float3(1,1,.85),bubble*.85+shine*.65);
                    alpha*=1-.16*bubble;
                    color=lerp(color,float3(1,1,.72),step(.94,forward)*.13);
                }
                return fixed4(saturate(color),alpha);
            }
            ENDCG
        }
    }
}
