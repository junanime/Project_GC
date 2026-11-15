Shader "ColonBosses/ShellYaw"
{
    Properties
    {
        [PerRendererData] _MainTex("Approved sprite",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        [MaterialToggle] PixelSnap("Pixel snap",Float)=0
        [HideInInspector] _RendererColor("RendererColor",Color)=(1,1,1,1)
        [HideInInspector] _Flip("Flip",Vector)=(1,1,1,1)
        [PerRendererData] _AlphaTex("Alpha",2D)="white"{}
        [PerRendererData] _EnableExternalAlpha("External alpha",Float)=0
        _Frame("Frame rect",Vector)=(0,0,1,1)
        _Yaw("Horizontal angle",Float)=0
        _Spin("Spin blend",Float)=0
        _Sweep("Fin sweep",Float)=0
        _SweepActive("Fin articulation",Float)=0
        _Side("Fin side",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "ShellArticulation.cginc"
            float4 _Frame;
            float _Yaw,_Spin,_Sweep,_SweepActive,_Side;
            fixed4 At(float2 q){return SampleSpriteTexture(_Frame.xy+q*_Frame.zw);}
            fixed4 frag(v2f i):SV_Target
            {
                float2 q=(i.texcoord-_Frame.xy)/_Frame.zw;
                float2 source=q;
                if(_SweepActive>.5)
                {
                    source=ShellFinSource(q,_Side,_Sweep);
                }
                fixed4 c=At(source);
                // Spin uses a complete eight-view turntable sprite. No dome-only UV
                // warp or overlay may leave the rim/underside stationary beneath it.
                c*=i.color;c.rgb*=c.a;return c;
            }
            ENDCG
        }
    }
}
