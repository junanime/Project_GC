sampler2D _ColonFloorTex;
float4 _ColonFloorRect,_ColonOrigin;
float _ColonClip;
float ColonFloor(float2 p){float2 uv=(p-_ColonFloorRect.xy)/_ColonFloorRect.zw;if(any(uv<0)||any(uv>1))return -1;return tex2Dlod(_ColonFloorTex,float4(uv,0,0)).r;}
void ClipColonFloor(float2 p){
 if(_ColonClip<.5)return;
 clip(ColonFloor(p)-.1);
 float2 ray=p-_ColonOrigin.xy;float len=length(ray);float walked=0;ray/=max(.001,len);
 // Conservative distance marching; no ground VFX on the far side of a ridge.
 [loop] for(int k=0;k<96;k++){float d=ColonFloor(_ColonOrigin.xy+ray*walked)-.1;clip(d);walked+=clamp(d*.7,.08,1);if(walked>=len)return;}
 clip(-1);
}
