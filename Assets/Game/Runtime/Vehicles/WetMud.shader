Shader "Game/Wet Mud Spray"
{
    Properties
    {
        _BaseMap("Splat mask", 2D) = "white" {}
        _BaseColor("Mud colour", Color) = (0.19,0.12,0.065,1)
        _Smoothness("Wet highlight", Range(0,1)) = 0.65
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
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 colour:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 colour:COLOR; half fog:TEXCOORD3; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.colour=i.colour;
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 mask=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                half alpha=mask.a*i.colour.a*_BaseColor.a; clip(alpha-.025h);
                half3 n=normalize(i.normalWS); Light light=GetMainLight();
                half3 illumination=max(SampleSH(n),half3(.12,.12,.12))+light.color*(.25h+.75h*abs(dot(n,light.direction)));
                half3 h=normalize(light.direction+GetWorldSpaceNormalizeViewDir(i.positionWS));
                half shine=pow(saturate(abs(dot(n,h))),48)*_Smoothness*.25h;
                // Break up the uniform mask with fine wet-earth tonal variation.
                half grain=frac(sin(dot(floor(i.uv*96),float2(12.9898,78.233)))*43758.5453);
                half3 colour=_BaseColor.rgb*i.colour.rgb*illumination*(.75h+grain*.25h)+light.color*shine;
                return half4(MixFog(colour,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
