Shader "Hidden/Tangdengi/ApprovedArtBake"
{
 Properties { _MainTex("Approved art",2D)="white"{} _Part("Body or leg",Float)=0 _Phase("Phase",Float)=0 }
 SubShader { Tags {"Queue"="Transparent"} Cull Off ZWrite Off
 Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize; float _Part,_Phase;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
 vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 float boundary(float y,float top,float bottom){return lerp(top,bottom,saturate((y-680)/180));}
 int leg(float2 p){
  float y=p.y,x=p.x;
  if(_Phase<.5){
   if(x<boundary(y,680,535))return y>=645?1:0;
   if(x<boundary(y,872,780))return y>=602?2:0;
   if(x<boundary(y,946,993))return y>=700?3:0;
   if(x<boundary(y,1184,1240))return y>=610?4:0;
   if(x<boundary(y,1290,1374))return y>=702?5:0;
   return y>=615?6:0;
  }
  if(x<boundary(y,773,646))return y>=642?1:0;
  if(x<boundary(y,944,850))return y>=601?2:0;
  if(x<boundary(y,1014,1065))return y>=697?3:0;
  if(x<boundary(y,1215,1278))return y>=604?4:0;
  if(x<boundary(y,1310,1408))return y>=701?5:0;
  return y>=610?6:0;
 }
 fixed4 frag(vary i):SV_Target {
  float2 p=float2(i.uv.x*_MainTex_TexelSize.z,(1-i.uv.y)*_MainTex_TexelSize.w);
  if(_Part>=0)clip(.1-abs(leg(p)-_Part));fixed4 c=tex2D(_MainTex,i.uv);clip(c.a-.04);return c;
 }
 ENDCG } }
}
