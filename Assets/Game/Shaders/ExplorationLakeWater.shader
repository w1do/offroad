Shader "Game/Exploration Lake Water"
{
 Properties {_BaseColor("Deep lake colour",Color)=(.025,.1,.13,.94) _BumpMap("Water ripples",2D)="bump"{} _NormalStrength("Ripple strength",Float)=.3}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass {Tags {"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);float4 _ForestWindState;
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor;float _NormalStrength;
 CBUFFER_END
 struct A {float4 p:POSITION;};struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;float2 shore:TEXCOORD2;};
 V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.p.z);o.shore=a.p.xz/float2(820,580);return o;}
 half4 Frag(V i):SV_Target {
 float wind=clamp(_ForestWindState.z,.4,2.7),time=_Time.y;
 float2 uv=i.world.xz*.17;
 float3 a=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv+time*float2(.015,.01)*wind));
 float3 b=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv*1.73-time*float2(.01,.018)*wind));
 float2 ripple=(a.xy+b.xy)*_NormalStrength+float2(sin(i.world.x*.9+time*1.6),cos(i.world.z*.7-time*1.4))*.035*wind;
 float3 n=normalize(float3(ripple.x,1,ripple.y));float3 view=GetWorldSpaceNormalizeViewDir(i.world);
 float fresnel=pow(1-saturate(dot(n,view)),4);Light light=GetMainLight();
 float3 reflection=GlossyEnvironmentReflection(reflect(-view,n),.12,1);
 float3 colour=lerp(_BaseColor.rgb*(SampleSH(n)+light.color*.25),reflection,.12+fresnel*.55);
 float spec=pow(saturate(dot(n,normalize(view+light.direction))),180);colour+=spec*light.color*.6;
 float radius=length(i.shore);float foam=smoothstep(.978,.992,radius)*(1-smoothstep(.996,1,radius))*(.3+.2*sin(time*1.5+i.world.x*.8+i.world.z*.5));
 colour=lerp(colour,half3(.55,.66,.65),foam);
 return half4(MixFog(colour,i.fog),_BaseColor.a);
 }
 ENDHLSL }
 }
}
