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
            #include "ColonOpening.cginc"
            sampler2D _DistanceTex,_WallCoordinateTex,_WallTex;
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
                // Approved D walls: a soft peach crest, a distinct rose side face and a
                // narrow contact shadow. Geometry and the original floor above stay intact.
                float width=lerp(_WallWidth,_WallWidth*1.65,exterior);
                float radius=width*(.94+.06*lobe);
                float rise=saturate(1-lerp(max(d,0),abs(d-1),exterior)/radius);
                float sideLight=1-smoothstep(.46,.80,rise);
                float crown=smoothstep(.35,.78,rise);
                float facing=saturate(.5+n.y*.38-n.x*.20);
                float sideHeight=smoothstep(.015,.46,rise);
                float3 wall=lerp(float3(.65,.31,.39),float3(.90,.48,.49),sideHeight);
                wall*=.92+.12*facing;
                wall=lerp(wall,float3(1,.65,.58),crown*(.76+.06*lobe));
                float shine=pow(facing,3)*smoothstep(.38,.62,rise)*(1-smoothstep(.86,.99,rise));
                wall=lerp(wall,float3(1,.79,.66),shine*.34);
                // Broad, low contrast lobes replace the old dark muscle creases.
                wall*=1-.055*pow(saturate(-lobe),2)*(1-crown*.4);
                float along=atan2(phase.y,phase.x)/(2*UNITY_PI)+.5;
                float2 tissueUV=float2(along,lerp(.015,.975,sqrt(rise)));
                float lod=max(0,log2(max(.001,footprint)));
                float3 painted=tex2Dlod(_WallTex,float4(tissueUV,0,lod)).rgb;
                float3 shifted=tex2Dlod(_WallTex,float4(frac(along+.5),tissueUV.y,0,lod)).rgb;
                painted=lerp(painted,shifted,smoothstep(.36,.49,abs(along-.5)));
                #ifndef UNITY_COLORSPACE_GAMMA
                    painted=LinearToGammaSpace(painted);
                #endif
                // Keep the original painted tissue only as subtle mottling, never grooves.
                wall=lerp(wall,painted*lerp(.96,1.03,facing),.075*sideLight);
                float contact=(1-smoothstep(radius-.08,radius+.64,d))*.38;
                float cast=(1-smoothstep(radius,radius+1.25,d))*.18*(1-facing*.45);
                float3 color=lerp(floorColor,floorColor*float3(.66,.58,.69),saturate(contact+cast));
                color=lerp(color,wall,1-smoothstep(radius-.38,radius,d));
                // Outside only the perimeter: inner ridge centers remain colored tissue.
                float outside=exterior*(1-smoothstep(-3.0,-2.4,d));
                color=lerp(color,float3(.29,.065,.13),outside);
                float wallFootprint=(1-outside)*(1-smoothstep(radius-.38,radius,d));
                color=drawColonOpening(color,floorColor,uv,_Inlet,_InletOpen,1,wallFootprint);
                color=drawColonOpening(color,floorColor,uv,_Outlet,_OutletOpen,-1,wallFootprint);
                return fixed4(rgb(saturate(color)),1);
            }
            ENDCG
        }
    }
}
