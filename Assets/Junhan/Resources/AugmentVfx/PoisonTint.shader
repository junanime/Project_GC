Shader "Vampire/PoisonBodyTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _AugmentOpacity ("Augment opacity", Range(0,1)) = 1
        _BodyMask ("Boss clipping", Float) = 0
        _Phase ("Boss phase", Float) = 0
        _Cell ("Sprite cell", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _AugmentOpacity;
            float _BodyMask, _Phase; float4 _Cell, _MainTex_TexelSize;
            struct V { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct F { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            F vert(V v) { F o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(F i):SV_Target {
                if (_BodyMask > .5 && _BodyMask < 1.5) {
                    float2 p=float2(i.uv.x,1-i.uv.y);
                    if(_Phase<.5){if(p.x>.928&&p.y<.187)discard;if(p.x>.985&&p.y>.575)discard;}
                    else {if(p.x<.0466&&p.y>.380)discard;if(p.x>.976&&p.y>.632)discard;}
                } else if (_BodyMask > 1.5) {
                    float2 p=i.uv*_MainTex_TexelSize.zw-_Cell.xy;p.y=_Cell.w-p.y;
                    float left=lerp(p.y<65?110:137,p.y<64?129:147,_Phase);
                    float right=lerp(p.y<65?195:188,p.y<64?205:199,_Phase);
                    if(p.x>left&&p.x<right&&p.y<lerp(94,102,_Phase))discard;
                }
                return fixed4(i.color.rgb,tex2D(_MainTex,i.uv).a*i.color.a*_AugmentOpacity);
            }
            ENDCG
        }
    }
}
