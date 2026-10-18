// Five curved, interlocking membrane plates. No petal silhouette or beaded ornaments.
float4 _Inlet, _Outlet;
float _InletOpen, _OutletOpen;
float2 openingRotate(float2 p,float angle)
{
    float s=sin(angle),c=cos(angle);return float2(c*p.x-s*p.y,s*p.x+c*p.y);
}
float3 openingColor(float3 c)
{
    #ifndef UNITY_COLORSPACE_GAMMA
        c=GammaToLinearSpace(c);
    #endif
    return c;
}
float3 drawOpening(float3 background,float2 uv,float4 shape,float opened,float tilt)
{
    float2 p=(uv-shape.xy)/shape.zw;
    float radius=length(p);
    if(radius>1.035)return background;
    float grain=frac(sin(dot(floor(p*420),float2(12.9898,78.233)))*43758.5453)-.5;
    float3 dark=openingColor(float3(.25,.035,.075));
    float3 rose=openingColor(float3(.94,.42,.49));
    float3 peach=openingColor(float3(1,.72,.60));
    float3 shadow=openingColor(float3(.68,.20,.31));
    float3 color=lerp(dark,shadow,.12*saturate((p.y+1)*.5));
    float2 iris=p/.835;
    // Each plate translates and twists outward; the fixed circular rim masks its retreat.
    [unroll] for(int blade=0;blade<5;blade++)
    {
        float angle=(blade+.5)*(2*UNITY_PI/5)+tilt;
        float2 travel=float2(cos(angle),sin(angle))*opened*1.12;
        float2 source=openingRotate(iris-travel,-opened*.42);
        float r=length(source);
        float curve=.85*pow(saturate(1-r),2);
        float sector=frac((atan2(source.y,source.x)+curve-tilt)/(2*UNITY_PI))*5;
        float local=sector-blade;
        if(local>=0 && local<1 && r<=1.035 && radius<.86)
        {
            float edge=min(local,1-local)*max(.12,r);
            float dome=saturate(1-r*r);
            float light=saturate(.48+source.y*.22-source.x*.17+dome*.24);
            color=lerp(rose,peach,light*.58)+grain*.014;
            color=lerp(shadow,color,smoothstep(.003,.026,edge));
            if(local>.968 && r>.05)color=lerp(color,peach,.65);
        }
    }
    // One continuous, rounded lip with the stomach rim's peach/rose palette.
    if(radius>.835)
    {
        float cross=(radius-.835)/.165;
        float crown=sqrt(saturate(1-pow((cross-.49)*2,2)));
        float light=saturate(crown*.82+.12*p.y-.06*p.x);
        color=lerp(shadow,lerp(rose,peach,light),smoothstep(0,.15,cross));
        color+=grain*.01;
    }
    return lerp(background,color,1-smoothstep(.995,1.035,radius));
}
