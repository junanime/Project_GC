Shader "Snail/MissingFruit"
{
 Properties { [PerRendererData] _MainTex("Shell",2D)="white"{} _EmptyTex("Empty cavities",2D)="white"{} _Missing("Blueberry Strawberry Melon Mango",Vector)=(0,0,0,0) _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
 Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex,_EmptyTex;float4 _Missing;fixed4 _Color;
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
 fixed4 frag(v2f i):SV_Target
 {
   fixed4 a=tex2D(_MainTex,i.uv),b=tex2D(_EmptyTex,i.uv);
   // Restrict changes to the four ingredient pockets. Original rim/spiral remain untouched.
   float2 uv=i.uv;
   float left=1-smoothstep(.45,.49,uv.x),right=smoothstep(.51,.55,uv.x);
   float low=1-smoothstep(.46,.5,uv.y),high=smoothstep(.51,.55,uv.y);
   float inside=1-smoothstep(.41,.46,length(uv-.5));
   float mask=saturate(_Missing.x*left*low+_Missing.y*left*high+_Missing.z*right*low+_Missing.w*right*high)*inside;
   fixed4 c=lerp(a,b,mask);c.a=a.a;return c*i.color;
 }
 ENDCG
 }
 }
}
