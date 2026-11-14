Shader "ColonEnding/LockLayer"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor",Color)=(1,1,1,1)
        [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
        _SpriteRect("Atlas rect",Vector)=(0,0,1,1)
        _Part("0 whole, 1 stone, 2 shackle",Float)=0
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"}
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            float4 _SpriteRect;
            float _Part;
            fixed4 frag(v2f IN):SV_Target
            {
                fixed4 c=SampleSpriteTexture(IN.texcoord)*IN.color;
                float2 p=(IN.texcoord-_SpriteRect.xy)/_SpriteRect.zw;
                // Matching complementary masks keep the original image intact at rest.
                // The raised stone crown remains with the body; only the gold bow lifts.
                float crown=1-smoothstep(.12,.27,abs(p.x-.50));
                float cut=.630+.036*crown;
                float bow=smoothstep(cut-.0015,cut+.0015,p.y);
                if(_Part>.5 && _Part<1.5)c.a*=1-bow;
                if(_Part>1.5)c.a*=bow;
                c.rgb*=c.a;return c;
            }
            ENDCG
        }
    }
}
