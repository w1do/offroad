Shader "Game/Water Ripple"
{
    Properties { _Color("Ripple colour", Color) = (.65,.73,.68,1) _Opacity("Opacity", Float) = .3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Opacity;
            CBUFFER_END
            V Vert(A a) { V o; o.position=TransformObjectToHClip(a.vertex.xyz); o.uv=a.uv; return o; }
            half4 Frag(V i) : SV_Target
            {
                float r=length(i.uv*2-1);
                float rings=exp(-pow((r-.8)*65,2))+.45*exp(-pow((r-.58)*50,2));
                return half4(_Color.rgb, saturate(rings)*_Opacity*_Color.a);
            }
            ENDHLSL
        }
    }
}
