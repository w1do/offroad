Shader "Game/Exploration Distant Ground"
{
 Properties { _BaseMap("Forest floor",2D)="white"{} }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 TEXTURE2D(_ExplorationLoadedMask);SAMPLER(sampler_ExplorationLoadedMask);
 struct A {float4 p:POSITION;float3 normal:NORMAL;};
 struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;};
 V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normal);o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 Frag(V i):SV_Target {
 float mask=SAMPLE_TEXTURE2D(_ExplorationLoadedMask,sampler_ExplorationLoadedMask,(i.world.xz+5000)/10112).r;clip(.5-mask);
 Light light=GetMainLight();half3 n=normalize(i.normal);
 half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.xz*.35).rgb*(SampleSH(n)+light.color*saturate(dot(n,light.direction)));
 return half4(MixFog(c,i.fog),1);
 }
 ENDHLSL
 }
 }
}
