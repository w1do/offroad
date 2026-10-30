Shader "Game/Forest River"
{
    Properties
    {
        _ShallowColor("Shallow water",Color)=(0.07,0.27,0.30,0.65)
        _DeepColor("Deep water",Color)=(0.025,0.10,0.16,0.85)
        _FlowDirection("Flow XZ",Vector)=(0,1,0,0)
        _CurrentSpeed("Current",Float)=2.7
        _WaveScale("Wave size",Float)=1.2
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "River" Tags{"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor,_DeepColor;float4 _FlowDirection;float _CurrentSpeed,_WaveScale;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 clip:SV_POSITION;float3 p:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;half3 n:TEXCOORD3;};
            V Vert(A v){V o;o.p=TransformObjectToWorld(v.p.xyz);o.p.y+=0.006*sin(o.p.x*1.7+o.p.z*2.1-_Time.y*2);o.clip=TransformWorldToHClip(o.p);o.uv=v.uv;o.n=TransformObjectToWorldNormal(v.n);o.fog=ComputeFogFactor(o.clip.z);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 screen=GetNormalizedScreenSpaceUV(i.clip);
                float t=_Time.y*_CurrentSpeed;
                float2 q=i.p.xz*_WaveScale-_FlowDirection.xy*t;
                float a=q.x*2.2+q.y*1.3,b=q.x*4.1-q.y*2.7+t*0.3;
                half3 n=normalize(i.n+half3(cos(a)*0.035+sin(b)*0.018,0,sin(a)*0.03-cos(b)*0.016));
                float scene=LinearEyeDepth(SampleSceneDepth(screen),_ZBufferParams);
                float surface=LinearEyeDepth(i.clip.z,_ZBufferParams);
                float depth=max(0,scene-surface);
                half3 tint=lerp(_ShallowColor.rgb,_DeepColor.rgb,saturate(depth*0.6));
                half3 below=SampleSceneColor(screen+n.xz*0.004*saturate(depth));
                half3 view=GetWorldSpaceNormalizeViewDir(i.p);
                half fresnel=0.04+0.75*pow(1-saturate(dot(n,view)),5);
                half3 reflected=GlossyEnvironmentReflection(reflect(-view,n),i.p,0.12,1,screen);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.p));
                half spec=pow(saturate(dot(n,normalize(view+sun.direction))),160)*sun.shadowAttenuation;
                half3 color=lerp(below,tint,0.55)+reflected*fresnel*0.75+sun.color*spec*0.65;
                float bank=smoothstep(0.10,0.015,min(i.uv.y,1-i.uv.y));
                float streak=pow(saturate(sin(a*0.7+sin(b)*0.6)),16);
                float foam=saturate(bank*0.18+streak*0.08)*(0.5+0.5*sin(q.x*6+q.y*0.6));
                color=lerp(color,half3(0.8,0.88,0.87),foam);
                color=MixFog(color,i.fog);
                // Geometric banks provide the shoreline. Keep a visible base layer
                // even when a camera's depth copy has no valid shallow-water sample.
                float shore=smoothstep(0,0.045,min(i.uv.y,1-i.uv.y));
                float ends=smoothstep(0,0.04,min(i.uv.x,1-i.uv.x));
                return half4(color,saturate(0.78+fresnel*0.12+foam)*shore*ends);
            }
            ENDHLSL
        }
    }
}
