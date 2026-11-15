// Continuous raised doorway shoulders, shared with ColonOpeningGeometry.
float4 _Inlet, _Outlet;
float _InletOpen, _OutletOpen;
float3 colonPaint(float along,float height,float facing)
{
    float cell=frac(along);
    float2 uv=float2(lerp(.282,.511,cell),lerp(.205,.775,saturate(height)));
    float4 painted=tex2Dlod(_WallTex,float4(uv,0,0));
    #ifndef UNITY_COLORSPACE_GAMMA
    painted.rgb=LinearToGammaSpace(painted.rgb);
    #endif
    float3 base=lerp(float3(.63,.28,.36),float3(1,.68,.59),height);
    float seam=smoothstep(0,.10,cell)*(1-smoothstep(.90,1,cell));
    return lerp(base,painted.rgb,painted.a*.87*seam)*lerp(.93,1.03,facing);
}
float2 colonOpeningRotate(float2 p,float angle)
{
    float s=sin(angle),c=cos(angle);
    return float2(c*p.x-s*p.y,s*p.x+c*p.y);
}
float colonMouthWidth(float y)
{
    float flare=saturate((-1.05-y)/1.25);
    return .745+.38*flare*flare*(3-2*flare);
}
void mergeColonOpeningSurface(float2 uv,float4 shape,float outward,inout float height,inout float2 normal,inout float along,inout float wallMask)
{
    float2 p=(uv-shape.xy)/shape.zw;p.y*=outward;
    if(abs(p.x)>1.98 || p.y>1.85 || p.y< -2.60)return;
    float2 nearest;
    if(p.y<-.20)nearest=float2((p.x>=0?1:-1)*1.28,max(-1.05,p.y));
    else
    {
        float angle=atan2((p.y+.20)/1.34,p.x/1.28);
        nearest=float2(cos(angle)*1.28,-.20+sin(angle)*1.34);
    }
    float2 offset=p-nearest;
    float tube=length(offset);
    float aa=max(.008,fwidth(tube)*.8);
    float mask=1-smoothstep(.52-aa,.52+aa,tube);
    float raised=sqrt(saturate(1-pow(tube/.52,2)));
    float blend=smoothstep(-.20,.20,raised-height)*mask;
    float arcAngle=acos(clamp(nearest.x/1.28,-1,1));
    if(nearest.y<-.20)arcAngle=nearest.x>0?(nearest.y+.20)/1.34:UNITY_PI-(nearest.y+.20)/1.34;
    float portalAlong=arcAngle/UNITY_PI*.8+.5;
    float2 phase=lerp(float2(cos(along*2*UNITY_PI),sin(along*2*UNITY_PI)),
        float2(cos(portalAlong*2*UNITY_PI),sin(portalAlong*2*UNITY_PI)),blend);
    along=atan2(phase.y,phase.x)/(2*UNITY_PI);
    float2 portalNormal=normalize(offset+float2(.00001,0));portalNormal.y*=outward;
    normal=normalize(lerp(normal,portalNormal,blend)+float2(.00001,0));
    // The shoulders join the existing wall's height field, not a painted U overlay.
    // At every outer edge, the taller original tissue remains completely unchanged.
    height=max(height,raised*mask);
    wallMask=max(wallMask,mask);
    // Bevel the newly carved approach into the old tissue. A plain floor-colored
    // mask would leave a rectangular cut through the wall at the end of each jamb.
    float arch=length(float2(p.x/colonMouthWidth(p.y),max(p.y+.20,0)/.89));
    float front=smoothstep(-2.55,-1.55,p.y);
    height*=lerp(1,smoothstep(.98,1.45,arch),front);
}
float3 drawColonOpening(float3 background,float3 floorColor,float2 uv,float4 shape,float opened,float outward)
{
    float2 p=(uv-shape.xy)/shape.zw;p.y*=outward;
    if(abs(p.x)>1.35 || p.y>1.05 || p.y< -2.55)return background;
    float2 archPoint=float2(p.x/colonMouthWidth(p.y),max(p.y+.20,0)/.89);
    float arch=length(archPoint);
    float mouth=(1-smoothstep(.97,1.06,arch))*smoothstep(-2.55,-1.55,p.y);
    float depth=smoothstep(-1.15,.55,p.y);
    float3 throat=lerp(floorColor,float3(.23,.075,.13),depth*.96);
    throat=lerp(throat,float3(.40,.19,.25),smoothstep(.66,1,arch)*depth*.48);
    float2 iris=float2(p.x/.745,(p.y+.12)/.90);
    // The closed membrane reaches the same floor lip as the open tunnel. Its bottom
    // cannot float above an already exposed strip of floor.
    float apertureMask=(1-smoothstep(.98,1.02,arch))*smoothstep(-1.08,-1.02,p.y);
    [unroll] for(int blade=0;blade<5;blade++)
    {
        float mid=(blade+.5)*(2*UNITY_PI/5)-.314159;
        float2 travel=float2(cos(mid),sin(mid))*opened*1.48;
        float2 source=colonOpeningRotate(iris-travel,-opened*.34);
        float r=length(source);
        float angle=atan2(source.y,source.x)+.82*pow(saturate(1-r),2);
        float sector=abs(atan2(sin(angle-mid),cos(angle-mid)));
        float angularAA=.006/max(.12,r);
        float mask=(1-smoothstep(UNITY_PI/5-angularAA,UNITY_PI/5+angularAA,sector))
            *(1-smoothstep(1.30,1.45,r))*apertureMask*(1-smoothstep(.72,1,opened));
        float seam=smoothstep(UNITY_PI/5-.035/max(.2,r),UNITY_PI/5,sector);
        float plateLight=saturate(.55+source.y*.18-source.x*.12+.20*saturate(1-r*r));
        float3 plate=lerp(float3(.74,.36,.43),float3(1,.68,.61),plateLight);
        plate=lerp(plate,float3(.53,.25,.33),seam*.48);
        plate*=1-.15*smoothstep(.68,1.02,arch);
        throat=lerp(throat,plate,mask);
    }
    return lerp(background,throat,mouth);
}
