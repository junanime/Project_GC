Shader "Vampire/ShiniBreath"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) _FrameUV("Frame",Vector)=(0,0,1,1) _Head("Reveal",Float)=1 _Tail("Erase",Float)=-1 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"} Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct vf {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex;float4 _FrameUV;fixed4 _Color;float _Head,_Tail;
 vf vert(app v){vf o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
 fixed4 frag(vf i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv)*i.color;float2 uv=(i.uv-_FrameUV.xy)/_FrameUV.zw;float wave=sin(uv.y*39+_Time.y*9)*.018+sin(uv.y*81)*.01;c.a*=1-smoothstep(_Head-.04,_Head+.02,uv.x);c.a*=smoothstep(_Tail+wave-.04,_Tail+wave+.02,uv.x);return c;}
 ENDCG }
 }
}
