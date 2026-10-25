Shader "FoodBosses/ArticulatedBody"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _Cell("Sprite cell",Vector)=(0,0,296,282) _Phase("Phase",Float)=0 _TwoNoodles("Curtain",Float)=0 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;float4 _Cell;float _Phase,_TwoNoodles;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(vary i):SV_Target {
  float2 p=i.uv*_MainTex_TexelSize.zw-_Cell.xy;p.y=_Cell.w-p.y;
  // Only the raised center noodle is replaced by the articulated limb. Keep bowl, feet and other noodles.
  float left=lerp(p.y<65?110:137,p.y<64?129:147,_Phase);
  float right=lerp(p.y<65?195:188,p.y<64?205:199,_Phase);
  float bottom=lerp(94,102,_Phase);
  if(p.x>left&&p.x<right&&p.y<bottom)discard;
  float2 second=p-float2(14,5)*_Phase;
  if(_TwoNoodles>.5 && ((second.y<90&&second.x>25&&second.x<119)||(second.y<113&&second.y>=90&&second.x>74&&second.x<103)))discard;
  return tex2D(_MainTex,i.uv)*i.color;
 }
 ENDCG }
 }
}
