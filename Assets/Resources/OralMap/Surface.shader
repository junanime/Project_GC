Shader "OralStage/Surface" {
 Properties { _MainTex("Texture",2D)="white"{} _SecondTex("Palate",2D)="white"{} _DetailTex("Fine Surface",2D)="gray"{} _Color("Tint",Color)=(1,1,1,1) _Mode("Mode",Float)=0 _Clock("Clock",Float)=0 _Fluid("Fluid",Float)=0 _Progress("Progress",Float)=0 _Ceiling("Ceiling",Float)=0 _StripeWidth("Hatch world width",Float)=.85 _HatchContrast("Hatch contrast",Range(0,1))=.75 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex,_SecondTex,_DetailTex; float4 _Color,_HoleRect,_TongueCenter,_TongueRadii; float _Mode,_Clock,_Fluid,_Progress,_Ceiling,_Slant,_SolidFloor,_SouthFloor,_StripeWidth,_HatchContrast;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 roofUV:TEXCOORD1;float4 color:COLOR;};
 struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 roofUV:TEXCOORD1;float2 world:TEXCOORD2;float4 color:COLOR;};
 vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.roofUV=v.roofUV;o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color;return o;}
 fixed4 frag(vary i):SV_Target {
 float2 uv=i.uv;
 if(_Mode>8.5){
 // Only the exact tooth damage meshes use this mode. World-space hatching
 // keeps equal physical stripe widths on molars, canines and narrow incisors.
 float hue=dot(i.world,float2(.035,.018))+_Clock*.12;
 float3 rgb=.16+.84*(.5+.5*cos(6.2831853*(hue+float3(0,.6666667,.3333333))));
 float stripeCoord=dot(i.world,float2(.70710678,.70710678))/max(.1,_StripeWidth)-_Clock*.28;
 float wave=sin(stripeCoord*6.2831853);
 float aa=max(.03,fwidth(stripeCoord)*6.2831853);
 float stripe=smoothstep(-aa,aa,wave);
 float3 dark=rgb*.22;
 float3 bright=lerp(rgb,float3(1,.98,.87),.72);
 float3 hatch=lerp(dark,bright,stripe);
 return float4(lerp(rgb,hatch,saturate(_HatchContrast))*_Color.rgb,_Color.a)*i.color;
 }
 if(_Mode>7.5){fixed4 floor=tex2D(_MainTex,uv);fixed4 roof=tex2D(_SecondTex,i.roofUV);roof.rgb=lerp(float3(.65,.32,.48),roof.rgb,roof.a);return float4(lerp(floor.rgb,roof.rgb,_Ceiling),1)*_Color*i.color;}
 if(_Mode<.5){
 float2 q=(uv-float2(.5,.45)); float inner=length(q/float2(.33,.29));
 // The lower gum mesh is the sole permanent gum rim. This surface supplies
 // only the depth of the chasm, never a second overlapping tissue arch.
 float3 dark=lerp(float3(.115,.023,.065),float3(.31,.065,.13),saturate(1-abs(inner-.95)*3));
 float3 c=dark;
 float rectEdge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
 c=lerp(float3(.115,.023,.065),c,smoothstep(0,.035,rectEdge));
 return float4(c,1);
 }
 if(_Mode<1.5){fixed4 c=lerp(tex2D(_MainTex,uv),tex2D(_SecondTex,i.roofUV),_Ceiling)*_Color;
 if(_SolidFloor>.5){float3 baseC=lerp(float3(.94,.39,.49),float3(.65,.32,.48),_Ceiling);c.rgb=lerp(baseC,c.rgb,c.a);c.a=1;
 // Mirrored tile UVs meet continuously, preserving sharp detail while the broad
 // tongue art supplies volume. The detail travels with the deforming mesh.
 float2 tile=1-abs(frac(uv*float2(7,5)*.5)*2-1);
 float detail=dot(tex2D(_DetailTex,tile).rgb,float3(.3,.59,.11))-.52;
 c.rgb+=detail*.48*(1-_Ceiling*.65);
 }else clip(c.a-.08);return c*i.color;}
 if(_Mode<2.5){
 float ellipse=length((uv-_TongueCenter.xy)/_TongueRadii.xy);
 float edge=uv.y>=_TongueCenter.y?smoothstep(1,.985,ellipse):smoothstep(_TongueRadii.x+.002,_TongueRadii.x-.002,abs(uv.x-_TongueCenter.x))*step(_SouthFloor,uv.y);float front=1.15-_Progress*1.5;
 float wave=sin(uv.x*26+_Clock*3)*.015+sin(uv.x*51-_Clock*4)*.006;
 float d=uv.y+(uv.x-.5)*_Slant-front+wave;float band=smoothstep(-.016,.02,d)*(1-smoothstep(.14,.26,d));
 float foam=exp(-abs(d)*160);float ripple=pow(saturate(sin(uv.y*100+uv.x*13-_Clock*5)),18);
 float3 baseC=_Fluid<.5?float3(.13,.65,.84):(_Fluid<1.5?float3(.40,.17,.065):float3(.96,.68,.19));
 float3 c=lerp(baseC,float3(.92,.98,.90),saturate(foam*.9+ripple*.28));
 return float4(c,edge*band*(.58+foam*.28)*_Color.a);
 }
 if(_Mode<3.5){float2 p=uv*2-1;float r=length(p);float mask=1-smoothstep(.94,1,r);float rim=exp(-abs(r-.84)*38);float glint=exp(-length((p-float2(-.31,.38))*float2(2.5,1.8))*13);float glint2=exp(-length(p-float2(.48,-.28))*20);float3 c=lerp(_Color.rgb,float3(1,1,.91),saturate(rim*.8+glint+glint2*.6));return float4(c,mask*(.12+rim*.65+glint*.8+glint2*.4)*_Color.a);}
 if(_Mode>6.5){float2 local=(uv-_HoleRect.xy)/_HoleRect.zw*2-1;float mask=1-smoothstep(.72,1,length(local));return tex2D(_MainTex,uv)*float4(1,1,1,mask)*i.color;}
 if(_Mode>5.5){
 // The original tongue's painted root folds form the rise. Only its upper
 // join fades; the rounded silhouette and highlight retain the map's finish.
 float x=uv.x*2-1;
 float4 tissue=tex2D(_MainTex,float2(uv.x,uv.y*.37));
 float join=1-smoothstep(.78,.99,uv.y+.08*x*x);
 float edge=smoothstep(.45,.62,tissue.a);
 return float4(tissue.rgb,edge*join);
 }
 if(_Mode>4.5){
 float ridge=sin(uv.y*3.14159);float folds=pow(saturate(sin(uv.x*170+sin(uv.y*13)*2)),12);
 float3 detail=tex2D(_MainTex,frac(uv*float2(32,3))).rgb;
 float shade=lerp(.65,1.15,ridge)-folds*.14;
 return float4(i.color.rgb*shade+(detail-.5)*.12,i.color.a);
 }
 return _Color*i.color;
 } ENDCG }
 }
}
