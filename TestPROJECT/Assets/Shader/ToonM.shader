Shader "ZombieSurvivors/ToonM"
{
    Properties
    {
        [Header(Rendering Options)]
        [KeywordEnum(Off, Back, Front)] _CullMode("Cull Mode", Float) = 2

        [Header(Base Color)]
        _Color("Color", Color) = (1, 1, 1, 1)
        _BaseMap("BaseMap", 2D) = "white" {}

        [Header(Step LIGHT)]
        [Toggle]_STEPLIGHT("StepLight (on/off)", int) = 0
        _DarkThreshold("DarkThreshold", Range(-0.5, 0.5)) = 0
        _DarkColor("DarkColor", Color) = (0.8, 0.8, 0.8, 1)

        [Header(Specular)]
        [Toggle]_SPECULAR("Specular (on/off)", int) = 0
        _SpecularThreshold("SpecularThreshold", Range(0.5, 1)) = 0.5
        // _SpecularPower("SpecularPower", Range(32, 1024)) = 1
        _SpecularIntensity("SpecularIntensity", Range(0, 10)) = 1

        [Header(Receive Shadow)]
        [Toggle]_RECEIVESHADOW("ReceiveShadow (on/off)", int) = 0

        [Header(Alpha Clip)]
        [Toggle]_CLIP("OpenClip", int) = 0
        _AlphaClip("AlphaClip", range(0, 1)) = 0

        [Header(Emission)]
        [Toggle]_USEEMISSION("Emission (on/off)", int) = 0
        [Toggle]_USEBREATH("UseBreath (on/off)", int) = 0
        _BreathSpeed("BreathSpeed", range(0, 100)) = 0
        [HDR]_EmissionColor("EmissionColor", Color) = (1, 1, 1, 1)
        _EmissionMap("EmissionMap", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #pragma vertex vert
        #pragma fragment frag
        #pragma shader_feature_local_fragment _STEPLIGHT_ON 
        #pragma shader_feature_local_fragment _SPECULAR_ON 
        #pragma shader_feature_local_fragment _RECEIVESHADOW_ON 
        #pragma shader_feature_local_fragment _CLIP_ON 
        #pragma shader_feature_local_fragment _USEEMISSION_ON 
        #pragma shader_feature_local_fragment _USEBREATH_ON

        #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile_fragment _ _SHADOWS_SOFT

        CBUFFER_START(UnityPerMaterial)
        real4 _Color;

        real _DarkThreshold;
        real4 _DarkColor;

        real _SpecularThreshold;
        real _SpecularIntensity;
        
        real4 _EmissionColor;
        real _BreathSpeed;
        real _AlphaClip;

        real4 _BaseMap_ST;

        real4 _EmissionMap_ST;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        ENDHLSL

        Pass
        {
            Tags {"LightMode" = "UniversalForward"}
            ZWrite On
            ZTest LEqual
            Cull [_CullMode]

            HLSLPROGRAM


            struct Attributes
            {
                real4 vertex : POSITION;
                real4 uv : TEXCOORD0;
                real3 normal : NORMAL;
            };

            struct Varyings
            {
                real4 vertex : SV_POSITION;
                real4 uv : TEXCOORD0;
                real3 normal : TEXCOORD2;
                real3 positionWS : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = positionInputs.positionCS;
                o.positionWS = positionInputs.positionWS;


                // o.uv.xy = TRANSFORM_TEX(v.uv.xy, _BaseMap);
                // o.uv.zw = TRANSFORM_TEX(v.uv.xy, _EmissionMap);
                VertexNormalInputs NormalInputs = GetVertexNormalInputs(v.normal);
                o.normal = NormalInputs.normalWS;
                // o.positionWS = positionInputs.positionWS;
                return o;
            }

            real4 frag (Varyings i) : SV_Target
            {
                // real4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy);

                real3 normalWS = i.normal;

                // #if defined(_CLIP_ON)
                // clip(baseMapColor.a - _AlphaClip);
                // #endif

                
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                real shadowAmount = MainLightRealtimeShadow(shadowCoord);
                // real shadowAmount = 1;
                #if defined(_RECEIVESHADOW_ON)
                shadowAmount = MainLightRealtimeShadow(shadowCoord);
                #endif

                real3 outputColor = light.color * light.shadowAttenuation  * _Color.rgb;

                #if defined(_USEEMISSION_ON)
                real emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv.zw).r;
                #if defined(_USEBREATH_ON)
                real breath = (sin(_Time.y * _BreathSpeed) * 0.5 + 0.5);
                outputColor += emissionMap * _EmissionColor.rgb * breath;
                #else
                outputColor += emissionMap * _EmissionColor.rgb;
                #endif
                #endif

                real3 stepLight = 1;
                real3 specular = 0;
                #if defined(_STEPLIGHT_ON)
                real cos = dot(normalWS, light.direction);
                stepLight = step(_DarkThreshold, cos)? light.color : _DarkColor.rgb;
                #endif

                #if defined(_SPECULAR_ON)
                real calculateSpecular = step(_SpecularThreshold, dot(normalWS, light.direction));
                specular = light.color * calculateSpecular * _SpecularIntensity;
                #endif

                return real4(outputColor * stepLight, 1);
                // return real4(light.color * light.shadowAttenuation * baseMapColor.rgb * _Color.rgb , 1);
            }
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "ShadowCaster"}
            ColorMask 0
            HLSLPROGRAM

            struct Attributes
            {
                real4 position : POSITION;
            };

            struct Varyings
            {
                real4 position : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.position = TransformObjectToHClip(v.position.xyz);
                return o;
            }

            real4 frag(Varyings i) : SV_Target
            {
                return 1;
            }

            ENDHLSL
        }

    }
}