Shader "ColonEnding/SummonReveal"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) [HideInInspector] _RendererColor("Renderer",Color)=(1,1,1,1) [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1) _ClipY("World reveal height",Float)=-9999 _KeepAbove("Keep above",Float)=1 _RevealBlend("Reveal blend",Float)=1 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"}
  Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnitySprites.cginc"
   float _ClipY,_KeepAbove,_RevealBlend;
   struct output {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float worldY:TEXCOORD1;};
   output vert(appdata_t input){v2f s=SpriteVert(input);output o;o.vertex=s.vertex;o.color=s.color;o.uv=s.texcoord;o.worldY=mul(unity_ObjectToWorld,UnityFlipSprite(input.vertex,_Flip)).y;return o;}
   fixed4 frag(output input):SV_Target {fixed4 c=SampleSpriteTexture(input.uv)*input.color;float visible=smoothstep(_ClipY-.035,_ClipY+.035,input.worldY);c.a*=lerp(1,lerp(1-visible,visible,_KeepAbove),_RevealBlend);c.rgb*=c.a;return c;}
   ENDCG
  }
 }
}
