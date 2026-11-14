Shader "FoodBosses/BuriedEmergence"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _CutY("Ground plane", Float) = -10000
        _Cell("Sprite cell", Vector) = (0,0,296,282)
        _NoodleMode("0 body, 1 hide noodle, 2 noodle only", Float) = 0
        _Pull("Pinned noodle displacement", Vector) = (0,0,0,0)
        _BaseY("Noodle base", Float) = 1.84
        _Length("Noodle length", Float) = .94
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize, _Cell, _Pull;
            float _CutY, _NoodleMode, _BaseY, _Length;
            struct app { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct vary { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float worldY:TEXCOORD1; };
            vary vert(app v)
            {
                vary o;
                float weight=saturate((v.vertex.y-_BaseY)/max(.01,_Length));
                v.vertex.xy+=_Pull.xy*(weight*weight*(3-2*weight));
                o.worldY=mul(unity_ObjectToWorld,v.vertex).y;
                o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
            }
            fixed4 frag(vary i):SV_Target
            {
                clip(i.worldY-_CutY);
                float2 p=i.uv*_MainTex_TexelSize.zw-_Cell.xy;p.y=_Cell.w-p.y;
                // Below the hook, x=110 also intersects the neighbouring noodle's outline.
                // Keep that outline on the body, rather than pulling a detached black shard.
                bool noodle=p.x>(p.y<48?110:137)&&p.x<(p.y<65?195:188)&&p.y<94;
                if(_NoodleMode>.5 && _NoodleMode<1.5 && noodle)discard;
                if(_NoodleMode>1.5 && !noodle)discard;
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                clip(c.a-.015);return c;
            }
            ENDCG
        }
    }
}
