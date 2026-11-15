#ifndef COLON_SHELL_ARTICULATION
#define COLON_SHELL_ARTICULATION
float2 ShellFinSource(float2 q,float side,float sweep)
{
    float2 pivot=side<.5?float2(.40,.27):float2(.66,.23);
    float angle=(sweep-.5)*.63*(side<.5?1:-1);
    float2 d=q-pivot;
    float region=(1-smoothstep(.39,.55,q.y))*(side<.5?1-smoothstep(.35,.48,q.x):smoothstep(.63,.72,q.x));
    float2 inverse=float2(d.x*cos(angle)+d.y*sin(angle),-d.x*sin(angle)+d.y*cos(angle));
    return lerp(q,pivot+inverse,region);
}
#endif
