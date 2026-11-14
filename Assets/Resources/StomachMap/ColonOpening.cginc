// Colon-only wall recess. The upper/lower doorway uses the existing gameplay ellipse;
// its soft shoulders blend into the terrain instead of drawing a separate circular rim.
float4 _Inlet, _Outlet;
float _InletOpen, _OutletOpen;

float2 colonOpeningRotate(float2 p,float angle)
{
    float s=sin(angle),c=cos(angle);
    return float2(c*p.x-s*p.y,s*p.x+c*p.y);
}

float3 drawColonOpening(float3 background,float3 floorColor,float2 uv,float4 shape,float opened,float outward,float wallFootprint)
{
    float2 p=(uv-shape.xy)/shape.zw;
    p.y*=outward;
    if(abs(p.x)>1.6 || p.y>1.65 || p.y< -1.45)return background;

    // A rounded arch with straight, floor-reaching jambs. Leaving the approach edge
    // open is what makes this a hole in the wall instead of a badge on its surface.
    float2 archPoint=float2(p.x,max(p.y+.10,-.26));
    float arch=length(archPoint/float2(.72,.83));
    float frontFade=smoothstep(-1.40,-1.02,p.y);
    float shoulders=(1-smoothstep(1.12,1.64,arch))*frontFade*wallFootprint;
    float rimHeight=exp(-pow((arch-1.26)/.30,2));
    float light=saturate(.48+.30*p.y-.15*p.x);
    // Retain the existing wall's shading at every shoulder. The doorway must never
    // paint a glowing ring beyond the terrain silhouette or replace the floor.
    float3 shoulder=lerp(background,float3(1,.71,.62),rimHeight*(.10+.08*light));
    shoulder=lerp(background*float3(.72,.66,.76),shoulder,smoothstep(.94,1.20,arch));
    float3 color=lerp(background,shoulder,shoulders);

    float mouth=(1-smoothstep(.985,1.015,arch))*frontFade*wallFootprint;
    float depth=smoothstep(-1.18,.43,p.y);
    float3 throat=lerp(floorColor*.88,float3(.31,.12,.19),depth);
    // A shaded inner vault and soft side occlusion make the depth visible even closed.
    float edge=smoothstep(.55,.99,arch);
    throat=lerp(throat,float3(.43,.20,.28),edge*.55);

    // The five plates sit behind the wall, not on its bright crest. Their retraction
    // exposes the same floor leading into the tunnel, then the shadowed inner vault.
    float2 iris=float2(p.x/.70,(p.y+.04)/.83);
    float aperture=length(iris);
    [unroll] for(int blade=0;blade<5;blade++)
    {
        float angle=(blade+.5)*(2*UNITY_PI/5)+.22;
        float2 travel=float2(cos(angle),sin(angle))*opened*1.24;
        float2 source=colonOpeningRotate(iris-travel,-opened*.42);
        float r=length(source);
        float curve=.85*pow(saturate(1-r),2);
        float sector=frac((atan2(source.y,source.x)+curve-.22)/(2*UNITY_PI))*5;
        float local=sector-blade;
        if(local>=0 && local<1 && r<=1.035 && aperture<1.02)
        {
            float seam=min(local,1-local)*max(.12,r);
            float dome=saturate(1-r*r);
            float plateLight=saturate(.45+source.y*.16-source.x*.13+dome*.26);
            float3 plate=lerp(float3(.78,.40,.46),float3(1,.66,.60),plateLight);
            plate=lerp(float3(.56,.28,.35),plate,smoothstep(.002,.025,seam));
            // The fixed vault casts over the plates, tying them to the wall opening.
            plate*=1-.16*smoothstep(.70,1.02,aperture);
            throat=plate;
        }
    }
    return lerp(color,throat,mouth);
}
