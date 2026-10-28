Shader "24Tu/Submerged travel outlet" {
 Properties { _Opened("Opened",Float)=0 _Clock("Clock",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+110" "RenderType"="Transparent"} Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "../StomachMap/Opening.cginc"
 float _Opened,_Clock;
 fixed4 frag(v2f_img i):SV_Target {float2 p=(i.uv-.5)*2.12;float r=length(p);float alpha=1-smoothstep(1,1.035,r);float2 uv=i.uv+float2(sin(i.uv.y*42+_Clock*2)*.001,0);float3 c=drawOpening(float3(.64,.77,.13),uv,float4(.5,.5,1/2.12,1/2.12),_Opened,0);c=lerp(c,float3(.65,.79,.18),.13);return float4(c,alpha*.93);}
 ENDCG }
 }
}
