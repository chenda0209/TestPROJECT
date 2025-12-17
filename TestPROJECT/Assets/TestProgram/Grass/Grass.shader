Shader "Custom/Grass"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex ("MainTex", 2D) = "white" {}
        _NoiseTex ("NoiseTex", 2D) = "white" {}
        _AlphaClip("_AlphaClip", Range(0, 1)) = 0.1
        _WaveStrength("_WaveStrength", Range(0, 1)) = 0.1
        _WaveSpeed_X("_WaveSpeed_X", Range(-1, 1)) = 0.1
        _WaveSpeed_Y("_WaveSpeed_Y", Range(-1, 1)) = 0.1
        [Power]_WaveTiling("_WaveTiling", Range(0.001, 1)) = 0.1
    }
    SubShader
    {
        Tags 
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100
        
        Pass
        {
            Tags {"LightMode" = "UniversalForward"}
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            // #pragma multi_compile_instancing
            // #pragma multi_compile _ALPHATEST_ON
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // ✅ 包含光照函数和宏
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl" // ✅ 包含光照函数和宏
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            // Sampler2D(_MainTex) ;
            // SAMPLER(_MainTex_ST) ;
            // CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _AlphaClip;
            half _WaveSpeed_X;
            half _WaveSpeed_Y;
            half _WaveStrength;
            half4 _MainTex_ST;
            half4 _NoiseTex_ST;
            // CBUFFER_END
            half4 _TerrainWind;
            sampler2D _MainTex;

            sampler2D _NoiseTex;
            struct GrassData
            {
                float4 worldPos;
                float4 r;
                float4x4 worldMatrix;
            };
            StructuredBuffer<GrassData> _GrassDataBuffer;
            // AppendStructuredBuffer 用于接收剔除后的结果
            StructuredBuffer<uint> _VisibleIndexBuffer;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 vcolor : COLOR;
                float3 normal : NORMAL;
                // half2 uv : TEXCOORD0;
                // uint vertexID : SV_VertexID;
                uint instanceID : SV_INSTANCEID;
                // UNITY_VERTEX_INPUT_INSTANCE_ID 
                // 应用于不需要RenderMeshIndirect上多数简单应用，像terrain草，SV_INSTANCEID比较偏底层，使用他你没法找到terrain对应的实例ID
            };
            // UNITY_INSTANCING_BUFFER_START(Props)
            //     UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
            // UNITY_INSTANCING_BUFFER_END(Props)
            struct v2f
            {
                // half2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 positionWS: TEXCOORD3;
                float4 vcolor : TEXCOORD2;
                // UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
            };



            v2f vert (appdata v)
            {
                v2f o;
                // --- 1. 获取 Indirect 实例数据 ---
                // UNITY_SETUP_INSTANCE_ID(v);
                // UNITY_TRANSFER_INSTANCE_ID(v, o);
                uint instanceID = v.instanceID;
                uint originalIndex = _VisibleIndexBuffer[instanceID]; 
                GrassData instanceData = _GrassDataBuffer[originalIndex];
                
                float4x4 worldMatrix = instanceData.worldMatrix;
                
                // 提取该株草的世界空间中心位置 (锚点)
                // 这是为了保证整棵草采样同一个噪声值，防止模型被扯碎
                float3 worldAnchorPos = float3(worldMatrix[0][3], worldMatrix[1][3], worldMatrix[2][3]);
                
                // 基础世界坐标
                float3 worldPos = mul(worldMatrix, v.vertex).xyz;

                // --- 2. 滚动噪声风场算法 ---

                // 风向向量 (由面板定义的 X, Y 速度决定)
                float2 windDir = normalize(float2(_WaveSpeed_X, _WaveSpeed_Y));
                float totalSpeed = length(float2(_WaveSpeed_X, _WaveSpeed_Y));
                
                // 计算滚动的 UV：世界坐标 / 缩放 + 时间 * 速度
                // _NoiseTex_ST.xy 控制噪声的频率（平铺倍数）
                float2 scrollingUV = worldAnchorPos.xz * _NoiseTex_ST.xy + _Time.y * windDir * totalSpeed;

                // 使用 tex2Dlod 采样噪声图（顶点着色器必须用 lod 采样）
                // 采样两个频率不同的点进行叠加，增加随机感
                float noise1 = tex2Dlod(_NoiseTex, float4(scrollingUV, 0, 0)).r;
                float noise2 = tex2Dlod(_NoiseTex, float4(scrollingUV * 2.5 + 0.5, 0, 0)).r;
                float combinedNoise = (noise1 * 0.7 + noise2 * 0.3) * 2.0 - 1.0; // 映射到 -1 ~ 1

                // --- 3. 应用位移 ---

                // 弯曲强度计算：高度权重 (vcolor.r) * 噪声强度 * 基础摆幅
                // 假设 v.vcolor.r 底部为 0，顶部为 1
                float heightWeight = v.vcolor.r; 
                float bendStrength = combinedNoise * _WaveStrength * heightWeight;

                // 沿着风向偏移
                worldPos.xz += windDir * bendStrength;
                
                // 物理补偿：草被吹弯时，高度应该略微下降（保持草的长度不变感）
                worldPos.y -= abs(bendStrength) * 0.5;

                // --- 4. 转换坐标 ---
                o.positionWS = worldPos;
                o.vertex = TransformWorldToHClip(worldPos);

                // --- 5. 法线处理 (如果是广告牌/Billboard，通常法线直接朝上或朝相) ---
                // 这里使用原本的矩阵变换法线
                float3x3 worldMat3x3 = (float3x3)worldMatrix;
                o.normal = normalize(mul(worldMat3x3, v.normal));

                o.vcolor = v.vcolor;
                return o;
            }

            half4 frag (v2f i , bool isFace : SV_IsFrontFace) : SV_Target
            {
                // UNITY_SETUP_INSTANCE_ID(i); // necessary only if any instanced properties are going to be accessed in the fragment Shader.
                // return UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                // half4 col = tex2D(_MainTex, i.uv);
                // clip(col.a - _AlphaClip);
                half alpha = 1;
                BRDFData brdfData;
                InitializeBRDFData(_Color.rgb, 0, half3(1, 1, 1), 0, alpha, brdfData);

                half3 normal = isFace? i.normal: -i.normal;

                half4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                // half shadowAmount = MainLightRealtimeShadow(shadowCoord);
                half3 lambert = LightingLambert(light.color, light.direction, normal);
                
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 specular = DirectBRDFSpecular(brdfData, normal, light.direction, viewDir);
                half3 brdf = DirectBRDF(brdfData, normal, light.direction, viewDir) * lambert * light.shadowAttenuation ;
                // half3 brdf = (brdfData.diffuse + specular * brdfData.specular) * lambert * light.color * light.shadowAttenuation ;
                float3 GI = SampleSH(normal);
                return half4(brdf + GI * _Color, 1);
            }
            ENDHLSL
        }
        // UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
