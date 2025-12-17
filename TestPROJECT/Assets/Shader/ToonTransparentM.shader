Shader "ZombieSurvivors/ToonTransparentM"
{
    Properties
    {
        [Header(Rendering Options)]
        [KeywordEnum(Off, Back, Front)] _CullMode("Cull Mode", Float) = 2 // 默认为Front，Cull 0 等于 Cull Off，Cull 1 等于 Cull Back，Cull 2 等于 Cull Front
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [KeywordEnum(Off, On)] _ZWrite("ZWrite", Float) = 0

        [Header(Base Color)]
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseMap ("BaseMap", 2D) = "white" {}
        [Toggle]_UVFLOW("UV flow (on/off)", int) = 0
        _FlowX("FlowX", range(-10, 10)) = 0
        _FlowY("FlowY", range(-10, 10)) = 0

        [Header(Alpha Clip)]
        [Toggle]_CLIP("OpenClip", int) = 0
        _AlphaClip ("AlphaClip", range(0, 1)) = 0

        [Header(Receive Shadow)]
        [Toggle]_RECEIVESHADOW("ReceiveShadow (on/off)", int) = 0

        [Header(Fresnel)]
        [Toggle]_FRESNEL("Fresnel (on/off)", int) = 0
        _FresnelPower("FresnelPower", Range(1, 10)) = 0
        [HDR]_FresnelColor("FresnelColor", Color) = (1, 1, 1)
        _FresnelIntensity("FresnelIntensity", Range(0, 1)) = 0

        [Header(Emission)]
        [Toggle]_USEEMISSION("Emission (on/off)", int) = 0
        [HDR]_EmissionColor("EmissionColor", Color) = (0, 0, 0)
        _EmissionMap ("EmissionMap", 2D) = "white" {}

        [Header(UseBreath)]
        [Toggle]_USEBREATH("UseBreath (on/off)", int) = 0
        _BreathSpeed("BreathSpeed", range(0, 100)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }


        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half4 _BaseMap_ST;
        half4 _EmissionMap_ST;
        half _FlowX;
        half _FlowY;

        half _AlphaClip;
        half _BreathSpeed;
        half3 _EmissionColor;

        half _FresnelPower;
        half3 _FresnelColor;
        half _FresnelIntensity;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        float3 CalculateFresnel(float3 normal, float3 viewDir, float power, float3 color)
        {
            float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), power);
            return fresnel * color;
        }

        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _UVFLOW_ON
            #pragma shader_feature_local _USEEMISSION_ON
            #pragma shader_feature_local _CLIP_ON
            #pragma shader_feature_local _USEBREATH_ON
            #pragma shader_feature_local _RECEIVESHADOW_ON
            #pragma shader_feature_local _FRESNEL_ON

            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE


            struct appdata
            {
                half4 vertex : POSITION;
                half3 n : NORMAL;
                half4 uv : TEXCOORD0;
            };

            struct v2f
            {
                half4 vertex : SV_POSITION;
                half3 n : NORMAL;
                half4 uv : TEXCOORD0;
                half3 positionWS : TEXCOORD2;
                half3 viewDirWS : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = positionInputs.positionCS;
                o.uv.xy = TRANSFORM_TEX(v.uv.xy, _BaseMap);
                o.uv.zw = TRANSFORM_TEX(v.uv.xy, _EmissionMap);

                o.viewDirWS = normalize(GetCameraPositionWS() - positionInputs.positionWS);

                VertexNormalInputs NormalInputs = GetVertexNormalInputs(v.n);
                o.n = NormalInputs.normalWS;

                o.positionWS = positionInputs.positionWS;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 BaseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy);
                
                
                #if defined(_UVFLOW_ON)
                i.uv.z += _FlowX * _Time.y;
                i.uv.w += _FlowY * _Time.y;
                #endif
                
                #if defined(_CLIP_ON)
                clip(BaseMapColor.a - _AlphaClip);
                #endif

                half shadowAmount = 1;
                #if defined(_RECEIVESHADOW_ON)
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                shadowAmount = MainLightRealtimeShadow(shadowCoord);
                #endif

                half3 finalColor = shadowAmount * BaseMapColor.rgb * _Color.rgb;
                half3 emission = 0;

                half alpha = BaseMapColor.a * _Color.a;

                #if defined(_USEEMISSION_ON)
                half emissionMapColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv.zw).r;
                emission = _EmissionColor * emissionMapColor;
                #endif

                #if defined(_FRESNEL_ON)
                half3 fresnel = CalculateFresnel(i.n, i.viewDirWS, _FresnelPower, _FresnelColor) * _FresnelIntensity;
                emission += fresnel;
                #endif

                #if defined(_USEBREATH_ON)
                half breath = (sin(_Time.y * _BreathSpeed) * 0.5 + 0.5);
                emission *= breath;
                #endif

                return half4(finalColor + emission , alpha);

            }
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "ShadowCaster"}
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                half4 vertex : POSITION;
            };

            struct v2f
            {
                half4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}