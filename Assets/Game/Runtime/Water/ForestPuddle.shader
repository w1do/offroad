Shader "Game/Forest Puddle"
{
    Properties
    {
        _BaseMap("CC0 muddy bottom", 2D) = "white" {}
        _BumpMap("Source puddle normal", 2D) = "bump" {}
        _BaseColor("Water tint", Color) = (.22,.28,.28,.95)
        _Smoothness("Smoothness", Range(0,1)) = .94
        _NormalStrength("Ripple strength", Range(0,1)) = .035
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURECUBE(_PuddleReflectionCube); SAMPLER(sampler_PuddleReflectionCube);
            float _PuddleReflectionReady;
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _Smoothness, _NormalStrength;
            CBUFFER_END
            struct A { float4 position:POSITION; float4 color:COLOR; };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; float depth:TEXCOORD1; float fog:TEXCOORD2; };
            V Vert(A a) { V o; o.world=TransformObjectToWorld(a.position.xyz); o.position=TransformWorldToHClip(o.world); o.depth=a.color.r; o.fog=ComputeFogFactor(o.position.z); return o; }
            half4 Frag(V i):SV_Target
            {
                float2 uv=i.world.xz*.55;
                float3 n1=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv+_Time.y*float2(.013,.009)));
                float3 n2=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv*1.7+_Time.y*float2(-.009,.011)));
                float3 n=normalize(float3((n1.x+n2.x)*_NormalStrength,2,(n1.y+n2.y)*_NormalStrength));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
                float3 reflection=GlossyEnvironmentReflection(reflect(-view,n),1-_Smoothness,1);
                if(_PuddleReflectionReady>.5)reflection=SAMPLE_TEXTURECUBE_LOD(_PuddleReflectionCube,sampler_PuddleReflectionCube,reflect(-view,n),0).rgb;
                Light light=GetMainLight();
                float spec=pow(saturate(dot(n,normalize(view+light.direction))),lerp(32,512,_Smoothness));
                float3 mud=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv*.8).rgb;
                float3 colour=lerp(mud*.65,_BaseColor.rgb,saturate(i.depth*4))*(SampleSH(n)+light.color*.35);
                colour=lerp(colour,reflection,saturate(fresnel*.85+.32))+spec*light.color*.65;
                float alpha=smoothstep(0,.012,i.depth)*lerp(.68,.98,saturate(i.depth*5));
                return half4(MixFog(colour,i.fog),alpha*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
