Shader "Vampire/Finite Colon Terrain"
{
    Properties
    {
        _MainTex("Approved colon layout",2D)="white"{}
        _DetailTex("Shared stage floor pixels",2D)="gray"{}
        _PixelSize("Shared world pixel",Float)=.050844
    }
    SubShader
    {
        Tags{"Queue"="Background" "RenderType"="Opaque"} Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Opening.cginc"
            sampler2D _MainTex,_DetailTex,_DistanceTex;
            float4 _MapRect;float _PixelSize,_WallWidth;
            struct appdata{float4 vertex:POSITION;};
            struct v2f{float4 vertex:SV_POSITION;float2 world:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
            float2 mirrorUV(float2 p){return 1-abs(frac(p*.5)*2-1);}
            fixed4 frag(v2f i):SV_Target
            {
                // All 96 chunks sample one absolute grid. Macro artwork supplies shape/color,
                // not enlarged nearest-neighbor pixels; the existing stage-one material supplies detail.
                float2 world=(floor(i.world/_PixelSize)+.5)*_PixelSize;
                float2 uv=(world-_MapRect.xy)/_MapRect.zw;
                float3 color=tex2Dlod(_MainTex,float4(uv,0,2.3)).rgb;
                float2 dataUV=uv*(511.0/512.0)+.5/512.0;
                float3 field=tex2D(_DistanceTex,dataUV).rgb;
                float d=field.r;float2 inward=normalize(field.gb+float2(.00001,0));
                float bend=max(0,_WallWidth+1-d);
                float2 cells=(world-inward*.3*bend*bend)/_PixelSize;
                float3 detail=tex2Dlod(_DetailTex,float4(mirrorUV(cells/104.5),0,0)).rgb;
                float3 reference=float3(251,151,148)/255.;
                #ifndef UNITY_COLORSPACE_GAMMA
                    reference=GammaToLinearSpace(reference);
                #endif
                float footprint=max(length(ddx(cells)),length(ddy(cells)));
                float visible=1-smoothstep(.7,2,footprint);
                // The rounded segment forms are exactly the approved macro silhouette.
                // Floor motifs compress at the foot then disappear on the clean wall crown.
                float floorMask=smoothstep(1.8,7,d);
                color=lerp(color,saturate(color*detail/reference),visible*floorMask);
                // Replace only painted cavities so five curved plates can close and open.
                float inletMask=1-smoothstep(.85,1.3,length((uv-_Inlet.xy)/_Inlet.zw));
                float outletMask=1-smoothstep(.85,1.3,length((uv-_Outlet.xy)/_Outlet.zw));
                float3 inletFill=tex2Dlod(_MainTex,float4(uv+float2(-.055,-.04),0,2.3)).rgb;
                float3 outletFill=tex2Dlod(_MainTex,float4(uv+float2(.055,.045),0,2.3)).rgb;
                color=lerp(color,inletFill,inletMask);color=lerp(color,outletFill,outletMask);
                color=drawOpening(color,uv,_Inlet,_InletOpen,.3);
                color=drawOpening(color,uv,_Outlet,_OutletOpen,-.3);
                return fixed4(saturate(color),1);
            }
            ENDCG
        }
    }
}
