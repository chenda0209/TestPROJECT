Shader "Custom/Grass"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex ("MainTex", 2D) = "white" {}
        _NoiseTex ("NoiseTex", 2D) = "white" {}
        _AlphaClip("_AlphaClip", Range(0, 1)) = 0.1
        _WaveStrength("_WaveStrength", Range(0, 10)) = 0.1
        _WaveSpeed_X("_WaveSpeed_X", Range(-1, 1)) = 0.1
        _WaveSpeed_Y("_WaveSpeed_Y", Range(-1, 1)) = 0.1
        _Smoothness("Smoothness", Range(0, 1)) = 0.1
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
            half _WaveTiling;
            half _Smoothness;
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
                float4x4 translateMatrix;
                float4x4 rotateMatrix;
            };
            StructuredBuffer<GrassData> _GrassDataBuffer;
            // AppendStructuredBuffer 用于接收剔除后的结果
            StructuredBuffer<uint> _Lod0Buffer;

            struct appdata
            {
                half2 uv : TEXCOORD0;
                float4 vertex : POSITION;

                float3 normal : NORMAL;
                float4 vcolor : COLOR;
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
                half2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 positionWS: TEXCOORD3;
                float4 vcolor : TEXCOORD2;
                // UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
            };


            // v2f vert (appdata v)
            // {
            //     v2f o;
            //     // --- 1. 获取 Indirect 实例数据 ---
            //     // UNITY_SETUP_INSTANCE_ID(v);
            //     // UNITY_TRANSFER_INSTANCE_ID(v, o);
            //     uint instanceID = v.instanceID;
            //     uint originalIndex = _VisibleIndexBuffer[instanceID]; 
            //     GrassData instanceData = _GrassDataBuffer[originalIndex];
            //     float4x4 worldMatrix = instanceData.worldMatrix;
                
            //     // 提取该株草的世界空间中心位置 (锚点)
            //     // 这是为了保证整棵草采样同一个噪声值，防止模型被扯碎
            //     float3 worldAnchorPos = float3(worldMatrix[0][3], worldMatrix[1][3], worldMatrix[2][3]);
                
            //     // 基础世界坐标
            //     float3 worldPos = mul(worldMatrix, v.vertex).xyz;

            //     // --- 2. 滚动噪声风场算法 ---

            //     // 风向向量 (由面板定义的 X, Y 速度决定)
            //     float2 windDir = normalize(float2(_WaveSpeed_X, _WaveSpeed_Y));
            //     float totalSpeed = length(float2(_WaveSpeed_X, _WaveSpeed_Y));
                
            //     // 计算滚动的 UV：世界坐标 / 缩放 + 时间 * 速度
            //     // _NoiseTex_ST.xy 控制噪声的频率（平铺倍数）
            //     float2 scrollingUV = worldAnchorPos.xz * _NoiseTex_ST.xy + _Time.y * windDir * totalSpeed;

            //     // 使用 tex2Dlod 采样噪声图（顶点着色器必须用 lod 采样）
            //     // 采样两个频率不同的点进行叠加，增加随机感
            //     float noise1 = tex2Dlod(_NoiseTex, float4(scrollingUV, 0, 0)).r;
            //     float noise2 = tex2Dlod(_NoiseTex, float4(scrollingUV * 2.5 + 0.5, 0, 0)).r;
            //     float combinedNoise = (noise1 * 0.7 + noise2 * 0.3) * 2.0 - 1.0; // 映射到 -1 ~ 1

            //     // --- 3. 应用位移 ---

            //     // 弯曲强度计算：高度权重 (vcolor.r) * 噪声强度 * 基础摆幅
            //     // 假设 v.vcolor.r 底部为 0，顶部为 1
            //     float heightWeight = v.vcolor.r; 
            //     float bendStrength = combinedNoise * _WaveStrength * heightWeight;

            //     // 沿着风向偏移
            //     worldPos.xz += windDir * bendStrength;
                
            //     // 物理补偿：草被吹弯时，高度应该略微下降（保持草的长度不变感）
            //     worldPos.y -= abs(bendStrength) * 0.5;

            //     // --- 4. 转换坐标 ---
            //     o.positionWS = worldPos;
            //     o.vertex = TransformWorldToHClip(worldPos);

            //     // --- 5. 法线处理 (如果是广告牌/Billboard，通常法线直接朝上或朝相) ---
            //     // 这里使用原本的矩阵变换法线
            //     float3x3 worldMat3x3 = (float3x3)worldMatrix;
            //     o.normal = normalize(mul(worldMat3x3, v.normal));

            //     o.vcolor = v.vcolor;
            //     return o;
            // }


            // v2f vert (appdata v)
            // {
            //     v2f o;
            //     uint instanceID = v.instanceID;
            //     uint originalIndex = _VisibleIndexBuffer[instanceID]; 
            //     GrassData instanceData = _GrassDataBuffer[originalIndex];
            //     float4x4 worldMatrix = instanceData.worldMatrix;

            //     // 基础世界坐标
            //     float3 worldPos = mul(worldMatrix, v.vertex).xyz;
            //     o.positionWS = worldPos;
            //     o.vertex = TransformWorldToHClip(worldPos);

            //     float3x3 worldMat3x3 = (float3x3)worldMatrix;
            //     o.normal = normalize(mul(worldMat3x3, v.normal));
            //     o.vcolor = v.vcolor;
            //     return o;
            // }


            // v2f vert (appdata v)
            // {
            //     v2f o;
            //     // --- 1. 获取 Indirect 实例数据 ---
            //     uint instanceID = v.instanceID;
            //     uint originalIndex = _VisibleIndexBuffer[instanceID]; 
            //     GrassData instanceData = _GrassDataBuffer[originalIndex];
            //     float4x4 worldMatrix = instanceData.worldMatrix;
                
            //     // 获取该株草的世界空间中心位置
            //     float3 worldAnchorPos = float3(worldMatrix[0][3], worldMatrix[1][3], worldMatrix[2][3]);
                
            //     // 基础世界坐标
            //     float3 worldPos = mul(worldMatrix, v.vertex).xyz;

            //     // --- 2. 滚动噪声风场算法 ---
            //     float2 windDir = normalize(float2(_WaveSpeed_X, _WaveSpeed_Y));
            //     float totalSpeed = length(float2(_WaveSpeed_X, _WaveSpeed_Y));
                
            //     float2 scrollingUV = worldAnchorPos.xz * _NoiseTex_ST.xy + _Time.y * windDir * totalSpeed;

            //     float noise1 = tex2Dlod(_NoiseTex, float4(scrollingUV, 0, 0)).r;
            //     float noise2 = tex2Dlod(_NoiseTex, float4(scrollingUV * 2.5 + 0.5, 0, 0)).r;
            //     float combinedNoise = (noise1 * 0.6 + noise2 * 0.4) * 2.0 - 1.0; 

            //     // --- 3. 应用位移 ---
            //     float heightWeight = v.vcolor.r; 
            //     float bendStrength = combinedNoise * _WaveStrength * heightWeight;

            //     worldPos.xz += windDir * bendStrength;
            //     worldPos.y -= abs(bendStrength) * 0.5;

                
            //     // --- 4. 转换坐标 ---
            //     o.positionWS = worldPos;
            //     o.vertex = TransformWorldToHClip(worldPos);

            //     // --- 5. 修改法线 (这里是关键) ---
            //     // 不再使用 mul(worldMat3x3, v.normal)
            //     // 直接给予一个绝对向上的世界空间法线
            //     // 我们使用 TransformObjectToWorldNormal(float3(0, 1, 0)) 或者直接 float3(0, 1, 0)
            //     o.normal = float3(0, 1, 0);
            //     o.vcolor = v.vcolor;
            //     return o;
            // }

            // float3 GetDisplacedWorldPos(float3 localPos, float4x4 worldMatrix, float3 worldAnchorPos)
            // {
            //     // 1. 初始世界坐标
            //     float3 wPos = mul(worldMatrix, float4(localPos, 1.0)).xyz;

            //     // 2. 你的风场逻辑
            //     float2 windDir = normalize(float2(_WaveSpeed_X, _WaveSpeed_Y));
            //     float totalSpeed = length(float2(_WaveSpeed_X, _WaveSpeed_Y));
            //     float2 scrollingUV = worldAnchorPos.xz * _NoiseTex_ST.xy + _Time.y * windDir * totalSpeed;

            //     // 顶点着色器采样噪声
            //     float noise1 = tex2Dlod(_NoiseTex, float4(scrollingUV, 0, 0)).r;
            //     float noise2 = tex2Dlod(_NoiseTex, float4(scrollingUV * 2.5 + 0.5, 0, 0)).r;
            //     float combinedNoise = (noise1 * 0.7 + noise2 * 0.3) * 2.0 - 1.0;

            //     // 3. 应用位移 (假设我们需要拿到顶点的高度权重，这里可以通过某种方式传入或者根据 localPos.y)
            //     // 注意：在函数内部我们拿不到 v.vcolor，所以我们假设用 localPos.y 或通过参数传进来
            //     // 这里为了演示，假设草的原始向上方向是 Y
            //     float hWeight = saturate(localPos.y); 
            //     float bendStrength = combinedNoise * _WaveStrength * hWeight;

            //     wPos.xz += windDir * bendStrength;
            //     wPos.y -= abs(bendStrength) * 0.5; // 物理高度补偿

            //     return wPos;
            // }

            // v2f vert (appdata v)
            // {
            //     v2f o;
            //     uint instanceID = v.instanceID;
            //     uint originalIndex = _VisibleIndexBuffer[instanceID]; 
            //     GrassData instanceData = _GrassDataBuffer[originalIndex];
            //     float4x4 worldMatrix = instanceData.worldMatrix;
            //     float3 worldAnchorPos = float3(worldMatrix[0][3], worldMatrix[1][3], worldMatrix[2][3]);

            //     // --- 核心步骤：三点采样法 ---
                
            //     // 1. 计算当前顶点的最终世界位置
            //     float3 p0 = GetDisplacedWorldPos(v.vertex.xyz, worldMatrix, worldAnchorPos);

            //     // 2. 在局部空间取两个微小的偏移点 (沿着切线和副切线方向)
            //     // 这里的 0.01 是偏移步长，越小越精确
            //     float3 p1 = GetDisplacedWorldPos(v.vertex.xyz + float3(0.01, 0, 0), worldMatrix, worldAnchorPos);
            //     float3 p2 = GetDisplacedWorldPos(v.vertex.xyz + float3(0, 0, 0.01), worldMatrix, worldAnchorPos);

            //     // 3. 计算形变后的新切线向量
            //     float3 tangentWS = normalize(p1 - p0);
            //     float3 bitangentWS = normalize(p2 - p0);

            //     // 4. 叉积得到最终的实时世界空间法线
            //     // 注意：根据你的模型坐标系（左手/右手），可能需要交换 tangent 和 bitangent 的顺序
            //     o.normal = normalize(cross(bitangentWS, tangentWS));

            //     // --- 赋值输出 ---
            //     o.positionWS = p0;
            //     o.vertex = TransformWorldToHClip(p0);
            //     o.vcolor = v.vcolor;
                
            //     return o;
            // }


            v2f vert (appdata v)
            {
                v2f o;
                // --- 1. 获取 Indirect 实例数据 ---
                uint instanceID = v.instanceID;
                uint originalIndex = _Lod0Buffer[instanceID]; 
                GrassData instanceData = _GrassDataBuffer[originalIndex];
                float4x4 worldMatrix = instanceData.worldMatrix;
                float4x4 translateMatrix = instanceData.translateMatrix;
                float4x4 rotateMatrix = instanceData.rotateMatrix;
                float3 worldPos = mul(worldMatrix, v.vertex).xyz;
                
                // 基础世界坐标
                
                // 1. 将强度转换为弧度 (假设 1.57 为 90度)
                float2 wave = float2(_WaveSpeed_X, _WaveSpeed_Y);
                float noise = tex2Dlod(_NoiseTex, float4(worldPos.xz * _WaveTiling + wave * _Time.y , 0, 0)).r;

                float angleX = noise * v.vertex.y * _WaveSpeed_X * 1.5708 * _WaveStrength;
                float angleZ = noise * v.vertex.y * _WaveSpeed_Y * 1.5708 * _WaveStrength;

                float sx, cx;
                sincos(angleX, sx, cx);

                float sz, cz;
                sincos(angleZ, sz, cz);

                // 2. 绕 X 轴旋转矩阵 (控制前后倒)
                float3x3 rotX = float3x3(
                    1,  0,   0,
                    0,  cx, -sx,
                    0,  sx,  cx
                );

                // 3. 绕 Z 轴旋转矩阵 (控制左右倒)
                float3x3 rotZ = float3x3(
                    cz, -sz, 0,
                    sz,  cz, 0,
                    0,   0,  1
                );
                

                float3x3 worldMat3x3 = (float3x3)worldMatrix;
                // 4. 混合应用：先绕 X 旋转，再绕 Z 旋转
                // 注意：mul(A, B) 在 HLSL 中效果等同于矩阵级联
                float3x3 combinedRot = mul(rotZ, rotX); 
                float3 finalPos = mul(combinedRot, mul(rotateMatrix, v.vertex.xyz));
                float3 positionWS = mul(worldMatrix, float4(finalPos,1)).xyz;
                // --- 4. 转换坐标 ---
                o.positionWS = positionWS;
                o.vertex = TransformWorldToHClip(positionWS);

                // --- 5. 修改法线 (这里是关键) ---
                // 不再使用 mul(worldMat3x3, v.normal)
                // 直接给予一个绝对向上的世界空间法线
                // 我们使用 TransformObjectToWorldNormal(float3(0, 1, 0)) 或者直接 float3(0, 1, 0)
                
                float3 normal = normalize(mul(rotateMatrix, v.normal));
                o.normal = normalize(mul(combinedRot, normal));

                o.vcolor = v.vcolor;
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i , bool isFace : SV_IsFrontFace) : SV_Target
            {
                // UNITY_SETUP_INSTANCE_ID(i); // necessary only if any instanced properties are going to be accessed in the fragment Shader.
                // return UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _AlphaClip);
                half alpha = 1;
                BRDFData brdfData;
                InitializeBRDFData(col * _Color.rgb, 0, half3(1, 1, 1), _Smoothness, alpha, brdfData);

                half3 normal = isFace? normalize(i.normal): normalize(-i.normal);

                half4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                // half shadowAmount = MainLightRealtimeShadow(shadowCoord);
                // half3 lambert = LightingLambert(light.color, light.direction, normal);
                
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 specular = DirectBRDFSpecular(brdfData, normal, light.direction, viewDir);
                // half3 brdf = DirectBRDF(brdfData, normal, light.direction, viewDir) * lambert * light.shadowAttenuation ;//没有使用兰伯特，背光效果太差了
                half3 brdf = (brdfData.diffuse + specular * brdfData.specular) * light.color * light.shadowAttenuation ;
                float3 GI = SampleSH(normal);

                return half4(brdf * i.positionWS.y + GI * col * _Color, 1);
            }
            ENDHLSL
        }
        // UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
