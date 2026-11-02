Shader "Vampire/HerbalGround"
{
 Properties { _Age("Age",Float)=0 _Life("Life",Float)=0 _Fade("Fade",Float)=1 _Style("Style",Float)=0 _AugmentOpacity("Opacity",Range(0,1))=1 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
 struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
 float _Age,_Life,_Fade,_Style,_AugmentOpacity;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 float leaf(float2 p,float2 center,float rotation,float2 size){p-=center;float s=sin(rotation),c=cos(rotation);p=float2(c*p.x+s*p.y,-s*p.x+c*p.y)/size;return 1-smoothstep(.85,1.1,dot(p,p));}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 fixed4 frag(v2f i):SV_Target {
 float2 p=(floor(i.uv*150)+.5)/150*2-1;float2 q=p/float2(1,.84);float r=length(q),a=atan2(q.y,q.x);
 float3 color=0;float alpha=0;
 if(_Style<.5){
   float flow=frac((a+_Age*.95+r*5.5)*3/6.283185);
   float band=smoothstep(.07,.17,flow)*(1-smoothstep(.63,.86,flow));
   float edge=1-smoothstep(.72,.88,r);float inner=smoothstep(.025,.14,r);
   float shade=floor((flow*.65+hash(floor(p*70))*.10)*5)/5;
   color=lerp(float3(.29,.28,.44),float3(.43,.77,.67),saturate((flow-.08)*1.7));
   float gold=(1-smoothstep(.018,.05,abs(flow-.16)))*smoothstep(.22,.5,r);
   color=lerp(color,float3(1,.87,.48),gold);alpha=band*edge*inner*.72+gold*edge*.75;
   float core=(1-smoothstep(.04,.13,r));color=lerp(color,float3(.28,.26,.40),core);alpha=max(alpha,core*.6);
   float dots=pow(saturate(cos(a*13+r*28+_Age*8)),24)*pow(saturate(sin(r*36+_Age*9)),18)*smoothstep(.12,.22,r)*edge;
   color=lerp(color,float3(1,.90,.59),dots);alpha=max(alpha,dots*.8);
 }else if(_Style<1.5){
   float fill=(1-smoothstep(.70,.88,r))*.17;float pulse=pow(saturate(cos(r*19-_Age*4)),10)*(1-smoothstep(.7,.88,r))*.09;
   color=float3(.68,.86,.42);alpha=fill+pulse;
   float root=(1-smoothstep(.012,.028,abs(r-(.73+.015*sin(a*7)))))*.48;
   color=lerp(color,float3(.83,.91,.54),root);alpha=max(alpha,root);
   // Quiet central leaf vein, and seven separate sprouts on the boundary.
   float stem=(1-smoothstep(.008,.018,abs(q.x-q.y*.36)))*(1-smoothstep(.38,.44,abs(q.y)))*.35;
   alpha=max(alpha,stem);
   for(int j=0;j<6;j++){float y=-.30+j*.12;float side=j%2==0?-1:1;float l=leaf(q,float2(y*.36+side*.10,y),side*.65,float2(.11,.043))*.27;alpha=max(alpha,l);}
   for(int k=0;k<7;k++){
     float angle=k*6.283185/7;float2 pos=float2(cos(angle),sin(angle))*.73;
     float2 b=q-pos;b.y-=.025;float grow=saturate(_Fade*2);
     float stalk=(1-smoothstep(.010,.023,abs(b.x)))*step(-.01,b.y)*(1-step(.11*grow,b.y));
     float l=max(leaf(b,float2(-.045,.11)*grow,-.55,float2(.067,.032)*max(.05,grow)),leaf(b,float2(.045,.13)*grow,.55,float2(.067,.032)*max(.05,grow)));
     float outline=max(leaf(b,float2(-.045,.11)*grow,-.55,float2(.078,.043)*max(.05,grow)),leaf(b,float2(.045,.13)*grow,.55,float2(.078,.043)*max(.05,grow)));
     color=lerp(color,float3(.31,.48,.20),max(stalk,outline));alpha=max(alpha,max(stalk,outline)*.9);
     color=lerp(color,lerp(float3(.57,.76,.28),float3(.87,.94,.54),saturate(b.y*7)),l);
   }
   for(int n=0;n<3;n++){float t=frac(_Age*.28+n*.33);float2 pos=float2(sin(n*3.1+t)*.48,-.25+t*.95);float l=leaf(q,pos,-.6+t*.4,float2(.035,.017))*sin(t*3.14159);color=lerp(color,float3(.91,.97,.65),l);alpha=max(alpha,l*.65);}
 }else{
   float star=max(pow(saturate(1-abs(q.x)*15),3)*saturate(1-abs(q.y)),pow(saturate(1-abs(q.y)*15),3)*saturate(1-abs(q.x)));
   color=float3(1,.85,.49);alpha=star;
 }
 return fixed4(color,saturate(alpha)*_Fade*_AugmentOpacity);
 }
 ENDCG }
 }
}
