Shader "Vampire/PhoenixWind"
{
 Properties { _AugmentOpacity("Opacity",Range(0,1))=1 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; };
 struct v2f { float4 pos:SV_POSITION; fixed4 color:COLOR; };
 float _AugmentOpacity;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;return o;}
 fixed4 frag(v2f i):SV_Target {i.color.a*=_AugmentOpacity;return i.color;}
 ENDCG }
 }
}
