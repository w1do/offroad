Shader "Game/Exploration Distant Trees"
{
 Properties {_BaseMap("Tree atlas",2D)="white"{} _Cutoff("Cutout",Range(0,1))=.45}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
 Pass {Tags {"LightMode"="UniversalForward"} Cull Off
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "ForestWind.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_ExplorationLoadedMask);SAMPLER(sampler_ExplorationLoadedMask);
 CBUFFER_START(UnityPerMaterial)
 float _Cutoff;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;float2 root:TEXCOORD3;};
 V Vert(A a){UNITY_SETUP_INSTANCE_ID(a);V o;float3 root=TransformObjectToWorld(float3(0,0,0));float3 p=TransformObjectToWorld(a.p.xyz);p+=ForestWindOffset(p,float4(root.x,root.z,root.y,1),.055,.045);o.p=TransformWorldToHClip(p);o.uv=a.uv;o.normal=TransformObjectToWorldNormal(a.normal);o.root=root.xz;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 Frag(V i):SV_Target {clip(.5-SAMPLE_TEXTURE2D(_ExplorationLoadedMask,sampler_ExplorationLoadedMask,(i.root+5000)/10112).r);half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(c.a-_Cutoff);Light light=GetMainLight();c.rgb*=SampleSH(normalize(i.normal))+light.color*(.18+.55*saturate(dot(normalize(i.normal),light.direction)));c.rgb=MixFog(c.rgb,i.fog);return c;}
 ENDHLSL }
 }
}
