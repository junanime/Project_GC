Shader "Vampire/Finite Colon Terrain"
{
    Properties
    {
        _MainTex("Layout reference",2D)="white"{}
        _DetailTex("Legacy floor reference",2D)="gray"{}
        _WallTex("Coral tissue wall",2D)="white"{}
        _PixelSize("World pixel",Float)=.050844
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
            sampler2D _DistanceTex,_WallCoordinateTex,_WallTex;
            #include "ColonOpening.cginc"
            float4 _MapRect;float _PixelSize,_WallWidth;
            struct appdata{float4 vertex:POSITION;};
            struct v2f{float4 vertex:SV_POSITION;float2 world:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float quant(float v,float steps){return floor(v*steps+.5)/steps;}
            float3 rgb(float3 c){
                #ifndef UNITY_COLORSPACE_GAMMA
                c=GammaToLinearSpace(c);
                #endif
                return c;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // Absolute world pixel coordinates are shared by every mesh chunk and minimap.
                float2 p=(floor(i.world/_PixelSize)+.5)*_PixelSize;
                float2 uv=(p-_MapRect.xy)/_MapRect.zw;
                float2 dataUV=uv*(511./512.)+.5/512.;
                float4 field=tex2D(_DistanceTex,dataUV);
                float d=field.r;
                float2 n=normalize(field.gb+float2(.00001,0));
                float exterior=saturate(field.a);
                float2 phase=tex2D(_WallCoordinateTex,dataUV).rg;
                float lobe=cos(atan2(phase.y,phase.x)*3)*saturate(length(phase));
                float tissue=quant(.5+.22*sin(p.x*.38+p.y*.16)+.18*sin(p.y*.48-p.x*.09),10);
                float3 floorColor=lerp(float3(.98,.59,.43),float3(1,.69,.51),tissue);
                float wave=sin(p.y*1.35-p.x*.42+sin(p.x*.3)*.9+sin(p.x*.047)*.6);
                floorColor=lerp(floorColor,float3(1,.76,.57),.4*smoothstep(.55,.86,wave));
                // Sparse crosses and tissue cells, never screen-sized enlarged source pixels.
                float2 cell=floor(p/1.8),f=frac(p/1.8)-.5;
                float rnd=hash(cell),cx=(hash(cell+11)-.5)*.5,cy=(hash(cell+27)-.5)*.5;
                float2 a=abs(f-float2(cx,cy));
                float cross=max(step(a.x,.025)*step(a.y,.10),step(a.x,.10)*step(a.y,.025));
                float dots=step(max(a.x,a.y),.035)+step(max(abs(a.x-.10),abs(a.y-.05)),.026);
                float motif=(rnd>.91?cross:rnd<.38?saturate(dots):0);
                float footprint=max(length(ddx(i.world)),length(ddy(i.world)))/_PixelSize;
                motif*=1-smoothstep(1.2,3.8,footprint);
                floorColor=lerp(floorColor,float3(1,.84,.66),motif*.53);
                // Raised, hand-painted soft lobes. Surface height is separate from the
                // navigable foot, so the side reads as height instead of floor shading.
                float along=atan2(phase.y,phase.x)/(2*UNITY_PI)+.5;
                float lobeCell=frac(along*3);
                float lobeShape=-cos(lobeCell*2*UNITY_PI)*saturate(length(phase));
                float width=lerp(_WallWidth,_WallWidth*1.65,exterior);
                float radius=width;
                float t=saturate(max(0,d)/radius);
                float innerHeight=sqrt(saturate(1-t*t));
                float outerT=saturate((d+3.35)/(radius+3.35));
                float outerHeight=pow(saturate(sin(outerT*UNITY_PI)),.68);
                float height=lerp(innerHeight,outerHeight,exterior)*(1-.05*(1-lobeShape));
                float aa=max(_PixelSize*.7,fwidth(d)*.8);
                float outside=exterior*(1-smoothstep(-3.35-aa,-3.35+aa,d));
                float wallMask=(1-smoothstep(radius-aa,radius+aa,d))*(1-outside);
                height*=wallMask;
                float paintAlong=along*3;
                mergeColonOpeningSurface(uv,_Inlet,1,height,n,paintAlong,wallMask);
                mergeColonOpeningSurface(uv,_Outlet,-1,height,n,paintAlong,wallMask);
                float facing=saturate(.55+(n.y*.35-n.x*.18)*saturate(1-height));
                float3 wall=colonPaint(paintAlong,height,facing);
                float crest=pow(height,7)*(1-.16*lobeShape);
                wall=lerp(wall,float3(1,.77,.66),crest*.22);
                float softFold=pow(saturate(-lobeShape),3)*(1-height*.55);
                wall*=1-.07*softFold;
                float contact=(1-smoothstep(radius,radius+.33,d))*.40;
                float cast=(1-smoothstep(radius,radius+1.05,d))*.14*(1-facing*.55);
                float3 color=lerp(floorColor,floorColor*float3(.65,.56,.67),saturate(contact+cast));
                color=lerp(color,float3(.29,.065,.13),outside);
                color=lerp(color,wall,wallMask);
                color=drawColonOpening(color,floorColor,uv,_Inlet,_InletOpen,1);
                color=drawColonOpening(color,floorColor,uv,_Outlet,_OutletOpen,-1);
                return fixed4(rgb(saturate(color)),1);
            }
            ENDCG
        }
    }
}
