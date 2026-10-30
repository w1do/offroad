Shader "Game/Living Forest"
{
    Properties
    {
        _BaseMap("Forest texture",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Cutoff("Alpha cutoff",Range(0,1))=0.35
        _AlphaClip("Alpha clip",Float)=0
        _Cull("Cull",Float)=0
        _Smoothness("Smoothness",Range(0,1))=0.15
        _Metallic("Metallic",Range(0,1))=0
        _SnowAmount("Light snow coverage",Range(0,1))=0.35
        _SnowColor("Snow",Color)=(0.87,0.91,0.94,1)
        _SnowScale("Snow scale",Float)=0.65
        _BumpMap("Normal",2D)="bump"{}
        _BumpScale("Normal strength",Range(0,2))=0
        _RoughnessMap("Roughness",2D)="white"{}
        _UseRoughness("Use roughness",Float)=0
        _UseWorldTextures("World mapping",Float)=0
        _WorldTextureScale("UV / meter",Float)=0.4
        _TreeSway("Tree bend",Float)=0
        _LocalWindAnchor("Object space wind root",Float)=0
        _LeafFlutter("Leaf flutter",Float)=0
        _FoliageLight("Leaf transmission",Range(0,1))=0
        _MudBlend("Scanned mud blend",Float)=0
        _MudMap("Scanned mud colour",2D)="white"{}
        _MudNormal("Scanned mud normal",2D)="bump"{}
        _MudHalfSize("Mud bounds and feather",Vector)=(2.6,5,1.05,0)
        _MudTint("Mud tint",Color)=(0.8,0.72,0.6,1)
        _MudOverlayFade("Fade dry decals over mud",Float)=0
        _MudHalfSize2("Second fade area",Vector)=(0,0,1,0)
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "ForestWind.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
        TEXTURE2D(_RoughnessMap);SAMPLER(sampler_RoughnessMap);
        TEXTURE2D(_MudMap);SAMPLER(sampler_MudMap);
        TEXTURE2D(_MudNormal);SAMPLER(sampler_MudNormal);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;half4 _BaseColor,_SnowColor;
        half _Cutoff,_AlphaClip,_Smoothness,_Metallic,_SnowAmount,_SnowScale;
        half _BumpScale,_UseRoughness,_UseWorldTextures,_WorldTextureScale;
        half _TreeSway,_LeafFlutter,_FoliageLight;
        half _LocalWindAnchor;
        half _MudBlend,_MudOverlayFade;float4 _MudHalfSize,_MudHalfSize2;half4 _MudTint;
        CBUFFER_END
        struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 mudUV:TEXCOORD1;float2 mudLocal:TEXCOORD3;float2 fadeLocal:TEXCOORD4;float4 anchor:TEXCOORD2;};
        float3 WindPosition(A v){float3 p=TransformObjectToWorld(v.positionOS.xyz);float4 a=v.anchor;if(_LocalWindAnchor>0.5){float3 root=TransformObjectToWorld(float3(a.x,a.z,a.y))+_ForestOriginOffset.xyz;a=float4(root.x,root.z,root.y,1);}return p+ForestWindOffset(p,a,_TreeSway,_LeafFlutter);}
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags{"LightMode"="UniversalForward"} Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog
            struct V{float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2;float4 shadow:TEXCOORD3;half fog:TEXCOORD4;half4 tangent:TEXCOORD5;float4 mud:TEXCOORD6;half3 mudTangent:TEXCOORD7;float2 fade:TEXCOORD8;};
            V Vert(A v)
            {
                V o;o.p=WindPosition(v);o.positionCS=TransformWorldToHClip(o.p);o.n=TransformObjectToWorldNormal(v.normalOS);
                o.tangent=half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.shadow=TransformWorldToShadowCoord(o.p);o.fog=ComputeFogFactor(o.positionCS.z);o.mud=float4(v.mudUV,v.mudLocal);o.mudTangent=TransformObjectToWorldDir(float3(1,0,0));o.fade=v.fadeLocal;return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);}
            half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float2 uv=lerp(i.uv,i.p.xz*_WorldTextureScale,_UseWorldTextures);
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;if(_AlphaClip>0.5)clip(base.a-_Cutoff);
                half3 n=normalize(i.n);half upward=smoothstep(0.25,0.85,n.y);
                float patch=Noise(i.p.xz*_SnowScale)*0.75+Noise(i.p.xz*4.5)*0.25;
                half snow=upward*smoothstep(1-_SnowAmount-0.13,1-_SnowAmount+0.08,patch);n=IS_FRONT_VFACE(face,n,-n);
                float edge=min(_MudHalfSize.x-abs(i.mud.z),_MudHalfSize.y-abs(i.mud.w));
                float feather=_MudHalfSize.z*(1+.18*sin(i.mud.w*2.6+i.mud.z*3.1)+.12*sin(i.mud.w*6.7));
                float mud=_MudBlend*smoothstep(0,max(.1,feather),edge);
                if(_MudOverlayFade>.5){float edge2=min(_MudHalfSize2.x-abs(i.fade.x),_MudHalfSize2.y-abs(i.fade.y));float feather2=_MudHalfSize2.z*(1+.18*sin(i.fade.y*2.6+i.fade.x*3.1)+.12*sin(i.fade.y*6.7));float fade=max(mud,smoothstep(0,max(.1,feather2),edge2));clip(base.a*(1-fade)-max(.05,_Cutoff));mud=0;}
                half3 mudColour=SAMPLE_TEXTURE2D(_MudMap,sampler_MudMap,i.mud.xy).rgb*_MudTint.rgb;
                base.rgb=lerp(base.rgb,mudColour,mud);snow*=1-mud;
                if(_BumpScale>0.001)
                {
                    half3 ts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
                    half3 t=_UseWorldTextures>0.5?normalize(float3(1,0,0)-n*n.x):normalize(i.tangent.xyz+half3(0.0001,0,0));
                    half3 b=cross(n,t)*(_UseWorldTextures>0.5?-1:i.tangent.w);n=normalize(t*ts.x+b*ts.y+n*ts.z);
                }
                float wet=ForestWetness(i.p)*(1-snow);
                if(mud>.001){half3 ts=UnpackNormalScale(SAMPLE_TEXTURE2D(_MudNormal,sampler_MudNormal,i.mud.xy),.65);half3 t=normalize(i.mudTangent-n*dot(i.mudTangent,n));half3 b=cross(n,t);n=normalize(lerp(n,t*ts.x+b*ts.y+n*ts.z,mud));}
                wet=max(wet,mud*.8);
                SurfaceData surface=(SurfaceData)0;surface.albedo=lerp(base.rgb*(1-wet*0.18),_SnowColor.rgb,snow*0.88);
                surface.metallic=_Metallic*(1-snow);
                float smooth=_UseRoughness>0.5?1-SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,uv).r:_Smoothness;
                surface.smoothness=lerp(lerp(smooth,min(0.83,smooth+0.38),wet),0.08,snow);
                surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;
                Light sun=GetMainLight(i.shadow);surface.emission=base.rgb*_FoliageLight*max(0,dot(-n,sun.direction))*sun.color*0.23;
                InputData data=(InputData)0;data.positionWS=i.p;data.positionCS=i.positionCS;data.normalWS=n;data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.p);
                data.shadowCoord=i.shadow;data.fogCoord=i.fog;data.bakedGI=SampleSH(n);data.vertexLighting=VertexLighting(i.p,n);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);data.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(data,surface);color.rgb=MixFog(color.rgb,i.fog);return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;float3 _LightPosition;
            struct S{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            S ShadowVert(A v)
            {
                S o;float3 p=WindPosition(v),n=TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection=normalize(_LightPosition-p);
                #else
                float3 lightDirection=_LightDirection;
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(p,n,lightDirection));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #endif
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;
            }
            half4 ShadowFrag(S i):SV_Target{if(_AlphaClip>0.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a-_Cutoff);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct D{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            D DepthVert(A v){D o;o.positionCS=TransformWorldToHClip(WindPosition(v));o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            half4 DepthFrag(D i):SV_Target{if(_AlphaClip>0.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a-_Cutoff);return 0;}
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
