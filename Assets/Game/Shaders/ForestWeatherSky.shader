Shader "Game/Forest Weather Sky"
{
    Properties { _Daylight("Daylight", Range(0,1)) = 1 _CloudCover("Cloud cover", Range(0,1)) = 0.4 _Rain("Rain", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Daylight, _CloudCover, _Rain;
            float4 _SunDirection, _CloudOffset;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.direction = v.positionOS.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            float fbm(float2 p) { float n=0, a=0.55; for(int i=0;i<5;i++) { n+=a*noise(p); p=p*2.03+17.1; a*=0.5; } return n; }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d=normalize(i.direction);
                float up=saturate(d.y);
                float3 horizon=lerp(float3(0.025,0.037,0.065),float3(0.65,0.77,0.87),_Daylight);
                float3 zenith=lerp(float3(0.004,0.009,0.025),float3(0.12,0.35,0.68),_Daylight);
                float3 col=lerp(horizon,zenith,pow(up,0.45));
                float sunDot=dot(d,normalize(_SunDirection.xyz));
                float dusk=(1-_Daylight)*smoothstep(-0.15,0.02,_SunDirection.y)*smoothstep(0.3,1,sunDot)*pow(1-up,4);
                col+=float3(0.85,0.22,0.045)*dusk;
                float2 p=d.xz/max(0.12,d.y+0.15)*2.2+_CloudOffset.xy;
                float n=fbm(p);
                float cloud=smoothstep(0.85-_CloudCover*0.65,1.04-_CloudCover*0.65,n)*smoothstep(0,0.14,d.y);
                float light=fbm(p+0.25);
                float3 cloudColor=lerp(float3(0.035,0.046,0.07),lerp(float3(0.53,0.60,0.68),float3(0.97,0.96,0.94),light),_Daylight);
                cloudColor*=lerp(1,0.48,_Rain);
                col=lerp(col,cloudColor,cloud);
                float visible=smoothstep(-0.015,0.025,d.y)*(1-cloud);
                float disc=smoothstep(0.99994,0.99997,sunDot);
                float glow=pow(saturate(sunDot),256)*0.35;
                col+=(disc*3+glow)*float3(1,0.8,0.5)*visible*(1-_Rain*0.85);
                float moon=smoothstep(0.99986,0.99993,dot(d,-normalize(_SunDirection.xyz)));
                col+=moon*float3(0.55,0.65,0.85)*visible*(1-_Daylight);
                float stars=step(0.9988,hash(floor(d.xz/max(0.08,abs(d.y))*650)));
                col+=stars*float3(0.5,0.6,0.8)*(1-_Daylight)*visible;
                col=lerp(col,horizon*0.6,saturate(-d.y*8));
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
