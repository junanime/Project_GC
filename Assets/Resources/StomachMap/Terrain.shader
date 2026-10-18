Shader "Vampire/Finite Stomach Terrain"
{
    Properties
    {
        _MainTex ("Stomach layout", 2D) = "white" {}
        _DetailTex ("Fine pixel material", 2D) = "gray" {}
        _WallTex ("Approved fine-dot folded wall", 2D) = "white" {}
        _PixelSize ("World pixel size", Float) = .050844
        _DetailStrength ("Detail contrast", Range(0,1)) = 1
        _LayoutLod ("Macro smoothing", Range(0,3)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Opening.cginc"
            sampler2D _MainTex, _DetailTex, _DistanceTex, _WallTex, _WallCoordinateTex;
            float _WallWidth;
            float _PixelSize, _DetailStrength, _LayoutLod;
            float4 _MapRect;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float2 world:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o;
            }
            float2 mirrorUV(float2 p) { return 1-abs(frac(p*.5)*2-1); }
            fixed4 frag(v2f i):SV_Target
            {
                float2 cells=i.world/_PixelSize;
                float2 grid=floor(cells)+.5;
                float2 world=grid*_PixelSize;
                float2 uv=(world-_MapRect.xy)/_MapRect.zw;
                // Geometry coordinates are finer than the flat-floor motifs. The same world
                // mapping is used on all chunks and by the full TAB map camera.
                float2 wallWorld=(floor(i.world/(_PixelSize*.25))+.5)*(_PixelSize*.25);
                float2 wallUV=(wallWorld-_MapRect.xy)/_MapRect.zw;
                float2 dataUV=wallUV*(511.0/512.0)+.5/512.0;
                float3 field=tex2D(_DistanceTex,dataUV).rgb;
                float distance=field.r;
                float2 inward=normalize(field.gb+float2(.000001,0));
                float rise=saturate(1-distance/_WallWidth);
                // Large-scale shape/color is filtered; it is NOT enlarged nearest-neighbor art.
                float3 baseColor=tex2Dlod(_MainTex,float4(uv,0,_LayoutLod)).rgb;
                // Remove the old painted cavities before drawing the smaller animated ones.
                // Only macro color is replaced; the existing wall folds and floor pixels remain.
                float oldInlet=1-smoothstep(.78,1.08,length((uv-float2(.844,.865))/float2(.051,.087)));
                float oldOutlet=1-smoothstep(.82,1.12,length((uv-float2(.195,.300))/float2(.071,.104)));
                float3 inletFill=tex2Dlod(_MainTex,float4(uv.x-.09,uv.y-.075,0,_LayoutLod)).rgb;
                float3 outletFill=tex2Dlod(_MainTex,float4(uv.x+.11,uv.y+.10,0,_LayoutLod)).rgb;
                baseColor=lerp(baseColor,inletFill,oldInlet);
                baseColor=lerp(baseColor,outletFill,oldOutlet);
                // Both layers use absolute world coordinates so neighboring mesh chunks join exactly.
                // Surface arc-length grows as the floor bends upward: compress the motifs
                // along the wall normal, without shrinking them along the wall tangent.
                float bend=max(0,_WallWidth+1-distance);
                float2 surface=world-inward*(.30*bend*bend);
                float2 tile=(surface/_PixelSize)/104.5;
                float3 a=tex2Dlod(_DetailTex,float4(mirrorUV(tile),0,0)).rgb;
                // Detail fades before it becomes sub-pixel, keeping the overview free of shimmer.
                float2 surfaceCells=surface/_PixelSize;
                float footprint=max(length(ddx(surfaceCells)),length(ddy(surfaceCells)));
                float visible=1-smoothstep(.7,2.0,footprint);
                float terrain=smoothstep(.20,.42,dot(baseColor,float3(.299,.587,.114)));
                // Preserve the approved peach/rose motif colors and density, without a second
                // rotated texture washing out or doubling the markings. Macro shading still
                // supplies the bowl/rim colors. At spawn the reference tile colors are unchanged.
                float3 referenceFloor=float3(251,151,148)/255.0;
                #ifndef UNITY_COLORSPACE_GAMMA
                    referenceFloor=GammaToLinearSpace(referenceFloor);
                #endif
                float3 detailed=a*baseColor/referenceFloor;
                float3 floorColor=saturate(lerp(baseColor,detailed,_DetailStrength*visible*terrain));
                // Fold artwork follows the actual closed boundary, rather than a flat
                // oversized screenshot. Cross-fade the texture edges without mirror pinches.
                float2 phase=tex2D(_WallCoordinateTex,dataUV).rg;
                float along=atan2(phase.y,phase.x)/(2*UNITY_PI)+.5;
                float wallLod=max(0,log2(max(.001,length(ddx(wallWorld))+length(ddy(wallWorld))))+6);
                float v=lerp(.035,.965,rise);
                float3 wall=tex2Dlod(_WallTex,float4(along,v,0,wallLod)).rgb;
                float3 seam=tex2Dlod(_WallTex,float4(frac(along+.5),v,0,wallLod)).rgb;
                wall=lerp(wall,seam,smoothstep(.38,.49,abs(along-.5)));
                float3 outside=float3(.27,.055,.095),rimLight=float3(1,.78,.66);
                float3 rimMid=float3(.98,.46,.53),rimShadow=float3(.72,.22,.36);
                #ifndef UNITY_COLORSPACE_GAMMA
                    outside=GammaToLinearSpace(outside);rimLight=GammaToLinearSpace(rimLight);
                    rimMid=GammaToLinearSpace(rimMid);rimShadow=GammaToLinearSpace(rimShadow);
                #endif
                // Floor detail continues onto the lower ramp, densely foreshortened, then
                // gradually gives way to the clean colored folds (no pasted-on edge).
                float blend=smoothstep(0,.27,rise);
                float3 compressedMotifs=lerp(float3(1,1,1),a/referenceFloor,
                    (1-smoothstep(.12,.48,rise))*visible*_DetailStrength);
                wall*=compressedMotifs;
                float3 color=lerp(floorColor,wall,blend);
                // Rounded raised lip: a narrow underside shadow and broad peach-lit crown.
                color=lerp(color,rimShadow,.30*exp(-pow((distance-.65)/.25,2)));
                color=lerp(rimShadow,color,smoothstep(-.25,.03,distance));
                float lip=1-smoothstep(.42,.68,abs(distance-.02));
                float crown=floor(saturate(1-abs(distance+.10)/.64)*7)/7;
                color=lerp(color,lerp(rimMid,rimLight,crown),lip);
                color=lerp(outside,color,smoothstep(-.72,-.44,distance));
                // Retain the approved esophagus/outlet landmarks and tube silhouette.
                float tube=(1-smoothstep(.043,.058,abs(uv.x-.858)))*smoothstep(.86,.94,uv.y);
                color=lerp(color,baseColor,tube);
                color=drawOpening(color,wallUV,_Inlet,_InletOpen,.3);
                color=drawOpening(color,wallUV,_Outlet,_OutletOpen,-.3);
                return fixed4(saturate(color),1);
            }
            ENDCG
        }
    }
}
