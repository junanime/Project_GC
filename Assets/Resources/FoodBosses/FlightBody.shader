Shader "FoodBosses/CleanFlightBody"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _Phase("Phase",Float)=0 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float _Phase;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(vary i):SV_Target {
  float2 p=float2(i.uv.x,1-i.uv.y);
  // Atlas-neighbour scraps touch the original contour through antialiased pixels.
  // Clip only these known empty corner areas; never resample or redraw the actual body.
  if(_Phase<.5){if(p.x>.928&&p.y<.187)discard;if(p.x>.985&&p.y>.575)discard;}
  else {if(p.x<.0466&&p.y>.380)discard;if(p.x>.976&&p.y>.632)discard;}
  return tex2D(_MainTex,i.uv)*i.color;
 }
 ENDCG }
 }
}
