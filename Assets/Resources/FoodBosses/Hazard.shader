Shader "FoodBosses/HatchedHazard"
{
 Properties { _Opacity("Opacity",Float)=1 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Opacity;
 struct app { float4 vertex:POSITION;float4 color:COLOR; };
 struct vary { float4 position:SV_POSITION;float2 world:TEXCOORD0;float4 color:COLOR; };
 vary vert(app v){vary o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color;return o;}
 fixed4 frag(vary i):SV_Target {
  float stripe=step(.77,frac((i.world.x+i.world.y)*2.4));
  float pulse=.93+.07*sin(_Time.y*7);
  return fixed4(i.color.rgb*(.72+stripe*.28),i.color.a*(.58+stripe*.42)*pulse*_Opacity);
 }
 ENDCG }
 }
}
