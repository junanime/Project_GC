Shader "FoodBosses/SoftFlow"
{
    Properties { _MainTex("Texture",2D)="white"{} _Clock("Clock",Float)=0 _Mode("Material",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            float _Clock,_Mode;
            v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                // Material follows arc length, so curved ribbons never stretch a repeating tile.
                float s=floor(i.uv.x*28)/28, v=floor(saturate(i.uv.y)*22)/22;
                float edge=abs(v-.5)*2;
                float surge=.5+.5*sin(s*3.8-_Clock*8.4);
                float ridge=abs(v-(.33+.025*sin(s*3.1-_Clock*5)));
                float shine=(1-smoothstep(.045,.11,ridge))*(.25+.08*surge);
                float shade=lerp(.68,1,1-smoothstep(.40,1,edge));
                float3 c=i.color.rgb*shade;
                c=lerp(c,float3(1,1,.92),shine);
                if(_Mode<.5)
                {
                    c=lerp(c,i.color.rgb*float3(.48,.39,.29),smoothstep(.90,1,edge)*.70);
                    c+=.025*surge*(1-edge);
                }
                else if(_Mode<1.5)
                {
                    float runnel=.5+.5*sin(v*43+s*1.3-_Clock*6);
                    c+=runnel*.035*(1-edge);
                }
                else
                {
                    c=lerp(i.color.rgb*.80,float3(1,1,.83),pow(saturate(1-edge),2.5)*.82);
                    c+=.05*surge;
                }
                return fixed4(saturate(c),i.color.a);
            }
            ENDCG
        }
    }
}
