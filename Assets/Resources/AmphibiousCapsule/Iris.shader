Shader "24Tu/Spiral iris" {
 Properties { _Progress("Closed",Range(0,1))=0 _Aspect("Aspect",Float)=1.7778 _MainTex("Texture",2D)="white"{} }
 SubShader { Tags {"Queue"="Overlay" "RenderType"="Transparent"} Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Progress,_Aspect;
 fixed4 frag(v2f_img i):SV_Target {float2 p=(i.uv-.5)*float2(_Aspect,1);float r=length(p);float a=atan2(p.y,p.x)+_Progress*3.8+r*2.6;float aperture=(1-_Progress)*sqrt(_Aspect*_Aspect+1)*.63;float lobes=.84+.16*sin(5*a);float edge=r-aperture*lobes;float alpha=smoothstep(-.002,.002,edge);if(_Progress<.0001)alpha=0;if(_Progress>.9999)alpha=1;return float4(0,0,0,alpha);}
 ENDCG }
 }
}
