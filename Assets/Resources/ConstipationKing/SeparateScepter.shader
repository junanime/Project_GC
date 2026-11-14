Shader "ConstipationKing/SeparateScepter"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        _HandOnly("Foreground grip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"
            float4 _StaffHead, _StaffShaft, _StaffHand;
            float _HandOnly;
            fixed4 frag(v2f IN):SV_Target
            {
                // All approved sheets use a 480-square reference canvas, including
                // the 1254-square original. UV remains the full source texture even
                // when the pose sprite excludes its obsolete underside nozzles.
                float2 p=float2(IN.texcoord.x,1-IN.texcoord.y)*480;
                fixed4 c=SampleSpriteTexture(IN.texcoord);
                float grip=1-smoothstep(.88,1,length((p-_StaffHand.xy)/_StaffHand.zw));
                if (_HandOnly>.5) c.a*=grip;
                else
                {
                    float head=1-smoothstep(.96,1,length((p-_StaffHead.xy)/_StaffHead.zw));
                    // The downward slam's paper tail extends diagonally beyond
                    // the roll ellipse. This outer-only lobe never touches skin.
                    float tail=1-smoothstep(.96,1,length((p-(_StaffHead.xy+float2(30,38)))/float2(25,23)));
                    head=max(head,tail*step(300,_StaffHead.x));
                    c.a*=1-head;
                    float2 a=_StaffShaft.xy,b=_StaffShaft.zw;
                    float2 axis=normalize(b-a),normal=float2(axis.y,-axis.x);
                    float q=dot(p-a,axis),lengthAxis=length(b-a);
                    float across=dot(p-a,normal),radius=14;
                    float shaft=(1-smoothstep(radius-1,radius,abs(across)))*step(-2,q)*step(q,lengthAxis+14);
                    // The old narrow gold shaft was painted over the cloak/tank.
                    // Reconstruct that tiny strip from its two immediate neighbours;
                    // keep the authored hand intact as a separate foreground layer.
                    float2 center=a+axis*q;
                    float2 left=center-normal*17,right=center+normal*17;
                    fixed4 l=SampleSpriteTexture(float2(left.x/480,1-left.y/480));
                    fixed4 r=SampleSpriteTexture(float2(right.x/480,1-right.y/480));
                    fixed4 underneath=lerp(l,r,saturate((across+17)/34));
                    c=lerp(c,underneath,shaft*(1-grip)*(1-head));
                }
                c*=IN.color; c.rgb*=c.a; return c;
            }
            ENDCG
        }
    }
}
