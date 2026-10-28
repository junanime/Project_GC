Shader "24Tu/Capsule immersion" {
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _CutY("Waterline",Float)=-9999 _Wet("Wet",Float)=0 _Clock("Clock",Float)=0 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;float4 color:COLOR;};
 sampler2D _MainTex;float _CutY,_Wet,_Clock;
 v2f vert(app v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*i.color;clip(c.a-.5);float wave=.018*sin(i.world.x*19+_Clock*5)+.009*sin(i.world.x*31-_Clock*3);float edge=i.world.y-_CutY-wave;if(_Wet>.001){c.a*=smoothstep(-.014,.012,edge);c.rgb=lerp(c.rgb,float3(.84,.97,.38),_Wet*(1-smoothstep(0,.04,edge))*.6);}return c;}
 ENDCG }
 }
}
