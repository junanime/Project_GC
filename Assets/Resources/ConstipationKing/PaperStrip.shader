Shader "ConstipationKing/PaperStrip"
{
    Properties
    {
        _ColonClip("Colon walls", Float) = 0
        _Length("Unrolled world length", Float) = 1
        _FlowDistance("Signed paper travel", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../FoodBosses/ColonFloorClip.cginc"
            float _Length, _FlowDistance;
            struct app { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct vary { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 color:COLOR; };
            vary vert(app v)
            {
                vary o; o.position=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(vary i):SV_Target
            {
                ClipColonFloor(i.world);
                // Distance-based fold spacing stays constant while the strip unrolls.
                // The encounter supplies travel, so pauses also freeze the surface.
                float along=i.uv.x*max(.001,_Length)-_FlowDistance;
                float fold=abs(frac(along/.72+.5)-.5);
                float seam=1-smoothstep(.010,.022,fold);
                float perforation=seam*step(.38,frac(saturate(i.uv.y)*12));
                float edge=1-smoothstep(.015,.075,min(i.uv.y,1-i.uv.y));
                float crease=(1-smoothstep(.01,.10,fold))*.055;
                fixed3 paper=i.color.rgb+sin(saturate(i.uv.y)*3.14159)*.025;
                paper-=edge*fixed3(.12,.13,.14)+crease;
                paper=lerp(paper,fixed3(.66,.51,.34),perforation*.65);
                return fixed4(paper,i.color.a);
            }
            ENDCG
        }
    }
}
