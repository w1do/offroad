Shader "Game/Realistic Forest Ground"
{
 Properties {
 _NeedleMap("Pine needle litter",2D)="white"{} _NeedleNormal("Needle normal",2D)="bump"{} _NeedleRough("Needle roughness",2D)="white"{}
 _MossMap("Moss and leaves",2D)="white"{} _MossNormal("Moss normal",2D)="bump"{} _MossRough("Moss roughness",2D)="white"{}
 _GrassMap("Grassy soil",2D)="white"{} _GrassNormal("Grass normal",2D)="bump"{} _GrassRough("Grass roughness",2D)="white"{}
 _MudMap("Wet forest mud",2D)="white"{} _MudNormal("Mud normal",2D)="bump"{} _MudRough("Mud roughness",2D)="white"{}
 _DirtMap("Compacted soil",2D)="white"{} _DirtNormal("Soil normal",2D)="bump"{} _DirtRough("Soil roughness",2D)="white"{}
 _RainWetness("Rain wetness",Range(0,1))=0 _MudDominance("Mud road",Range(0,1))=0
 _SoilTrackMask("Tracks",2D)="black"{} _DynamicSoil("Runtime tracks",Float)=0 _SoilMaskBounds("Mask bounds",Vector)=(0,0,1,1)
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_NeedleMap);SAMPLER(sampler_NeedleMap);TEXTURE2D(_NeedleNormal);TEXTURE2D(_NeedleRough);
 TEXTURE2D(_MossMap);TEXTURE2D(_MossNormal);TEXTURE2D(_MossRough);
 TEXTURE2D(_GrassMap);TEXTURE2D(_GrassNormal);TEXTURE2D(_GrassRough);
 TEXTURE2D(_MudMap);TEXTURE2D(_MudNormal);TEXTURE2D(_MudRough);
 TEXTURE2D(_DirtMap);TEXTURE2D(_DirtNormal);TEXTURE2D(_DirtRough);
 TEXTURE2D(_SoilTrackMask);SAMPLER(sampler_SoilTrackMask);
 CBUFFER_START(UnityPerMaterial)
 float _RainWetness,_MudDominance,_DynamicSoil;float4 _SoilMaskBounds;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 normal:NORMAL;float4 weights:COLOR;float2 road:TEXCOORD1;};
 struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 weights:TEXCOORD2;float2 road:TEXCOORD3;float2 maskUV:TEXCOORD4;float fog:TEXCOORD5;};
 V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normal);o.weights=a.weights;o.road=a.road;o.maskUV=(a.p.xz-_SoilMaskBounds.xy)/max(.001,_SoilMaskBounds.zw);o.fog=ComputeFogFactor(o.p.z);return o;}
 ENDHLSL
 Pass {
 Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma target 4.5
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
 #pragma multi_compile_fog
 half4 Frag(V i):SV_Target {
 float2 p=i.world.xz;float moss=i.weights.g;float grass=smoothstep(.34,.69,i.weights.a);float bog=i.weights.b;
 float road=saturate(i.weights.r);float side=abs(i.road.x);float centre=exp(-pow(side/.57,4))*(.58+.28*sin(i.road.y*.14));
 float dryTrack=max(exp(-pow((side-.79)/.42,2)*2),smoothstep(.9,1.9,side)*.25);
 float exposed=road*(1-centre*(1-_MudDominance*.8));
 float wet=max(_RainWetness,_MudDominance*.8);float mud=saturate(_MudDominance*.85+wet*.32+bog*.22);
 half3 needles=SAMPLE_TEXTURE2D(_NeedleMap,sampler_NeedleMap,p*.5).rgb;
 half3 green=SAMPLE_TEXTURE2D(_GrassMap,sampler_NeedleMap,p*.17).rgb;
 half3 mossColour=SAMPLE_TEXTURE2D(_MossMap,sampler_NeedleMap,p/3).rgb;
 half3 soil=SAMPLE_TEXTURE2D(_DirtMap,sampler_NeedleMap,p*.45).rgb;
 half3 muddy=SAMPLE_TEXTURE2D(_MudMap,sampler_NeedleMap,p/2.3).rgb;
 green*=half3(.68,.92,.5);mossColour*=half3(.72,.9,.53);needles*=.8;
 half3 forest=lerp(needles,mossColour,moss*.85);forest=lerp(forest,green,grass*.85);forest=lerp(forest,muddy,bog*.45);
 half3 colour=lerp(forest,lerp(soil,muddy,mud),exposed);colour=lerp(colour,green,road*centre*(1-_MudDominance)*.95);colour*=1-exposed*dryTrack*.12;
 float patch=.93+.07*sin(p.x*.23+sin(p.y*.19));colour*=patch;
 float3 nNeedle=UnpackNormal(SAMPLE_TEXTURE2D(_NeedleNormal,sampler_NeedleMap,p*.5));
 float3 nMoss=UnpackNormal(SAMPLE_TEXTURE2D(_MossNormal,sampler_NeedleMap,p/3));
 float3 nGrass=UnpackNormal(SAMPLE_TEXTURE2D(_GrassNormal,sampler_NeedleMap,p*.17));
 float3 nDirt=UnpackNormal(SAMPLE_TEXTURE2D(_DirtNormal,sampler_NeedleMap,p*.45));
 float3 nMud=UnpackNormal(SAMPLE_TEXTURE2D(_MudNormal,sampler_NeedleMap,p/2.3));
 float3 ts=normalize(lerp(lerp(lerp(nNeedle,nMoss,moss*.85),nGrass,grass*.65),lerp(nDirt,nMud,mud),exposed));
 float3 n=normalize(i.normal),t=normalize(float3(1,0,0)-n*n.x),b=cross(n,t);n=normalize(n+t*ts.x*.6+b*ts.y*.6);
 float roughForest=lerp(SAMPLE_TEXTURE2D(_NeedleRough,sampler_NeedleMap,p*.5).r,SAMPLE_TEXTURE2D(_MossRough,sampler_NeedleMap,p/3).r,moss);
 roughForest=lerp(roughForest,SAMPLE_TEXTURE2D(_GrassRough,sampler_NeedleMap,p*.17).r,grass*.65);
 float roughRoad=lerp(SAMPLE_TEXTURE2D(_DirtRough,sampler_NeedleMap,p*.45).r,SAMPLE_TEXTURE2D(_MudRough,sampler_NeedleMap,p/2.3).r,mud);
 float track=_DynamicSoil>.5?SAMPLE_TEXTURE2D(_SoilTrackMask,sampler_SoilTrackMask,i.maskUV).r:0;
 colour*=1-track*.28;colour*=1-wet*.13;
 SurfaceData surface=(SurfaceData)0;surface.albedo=colour;surface.smoothness=min(.6,1-lerp(roughForest,roughRoad,exposed)+wet*.19);surface.normalTS=float3(0,0,1);surface.occlusion=1-track*.12;surface.alpha=1;
 InputData data=(InputData)0;data.positionWS=i.world;data.positionCS=i.p;data.normalWS=n;data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);data.shadowCoord=TransformWorldToShadowCoord(i.world);data.fogCoord=i.fog;data.bakedGI=SampleSH(n);data.vertexLighting=VertexLighting(i.world,n);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);data.shadowMask=1;
 half4 result=UniversalFragmentPBR(data,surface);result.rgb=MixFog(result.rgb,i.fog);return result;
 }
 ENDHLSL
 }
 Pass {Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
 HLSLPROGRAM
 #pragma vertex ShadowVert
 #pragma fragment ShadowFrag
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
 float3 _LightDirection;float3 _LightPosition;
 float4 ShadowVert(A a):SV_POSITION {float3 p=TransformObjectToWorld(a.p.xyz),n=TransformObjectToWorldNormal(a.normal);
 #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
 float3 direction=normalize(_LightPosition-p);
 #else
 float3 direction=_LightDirection;
 #endif
 float4 position=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
 #if UNITY_REVERSED_Z
 position.z=min(position.z,UNITY_NEAR_CLIP_VALUE*position.w);
 #else
 position.z=max(position.z,UNITY_NEAR_CLIP_VALUE*position.w);
 #endif
 return position;}
 half4 ShadowFrag():SV_Target{return 0;}
 ENDHLSL }
 Pass {Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
 HLSLPROGRAM
 #pragma vertex DepthVert
 #pragma fragment DepthFrag
 float4 DepthVert(A a):SV_POSITION{return TransformObjectToHClip(a.p.xyz);} half4 DepthFrag():SV_Target{return 0;}
 ENDHLSL }
 }
}


