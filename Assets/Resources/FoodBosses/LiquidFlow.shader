Shader "FoodBosses/ContinuousLiquid"
{
 Properties { _MainTex("Texture",2D)="white"{} }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct app { float4 vertex:POSITION;float4 color:COLOR; };
 struct vary {float4 position:SV_POSITION;float2 world:TEXCOORD0;float4 color:COLOR;};
 vary vert(app v){vary o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color;return o;}
 fixed4 frag(vary i):SV_Target{
  float2 p=floor(i.world*42)/42;float wave=sin(p.x*13+p.y*9-_Time.y*5)+sin(p.y*22-p.x*5+_Time.y*3);
  float foam=step(1.50,wave);float shade=step(.35,wave)*.1-step(wave,-.7)*.08;
  return fixed4(lerp(i.color.rgb+shade,float3(1,.98,.85),foam*.65),i.color.a);
 }
 ENDCG }
 }
}
