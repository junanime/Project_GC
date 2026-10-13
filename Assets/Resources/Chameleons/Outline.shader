Shader "Chameleons/Outline"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
  Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
   fixed4 frag(v2f i):SV_Target {
    float a=tex2D(_MainTex,i.uv).a,nearby=0;float2 d=_MainTex_TexelSize.xy*3;
    nearby=max(nearby,tex2D(_MainTex,i.uv+float2(d.x,0)).a);nearby=max(nearby,tex2D(_MainTex,i.uv-float2(d.x,0)).a);
    nearby=max(nearby,tex2D(_MainTex,i.uv+float2(0,d.y)).a);nearby=max(nearby,tex2D(_MainTex,i.uv-float2(0,d.y)).a);
    nearby=max(nearby,tex2D(_MainTex,i.uv+d).a);nearby=max(nearby,tex2D(_MainTex,i.uv-d).a);
    nearby=max(nearby,tex2D(_MainTex,i.uv+float2(d.x,-d.y)).a);nearby=max(nearby,tex2D(_MainTex,i.uv+float2(-d.x,d.y)).a);
    fixed4 c=i.color;c.a*=saturate(nearby-a)*(.75+.25*sin(_Time.y*16));return c;
   }
   ENDCG
  }
 }
}
