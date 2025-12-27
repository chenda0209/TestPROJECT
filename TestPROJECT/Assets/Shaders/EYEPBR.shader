Shader "Custom/Eye"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _PositionOS ("PositionOS", Vector) = (1, 1, 1, 1)
        _Specular("Specular", Range(0, 1)) = 0
        _Iris("Iris", Range(0, 10)) = 0
        _Strange("Strange", Range(0, 10)) = 0
        _PupilSize("PupilSize", Range(0, 1)) = 0
        _Refract("_Refract", Range(0, 2)) = 0
        _Offset("Offset", float) = 0
        _BaseMap("BaseMap", 2D) = "white" {}
        _EmissionMap("EmissionMap", 2D) = "black" {}
        [HDR]_EmissionColor("EmissionColor", Color) = (1, 1, 1, 1)
        [Normal]_NormalMap("NormalMap", 2D) = "bump" {}
        [Toggle]_RECEVERSHADOW("ReceverShadow", int) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        HLSLINCLUDE

        // define的正确使用，_SPECULAR_SETUP是在BRDF.HLSL文件中，如果#define _SPECULAR_SETUP 写在#include下面，编译时，#ifdef _SPECULAR_SETUP会找不到定义，应该会有先后顺序，所以后写#define _SPECULAR_SETUP无法生效，但是使用multi_compile就不同，他是告诉编译器全局分支
        // 所以define的使用要遵循一个核心，先定义再使用，先define，再if判断
        // #define _SPECULAR_SETUP
        // #if defined(_RECEVERSHADOW_ON)
        // #define _ADDITIONAL_LIGHT_SHADOWS
        // #define _ADDITIONAL_LIGHT
        // #endif
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

        ENDHLSL
        Pass
        {
            Name "Eyes"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // #pragma multi_compile_instancing
            // #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            // #pragma multi_compile_fragment _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma shader_feature _RECEVERSHADOW_ON
            // #pragma multi_compile _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma shader_feature _ADDITIONAL_LIGHT_SHADOWS
            #pragma shader_feature _ADDITIONAL_LIGHT
            // #pragma multi_compile _ADDITIONAL_LIGHT_VERTEX
            // #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _SHADOWS_SOFT _SHADOWS_SOFT_HIGH
            // #pragma shader_feature _SPECULAR_SETUP
            // #pragma shader_feature _RECEVERSHADOW_ON
            // #if defined(_RECEVERSHADOW_ON)
            // #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // #endif
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _PositionOS;
            float _Specular;
            float _Iris;
            float _Strange;
            float _PupilSize;
            float _Refract;
            float _Offset;
            float4 _BaseMap_ST;
            float4 _EmissionMap_ST;
            float4 _EmissionColor;
            float4 _NormalMap_ST;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // UNITY_INSTANCING_BUFFER_START(InstanceProps)
            // // UNITY_DEFINE_INSTANCED_PROP(float4, _Color) // 每个实例的颜色
            // UNITY_INSTANCING_BUFFER_END(InstanceProps)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normalWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD1;
                float4 position : TEXCOORD4;
            };

            v2f vert (appdata i)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(i.vertex.xyz);
                o.position = i.vertex;
                VertexNormalInputs normalInput = GetVertexNormalInputs(i.normal);
                o.normalWS = normalInput.normalWS;
                o.tangentWS = normalInput.tangentWS;
                o.bitangentWS = normalInput.bitangentWS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // 放大虹膜
                float2 irisRange = (i.uv - 0.5) * _Iris;
                // 计算 到中心距离
                float range = length(irisRange);
                // 中心渐变到周围并强化淡出
                float strange = 1 - saturate((pow (range, _Strange)));
                // float strange = (pow (range, _Strange)) / range;

                // 获取物体的世界空间位置
                float3 positionWS = TransformObjectToWorld(i.position.xyz);

                // 获取视向量
                float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
                // float3 viewDir = normalize(GetCameraPositionWS() - positionWS);
                // 折射方向
                float3 refractDir;
                // 如果视线与表面角度超过90度不计算，只有小于90度才计算折射
                // if (dot(i.normalWS, viewDir) > 0)
                refractDir = refract(-viewDir, normalize(i.normalWS), _Refract);
                // if (dot(refractDir, refractDir) < 0.001)
                // refractDir = half3(0, 0, 0);

                // 偏移后的折射位置
                float2 uvc = i.uv + refractDir.xy / refractDir.z * strange * _Offset;



                float2 uvcenter = float2(0.5, 0.5);
                float l = length(i.uv - uvcenter);
                float RefractRange = smoothstep(1 / _Iris, 1 / _Iris + 0.005, l);


                float2 uv = lerp(uvc, i.uv, RefractRange);


                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvc); // 基础颜色采样
                float4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvc); // 法线贴图采样
                float3 tangentNormal = UnpackNormalScale(normalSample, 1); // 法线贴图解码
                float emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvc).a * strange; // 法线贴图采样

                float3x3 TBN = float3x3(normalize(i.tangentWS), normalize(i.bitangentWS), normalize(i.normalWS));
                float3 n = mul(tangentNormal, TBN);
                float3 normalWS = normalize(i.normalWS);

                half alpha = 1;


                #ifdef _RECEVERSHADOW_ON

                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light light = GetMainLight(shadowCoord);

                BRDFData brdfData;
                InitializeBRDFData(_Color.rgb * baseColor.rgb, 0, half3(1, 0, 0), _Specular, alpha, brdfData);

                // float3 brdf = DirectBRDF(brdfData, normalWS, light.direction, viewDir) * LightingLambert(light.color, light.direction, normalWS) * light.shadowAttenuation;
                float3 lambert = LightingLambert(light.color, light.direction, normalWS) * light.shadowAttenuation;
                float3 specular = DirectBRDFSpecular(brdfData, normalWS, light.direction, viewDir);

                float3 brdf = (brdfData.diffuse + specular * brdfData.specular) * lambert;

                uint lightsCount = GetAdditionalLightsCount();
                if (lightsCount > 0) {
                    for(uint index = 0; index < lightsCount; index ++)
                    {
                        // 1. 获取当前附加光信息
                        // GetAdditionalLight只有光线，第三个参数是bakeshadow，如果没有，写一个占位也可以。或者使用AdditionalLightRealtimeShadow，他们的值都是additionalLight.shadowAttenuation
                        Light additionalLight = GetAdditionalLight(index, positionWS, 1);
                        // 2. 计算光照（例如使用 Lambert 模型）
                        // 注意：附加光通常需要考虑其距离衰减（attenuation）
                        float shadowAttenuation = AdditionalLightRealtimeShadow(index, positionWS, additionalLight.direction);
                        float attenuation = additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
                        // 3. 计算光照颜色并累加
                        // brdf += DirectBRDF(brdfData, normalWS, additionalLight.direction, viewDir) * LightingLambert(additionalLight.color, additionalLight.direction, normalWS) * attenuation;
                        float3 lambert = LightingLambert(additionalLight.color * attenuation, additionalLight.direction, normalWS);
                        float3 specular = DirectBRDFSpecular(brdfData, normalWS, additionalLight.direction, viewDir);
                        brdf += (brdfData.diffuse + specular * brdfData.specular) * lambert;
                    }
                }

                // 2. 计算菲涅尔项
                // half fresnelTerm = FresnelSchlick(NoV, brdfData.specular);
                // -- - ** 获取间接光照值 (采样发生在这些函数的内部) -- -

                // 3. 获取间接漫反射 (来自光照探针或环境光)
                // - 函数内部处理了 SH 解码或 Cubemap 采样
                // half3 indirectDiffuse = SampleEnvironmentDiffuse(positionWS, normalWS);

                // 4. 获取间接镜面反射 (来自反射探针)
                // - 函数内部处理了 Cubemap 采样，并根据 Roughness 选择了 Mipmap 级别

                // -- - ** 组合间接光照贡献 -- -

                // 5. 调用 EnvironmentBRDF 组合最终的间接光照
                // half3 indirectLight = EnvironmentBRDF(
                // brdfData,
                // indirectDiffuse, // 传入已采样的间接漫反射颜色
                // indirectSpecular, // 传入已采样的间接镜面反射颜色
                // 0
                //);

                float3 gi = SampleSH(i.normalWS);
                half3 indirectSpecular = GlobalIllumination(brdfData, gi, 1, normalWS, viewDir);
                return float4(brdf + indirectSpecular + emission * _EmissionColor.rgb, 1);
                // return float4(strange, strange, strange, 1);

                #else

                Light light = GetMainLight();
                float3 col = LightingLambert(light.color, light.direction, normalize(i.normalWS));


                float3 specular = LightingSpecular(light.color, light.direction, normalize(i.normalWS), viewDir, float4(light.color, 1), _Specular);
                float3 gi = SampleSH(i.normalWS);

                return float4(_Color.rgb * (col + gi) * baseColor.rgb + emission * _EmissionColor.rgb + specular, 1);
                #endif


            }
            ENDHLSL
        }

        // UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Tags {"LightMode" = "ShadowCaster"}
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // GPU Instancing
            // #pragma multi_compile_instancing
            // #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            ENDHLSL
        }
    }
}
