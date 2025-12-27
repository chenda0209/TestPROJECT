Shader "ZombieSurvivors/ScreenSpaceReflection"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _WaveTex("WaveTex", 2D) = "black" {}

        [Header(Normal)]
        [Normal]_NormalMap("NormalMap", 2D) = "bump" {}
        _NormalStrength("NormalStrength", Range(0, 10)) = 0.5
        _NormalFlowX("NormalFlowX", Range(0, 10)) = 0
        _NormalFlowY("NormalFlowY", Range(0, 10)) = 0

        [Space(20)]
        _TraceTex ("TraceTex", 2D) = "white" {}
        _TraceStrength("TraceStrength", Range(0, 10)) = 1

        [Header(Depth)]
        _DepthTest("DepthTeoSee", Range(-1, 1)) = 0

        [Header(Fresnel)]
        _FresnelPower("FresnelPower", Range(0, 1)) = 1
        _FresnelIntensity("FresnelIntensity", Range(0, 1)) = 1

        [Header(SpecularPower)]
        _SpecularPower("SpecularPower", Range(0, 1)) = 0
        _SpecularlIntensity("SpecularIntensity", Range(0, 1)) = 1

        [Header(Reflection)]
        _ReflectionStrength("ReflectionStrength", Range(0, 1)) = 0.5
        _MaxDistance ("Max Reflection Distance", Range(1, 100)) = 10 // 适当增大距离
        [IntRange]_StepCount ("Ray Step Count", Range(32, 64)) = 64 // 增加步进数提高精度
        _HitTolerance ("Hit Tolerance", Range(0, 2)) = 0.1 // 命中容差（解决精度问题）

        _ProbeStrength("ProbeStrength", Range(0, 5)) = 1

        [Header(Wave)]
        _WaveStrength("WaveStrength", Range(0.1, 1000)) = 1

        [Header(Blur)]
        _BlurSize("Blur Size", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "Rendertype" = "Opaque"

            // 关键：确保在其他不透明物体之后渲染
        }

        Pass
        {
            Name "SSR"
            Tags { "LightMode" = "UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            HLSLPROGRAM
            // 引入URP内置纹理声明（包含深度和不透明纹理）
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityEnvironment.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _NormalMap_ST;
            float _NormalStrength;
            float _NormalFlowX;
            float _NormalFlowY;
            half4 _TraceTex_ST;
            float _TraceStrength;
            float _DepthTest;
            float _FresnelPower;
            float _FresnelIntensity;
            float _SpecularPower;
            float _SpecularlIntensity;
            float4 _WaveTex_ST;
            float _ReflectionStrength;
            float _MaxDistance;
            int _StepCount;
            float _HitTolerance; // 新增：命中容差参数
            int _ProbeStrength;
            float _WaveStrength;


            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_TraceTex);
            SAMPLER(sampler_TraceTex);
            TEXTURE2D(_WaveTex);
            SAMPLER(sampler_WaveTex);

            #pragma vertex vert
            #pragma fragment frag

            // 确保宏定义正确
            // #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            // #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ENVIRONMENTREFLECTIONS_ON
            #pragma multi_compile_fragment _ _GLOSSY_ENVIRONMENT_CUBEMAP

            float3 CalculateFresnel(float3 normal, float3 viewDir, float power, float3 color)
            {
                float cos = saturate(dot(normal, viewDir));
                return (power + (1 - power) * pow(1 - cos, 5)) * color;
            }
            float3 GetNormalFromHeight(float height, float strength)
            {
                // 在切线空间中，X 和 Y 方向的梯度
                // 我们假设高度变化是基于X和Y轴的。
                // 这是一种非常简化的模型，通常用于平滑的、程序化的表面。
                float dx = ddx(height) * strength;
                float dy = ddy(height) * strength;

                // ddx 和 ddy 是导数函数，计算了高度在X和Y方向上的变化率。
                // 这两个值代表了法线的 X 和 Y 分量。
                // Z 分量是向上的，我们用它来保证法线是单位向量（长度为1）。

                // 构建法线向量，并进行归一化
                // 切线空间的法线通常是(x, y, z)，其中 z 轴指向表面外部。
                return normalize(float3(- dx, - dy, 1.0));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;
                half3 normal : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                half3 normal : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };
            float3 _PlayerPosition;
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.uv.xy = TRANSFORM_TEX(IN.uv.xy, _BaseMap);
                OUT.uv.xy = TRANSFORM_TEX(IN.uv.xy, _WaveTex);
                OUT.uv.zw = TRANSFORM_TEX(IN.uv.xy, _NormalMap);
                OUT.normal = IN.normal;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 normalUV = IN.uv.zw;
                normalUV.x += _Time.y * _NormalFlowX;
                normalUV.y += _Time.y * _NormalFlowY;
                float2 distortUV = IN.uv.xy;
                distortUV.x += _Time.y * _NormalFlowX;
                distortUV.y += _Time.y * _NormalFlowY;

                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, distortUV) * _BaseColor; // 基础颜色采样
                half waveTex = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, IN.uv.xy).r; // 反射区域光滑度采样
                float4 traceTex = SAMPLE_TEXTURE2D(_TraceTex, sampler_TraceTex, (_PlayerPosition.xz - IN.positionWS.xz) * 0.03 + 0.5);
                // if(wave < 0.05) wave = 0;
                float3 waveNormal = GetNormalFromHeight(waveTex, _WaveStrength);

                float4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV); // 法线贴图采样
                float3 tangentNormal = UnpackNormalScale(normalSample, _NormalStrength); // 法线贴图解码
                float3 trace = UnpackNormalScale(traceTex, _TraceStrength); // 法线贴图解码

                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normal); // TBN
                float3 tangentWS = normalInputs.tangentWS;
                float3 bitangentWS = normalInputs.bitangentWS;
                float3 normalWS = normalInputs.normalWS;
                float3x3 TBN = float3x3(tangentWS, bitangentWS, normalWS);
                // float3 finalNormal = mul(tangentNormal, TBN); // 最终法线
                // float3 finalwaveNormal = mul(waveNormal, TBN);
                float3 blendNormal = mul(normalize(waveNormal + tangentNormal), TBN);

                float4 positionCS = TransformWorldToHClip(IN.positionWS);
                float4 positionNDC = ComputeScreenPos(positionCS); // 计算屏幕UV
                float2 screenUV = positionNDC.xy / positionNDC.w;
                float depth = SampleSceneDepth(screenUV);
                float depthReal = LinearEyeDepth(depth, _ZBufferParams);
                float currentDepth = LinearEyeDepth(positionCS.z / positionCS.w, _ZBufferParams);
                float culDepth = saturate(depthReal - currentDepth - _DepthTest); //深度检测，得到边缘差值

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS); // 视线方向

                Light light = GetMainLight();

                // 计算射线反射方向（确保单位向量）
                float3 reflectDirWS = normalize(reflect(-viewDirWS, blendNormal));
                // 射线参数
                float3 rayOriginWS = IN.positionWS; //射线起点
                float stepLength =  _MaxDistance / _StepCount; //每次步进的距离
                float2 hitUV = 0; //声明命中的点在屏幕上的UV
                bool hit = false;
                float3 rayPosWS = rayOriginWS; //射线起点
                // 射线步进（增加精度和容错）
                int stepCount = _StepCount;
                float ang = dot(viewDirWS, blendNormal);
                [loop]
                for(int i = 1; i < stepCount; i ++) // 从1开始，避免检测自身
                {
                    float3 lastPosWS = rayPosWS; //上一次步进点
                    rayPosWS += reflectDirWS * stepLength * i * (ang);//当前步进点
                    float4 rayPosHCS = TransformWorldToHClip(rayPosWS);
                    float4 rayPosNDC = ComputeScreenPos(rayPosHCS);
                    float2 rayUV = rayPosNDC.xy / rayPosNDC.w; //每次步进后，将步进点转换为屏幕UV；
                    // 超出屏幕范围则终止
                    if (rayUV.x < 0 || rayUV.x > 1 || rayUV.y < 0 || rayUV.y > 1)
                    break;
                    // // 采样场景深度并转换为世界空间位置
                    float rayPosZ = rayPosHCS.z / rayPosHCS.w; //步进点的采样深度
                    float linerayPosZ = LinearEyeDepth(rayPosZ, _ZBufferParams);
                    float RealsceneZ = SampleSceneDepth(rayUV); //步进点在屏幕上的真实深度，记录着真实空间深度，
                    float lineRealsceneZ = LinearEyeDepth(RealsceneZ, _ZBufferParams);
                    if (lineRealsceneZ > linerayPosZ && lineRealsceneZ < linerayPosZ  + _HitTolerance) // 对比采样深度和真实深度，就知道有没有命中，第二个是防止穿透
                    {
                        [loop]
                        for (int j = 1; j < stepCount; j ++) // 从1开始，避免检测自身
                        {
                            lastPosWS += reflectDirWS * stepLength * 0.5 * (ang);
                            float4 rayPosHCS = TransformWorldToHClip(lastPosWS);
                            float4 rayPosNDC = ComputeScreenPos(rayPosHCS);
                            float2 rayUV = rayPosNDC.xy / rayPosNDC.w; //每次步进后，将步进点转换为屏幕UV；
                            // // 采样场景深度并转换为世界空间位置
                            float rayPosZ = rayPosHCS.z / rayPosHCS.w; //步进点的采样深度
                            float linerayPosZ = LinearEyeDepth(rayPosZ, _ZBufferParams);
                            float RealsceneZ = SampleSceneDepth(rayUV); //步进点在屏幕上的真实深度，记录着真实空间深度，
                            float lineRealsceneZ = LinearEyeDepth(RealsceneZ, _ZBufferParams);

                            if (lineRealsceneZ > linerayPosZ && lineRealsceneZ < linerayPosZ  + _HitTolerance * 0.2)
                            {
                                hitUV = rayUV;
                                hit = true;
                                break;
                            }
                        }
                    }
                }
                half3 reflectionColor = 0;
                if (hit)
                {
                    reflectionColor = SampleSceneColor(hitUV).rgb;
                }
                else
                {
                    // 这是在URP中手动采样立方体贴图的方法
                    reflectionColor = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDirWS, _ProbeStrength).rgb;
                    // [unroll(8)]
                    // for (uint i = 1; i < 8; i ++) // 从1开始，避免检测自身
                    // {
                        // rayPosWS += reflectDirWS * stepLength;
                        // float4 rayPosHCS = TransformWorldToHClip(rayPosWS);
                        // float4 rayPosNDC = ComputeScreenPos(rayPosHCS);
                        // float2 rayUV = rayPosNDC.xy / rayPosNDC.w;
                        // // 超出屏幕范围则终止
                        //     if (rayUV.x < 0 || rayUV.x > 1 || rayUV.y < 0 || rayUV.y > 1)
                        // break;
                        // // // 采样场景深度并转换为世界空间位置
                        // float rayPosZ = rayPosHCS.z / rayPosHCS.w; //采样深度
                        // float RealsceneZ = SampleSceneDepth(rayUV); //采样点实际深度
                        //     if (RealsceneZ > rayPosZ && RealsceneZ < rayPosZ  + _HitTolerance) // 防止穿透
                        // {
                            // hitUV = rayUV;
                            // reflectionColor = SampleSceneColor(hitUV).rgb;
                            // break;
                        // }
                    // }
                }

                // 从环境贴图采样反射颜色（URP专用采样函数）
                // SAMPLE_TEXTURECUBE
                // 这是一个 URP 原生函数。它直接来自 URP 的 Shader 库，专门用于在 URP 中采样 Cube 贴图。
                // 优点 : 功能更强大、更灵活。它提供了多个重载版本，可以让你控制采样时的细节级别（LOD）。例如，SAMPLE_TEXTURECUBE_LOD 函数就允许你指定一个 LOD 值，这对于实现基于粗糙度的反射效果（比如 PBR 中的高光反射）至关重要。
                // 缺点 : 仅限于 URP。如果你想在内置管线中使用它，代码会报错。
                // float3 reflectDir = reflect(- viewDirWS, normalWS);
                // // 在 URP 中，_ReflectionProbe 这个变量名已经被弃用，反射探针的环境贴图通常被命名为 _GlossyEnvironmentCubeMap。
                // // 所以，你在 URP 的 Shader 中会看到 _GlossyEnvironmentCubeMap 配合 UNITY_SAMPLE_TEXCUBE 或 SAMPLE_TEXTURECUBE 一起使用。
                // half3 reflectionCube = SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, reflectDirWS, 1).rgb;
                half3 h = normalize(light.direction + viewDirWS);
                half3 specular = LightingSpecular(light.color, light.direction, blendNormal, viewDirWS, half4(_SpecularlIntensity, _SpecularlIntensity, _SpecularlIntensity, 1), exp2(_SpecularPower * 10 + 1));

                float3 fresnel = CalculateFresnel(blendNormal, viewDirWS, _FresnelPower, light.color) * _FresnelIntensity; // 菲尼尔

                //混合最终颜色
                half3 finalColor = baseColor.rgb + reflectionColor * _ReflectionStrength + specular + fresnel;

                return half4(finalColor, baseColor.a * culDepth);
            }
            ENDHLSL
        }
        // Pass 2 : 模糊处理
        // Pass
        // {
            // // Blend SrcAlpha OneMinusSrcAlpha
            // // Tags { "RenderType" = "Transparent" }

            // HLSLPROGRAM
            // #pragma vertex vert
            // #pragma fragment frag

            // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            // struct Attributes
            // {
                // float4 positionOS : POSITION;
                // float2 uv : TEXCOORD0;
            // };

            // struct Varyings
            // {
                // float4 positionHCS : SV_POSITION;
                // float2 uv : TEXCOORD0;
            // };

            // Varyings vert(Attributes IN)
            // {
                // Varyings OUT;
                // OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // OUT.uv = IN.uv;
                // return OUT;
            // }

            // half4 frag(Varyings IN) : SV_Target
            // {
                // // float2 pixelSize = 1.0 / _ScreenParams.xy;

                // // half4 sum = half4(0, 0, 0, 0);

                // // // 9个像素的Box Blur
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(- 1, - 1) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(0, - 1) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(1, - 1) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(- 1, 0) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(0, 0) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(1, 0) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(- 1, 1) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(0, 1) * pixelSize * _BlurSize);
                // // sum += SAMPLE_TEXTURE2D(_CustomTex, sampler_CustomTex, IN.uv + float2(1, 1) * pixelSize * _BlurSize);

                // // return sum / 9.0;
                // return half4(0, 0, 0, 0);
            // }
            // ENDHLSL
        // }
    }
    // FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
