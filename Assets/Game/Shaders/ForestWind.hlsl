#ifndef GAME_FOREST_WIND_INCLUDED
#define GAME_FOREST_WIND_INCLUDED
float4 _ForestWindState;
float4 _ForestOriginOffset;
float4 _ForestWetArea0, _ForestWetArea1;
float3 ForestWindOffset(float3 p, float4 anchor, float sway, float flutter)
{
    anchor.xy -= _ForestOriginOffset.xz;
    anchor.z -= _ForestOriginOffset.y;
    float height=max(0,p.y-anchor.z),phase=dot(anchor.xy,float2(0.17,0.23)),time=_ForestWindState.w;
    float gust=0.65+0.22*sin(time*0.63+phase)+0.13*sin(time*1.27+phase*0.7);
    float bend=height*saturate(height/16)*sway*gust*_ForestWindState.z;
    float leaf=sin(time*3.6+dot(p,float3(1.8,0.7,1.3)))*flutter*_ForestWindState.z*saturate(height/2);
    return float3(_ForestWindState.x*bend+leaf,abs(leaf)*0.13,_ForestWindState.y*bend+leaf*0.4);
}
float ForestWetness(float3 p)
{
    float a=saturate(1-distance(p.xz,_ForestWetArea0.xz)/max(1,_ForestWetArea0.w));
    float b=saturate(1-distance(p.xz,_ForestWetArea1.xz)/max(1,_ForestWetArea1.w));
    return max(a,b)*0.65;
}
#endif

