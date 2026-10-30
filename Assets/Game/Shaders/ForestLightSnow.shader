Shader "Game/Forest Light Snow"
{
    Properties
    {
        _BaseMap("Original forest texture", 2D) = "white" {}
        _BaseColor("Original tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.35
        _AlphaClip("Alpha clip", Float) = 0
        _Cull("Cull", Float) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0
        _SnowAmount("Light snow coverage", Range(0,1)) = 0.35
        _SnowColor("Snow color", Color) = (0.87,0.91,0.94,1)
        _SnowScale("Patch scale", Float) = 0.65
        [HideInInspector] _BumpMap("Normal", 2D) = "bump" {}
        [HideInInspector] _BumpScale("Normal scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _SnowColor;
                half _Cutoff, _AlphaClip, _Smoothness, _Metallic, _SnowAmount, _SnowScale;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2;
                float4 shadowCoord:TEXCOORD3;
                half fog:TEXCOORD4;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                o.shadowCoord=GetShadowCoord(p); o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                if(_AlphaClip>0.5) clip(base.a-_Cutoff);
                half3 normal=normalize(i.normalWS);
                // Preserve source normals for snow placement; show original double-sided foliage.
                half upward=smoothstep(0.25,0.85,normal.y);
                float patch=Noise(i.positionWS.xz*_SnowScale)*0.75+Noise(i.positionWS.xz*4.5)*0.25;
                half snow=upward*smoothstep(1-_SnowAmount-0.13,1-_SnowAmount+0.08,patch);
                normal=IS_FRONT_VFACE(front,normal,-normal);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(base.rgb,_SnowColor.rgb,snow*0.88);
                surface.metallic=_Metallic*(1-snow);
                surface.smoothness=lerp(_Smoothness,0.08,snow);
                surface.normalTS=half3(0,0,1); surface.occlusion=1; surface.alpha=1;
                InputData input=(InputData)0;
                input.positionWS=i.positionWS; input.positionCS=i.positionCS;
                input.normalWS=normal; input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=i.shadowCoord; input.fogCoord=i.fog;
                input.bakedGI=SampleSH(normal); input.vertexLighting=VertexLighting(i.positionWS,normal);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(input,surface);
                color.rgb=MixFog(color.rgb,i.fog); return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
