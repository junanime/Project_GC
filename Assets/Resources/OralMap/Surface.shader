Shader "OralStage/Surface" {
 Properties { _MainTex("Texture",2D)="white"{} _SecondTex("Palate",2D)="white"{} _DetailTex("Fine Surface",2D)="gray"{} _Color("Tint",Color)=(1,1,1,1) _Mode("Mode",Float)=0 _Clock("Clock",Float)=0 _Fluid("Fluid",Float)=0 _Progress("Progress",Float)=0 _Ceiling("Ceiling",Float)=0 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex,_SecondTex,_DetailTex; float4 _Color; float _Mode,_Clock,_Fluid,_Progress,_Ceiling,_Slant,_SolidFloor,_SouthFloor;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 roofUV:TEXCOORD1;float4 color:COLOR;};
 struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 roofUV:TEXCOORD1;float4 color:COLOR;};
 vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.roofUV=v.roofUV;o.color=v.color;return o;}
 fixed4 frag(vary i):SV_Target {
 float2 uv=i.uv;
 if(_Mode<.5){
 float2 q=(uv-float2(.5,.45)); float outer=length(q/float2(.485,.50)); float inner=length(q/float2(.33,.29));
 float rim=smoothstep(1.014,.994,outer)*smoothstep(.985,1.01,inner)*smoothstep(.145,.155,uv.y);
 float folds=sin(atan2(q.y,q.x)*44+sin(outer*60)*.3)*.018;
 float groove=abs(frac(outer*7+.07*sin(atan2(q.y,q.x)*9))-.5);
 float3 tissue=lerp(float3(.48,.10,.20),float3(.92,.36,.45),saturate((1.02-outer)*10))+folds;
 tissue+=float3(.14,.06,.06)*smoothstep(.46,.5,groove);
 float3 detail=tex2D(_MainTex,frac(uv*float2(10,7))).rgb;float grain=dot(detail,float3(.3,.59,.11))-.5;tissue+=grain*.32; float vessel=pow(saturate(cos(uv.x*190+sin(uv.y*41)*3)),28); tissue-=vessel*float3(.16,.06,.04);
 float3 dark=lerp(float3(.115,.023,.065),float3(.31,.065,.13),saturate(1-abs(inner-.95)*3));
 float3 c=lerp(dark,tissue,rim);
 // South root is non-walkable supporting tissue; its ribs clearly close the map.
 float south=smoothstep(.18,.06,uv.y)*smoothstep(1.04,.98,outer);
 c=lerp(c,float3(.48,.16,.25)+sin(uv.x*45+uv.y*32)*.025,south);
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
 float ellipse=length((uv-float2(.5,.44))/float2(.23,.23));
 float edge=uv.y>=.44?smoothstep(1,.985,ellipse):smoothstep(.232,.228,abs(uv.x-.5))*step(_SouthFloor,uv.y);float front=1.15-_Progress*1.5;
 float wave=sin(uv.x*26+_Clock*3)*.015+sin(uv.x*51-_Clock*4)*.006;
 float d=uv.y+(uv.x-.5)*_Slant-front+wave;float band=smoothstep(-.016,.02,d)*(1-smoothstep(.14,.26,d));
 float foam=exp(-abs(d)*160);float ripple=pow(saturate(sin(uv.y*100+uv.x*13-_Clock*5)),18);
 float3 baseC=_Fluid<.5?float3(.13,.65,.84):(_Fluid<1.5?float3(.40,.17,.065):float3(.96,.68,.19));
 float3 c=lerp(baseC,float3(.92,.98,.90),saturate(foam*.9+ripple*.28));
 return float4(c,edge*band*(.58+foam*.28)*_Color.a);
 }
 if(_Mode<3.5){float2 p=uv*2-1;float r=length(p);float mask=1-smoothstep(.94,1,r);float rim=exp(-abs(r-.84)*38);float glint=exp(-length((p-float2(-.31,.38))*float2(2.5,1.8))*13);float glint2=exp(-length(p-float2(.48,-.28))*20);float3 c=lerp(_Color.rgb,float3(1,1,.91),saturate(rim*.8+glint+glint2*.6));return float4(c,mask*(.12+rim*.65+glint*.8+glint2*.4)*_Color.a);}
 if(_Mode>5.5){float3 tissue=tex2D(_MainTex,frac(uv*float2(14,1))).rgb;float height=sin(uv.y*3.14159);return float4(tissue*lerp(.45,1.1,height),1);}
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
