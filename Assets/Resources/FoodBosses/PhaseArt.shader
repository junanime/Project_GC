Shader "FoodBosses/PhaseArt"
{
 Properties {
  _MainTex("Original art",2D)="white"{} _Opacity("Opacity",Float)=1
  _Part("Anatomy part",Float)=0 _Size("Source pixels",Vector)=(304,297,0,0)
  _Radial("Radial reveal",Float)=-1 _Incoming("Incoming",Float)=0
  _Embarrassed("Embarrassed expression",Float)=0
 }
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float _Opacity,_Part,_Radial,_Incoming,_Embarrassed;float4 _Size;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;};
 struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;};
 vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.local=v.local;return o;}
 float segment(float x,float a,float b,float ya,float yb){return lerp(ya,yb,saturate((x-a)/(b-a)));}
 float lidEdge(float x){
  if(x<28)return segment(x,0,28,165,174);
  if(x<55)return segment(x,28,55,174,179);
  if(x<80)return segment(x,55,80,179,174);
  if(x<110)return segment(x,80,110,174,178);
  if(x<140)return segment(x,110,140,178,187);
  if(x<180)return segment(x,140,180,187,201);
  if(x<220)return segment(x,180,220,201,214);
  return segment(x,220,270,214,229);
 }
 float ellipse(float2 p,float2 at,float2 r){return step(length((p-at)/r),1);}
 float claws(float2 p,float phase,float side){
  float2 at=side<.5?lerp(float2(46,197),float2(59,201),phase):lerp(float2(148,214),float2(168,226),phase);
  float2 radius=side<.5?lerp(float2(44,51),float2(59,68),phase):lerp(float2(51,43),float2(50,49),phase);
  return ellipse(p,at,radius);
 }
 float horn(float2 p,float upper){
  float edge=80+min(p.x,100)*.2;
  return upper>.5?step(p.y,edge)*step(p.x,180):step(edge,p.y)*step(p.x,116)*step(p.y,160);
 }
 fixed4 frag(vary i):SV_Target {
  float2 p=float2(i.local.x*_Size.x,(1-i.local.y)*_Size.y);
  float keep=1;
  float lid=step(p.y,lidEdge(p.x))*step(p.x,267);
  if(_Part==1)keep=1-lid;
  if(_Part==2)keep=lid;
  // The incoming crab remains one connected surface; claws grow with pinned joints.
  if(_Part==11)keep=claws(p,1,0);
  if(_Part==12)keep=claws(p,1,1);
  if(_Part>=13&&_Part<=16){float side=floor((_Part-13)/2);float half=fmod(_Part-13,2);keep=claws(p,0,side);float x=side<.5?46:148;keep*=half<.5?step(p.x,x):step(x,p.x);}
  if(_Part>=17&&_Part<=19){float k=_Part-17;keep=step(70,p.x)*step(p.y,139)*step(k,floor((p.x-70)/80))*step(floor((p.x-70)/80),k);}
  // Incoming beetle is continuous: horn roots stay joined during both contacts.
  if(_Part==21)keep=horn(p,1);
  if(_Part==22)keep=horn(p,0);
  if(_Part>=30&&_Part<=35){float k=_Part-30;float s=floor((p.x-195)/30)+3*step(147,p.y);keep=step(195,p.x)*step(p.x,285)*step(112,p.y)*step(p.y,182)*step(k,s)*step(s,k);}
  clip(keep-.5);
  fixed4 c=tex2D(_MainTex,i.uv);
  if(_Radial>=0){float d=length((i.local-float2(.58,.39))*float2(1,.87));float reveal=smoothstep(_Radial-.015,_Radial+.015,d);c.a*=lerp(reveal,1-reveal,_Incoming);c.rgb=lerp(c.rgb,float3(1,.93,.73),(1-saturate(abs(d-_Radial)/.015))*.32);}
  if(_Embarrassed>.001){
   // Temporary cream-face pose; original outline, tongue, shell and hinge remain untouched.
   float eyeMask=max(ellipse(p,float2(106,64),float2(25,30)),ellipse(p,float2(181,76),float2(29,26)));
   float3 cream=float3(1,.961,.845);
   c.rgb=lerp(c.rgb,cream,eyeMask*_Embarrassed);
   float lx=p.x-106,rx=p.x-181;
   float lcurve=74-.009*lx*lx,rcurve=86-.007*rx*rx;
   float eyes=max(step(abs(lx),16)*step(abs(p.y-lcurve),1.7),step(abs(rx),18)*step(abs(p.y-rcurve),1.7));
   c.rgb=lerp(c.rgb,float3(.24,.075,.045),eyes*_Embarrassed);
   float blush=max(ellipse(p,float2(77,94),float2(11,5)),ellipse(p,float2(217,103),float2(11,5)));
   c.rgb=lerp(c.rgb,float3(1,.43,.46),blush*.55*_Embarrassed);
  }
  c.a*=_Opacity;return c;
 }
 ENDCG }
 }
}
