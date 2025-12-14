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
        // Tags { "RenderType"="Opaque" }
        // LOD 100

        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // #pragma multi_compile_instancing
            // #pragma multi_compile _ALPHATEST_ON
            #pragma multi_compile_fragment _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // ✅ 包含光照函数和宏
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            // Sampler2D(_MainTex) ;
            // SAMPLER(_MainTex_ST) ;
            // CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _AlphaClip;
            float _WaveStrength;
            float _WaveSpeed_X;
            float _WaveSpeed_Y;
            float _WaveTiling;
            float4 _MainTex_ST;
            float4 _NoiseTex_ST;
            // CBUFFER_END
            float4 _TerrainWind;
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
                // float2 uv : TEXCOORD0;
                // uint vertexID : SV_VertexID;
                uint instanceID : SV_INSTANCEID;
                // UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            // UNITY_INSTANCING_BUFFER_START(Props)
            //     UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            // UNITY_INSTANCING_BUFFER_END(Props)
            struct v2f
            {
                // float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 positionWS: TEXCOORD3;
                float4 vcolor : TEXCOORD2;
                // UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
            };



            v2f vert (appdata v)
            {
                v2f o;
                // 必须在函数开头调用，用于设置和初始化实例 ID
                // UNITY_SETUP_INSTANCE_ID(v);

                // 2. ✅ 获取当前渲染实例的 ID (0到N-1，N是可见实例数)
                // 使用 URP 最可靠的内置变量/宏：
                // uint instanceID = unity_InstanceID; 
                uint instanceID = v.instanceID;
                // 3. ✅ 通过 instanceID 查找原始 GrassData 的索引
                // instanceID 是 RenderMeshIndirect 调用的第 k 个实例
                uint originalIndex = _VisibleIndexBuffer[instanceID]; 

                // 4. ✅ 通过原始索引获取实例数据
                GrassData instanceData = _GrassDataBuffer[originalIndex];
                
                // 5. 获取实例的世界矩阵和位置
                float4x4 worldMatrix = instanceData.worldMatrix;
                float4 originalLocalPos = v.vertex;

                // --- 【风场计算开始】 ---

                // **使用世界矩阵计算当前顶点在世界空间的位置（未位移前）**
                float3 worldPos = mul(worldMatrix, originalLocalPos).xyz;

                // 替换您原来的错误行
                
                // --- 【风场计算，保持不变】 ---
                float2 worldUV = worldPos.xz; 
                float2 noiseUV = TRANSFORM_TEX(worldUV, _NoiseTex); 
                noiseUV *= _WaveTiling;
                noiseUV.x += _Time.y * _WaveSpeed_X; 
                noiseUV.y += _Time.y * _WaveSpeed_Y; 
                float noiseValue = tex2Dlod(_NoiseTex, float4(noiseUV, 0, 0)).r;
                float displacement = noiseValue * _WaveStrength;
                
                // 2. 构造世界空间【纯位移向量】
                // V_displacement = (风向) * (强度) * (顶点权重)
                // 📢 使用 TransformObjectToWorldDir 宏获取本地风向的世界向量，确保 Instancing 友好
                float3 windDirectionWS = float3(-_WaveSpeed_X, 0, -_WaveSpeed_Y); // 暂时使用固定世界方向
                
                float heightWeight = v.vcolor.r; // 假设 v.vcolor.r 是权重 (0=底部, 1=顶部)

                // 弯曲/位移的幅度
                float bendMagnitude = displacement * heightWeight;

                // 构造最终的世界空间位移向量
                // 注意：将 windDirectionWS 标准化，以防它的长度不是1
                float3 finalDisplacementVectorWS = normalize(windDirectionWS) * bendMagnitude;
                
                // 3. 将位移应用到世界坐标
                float3 finalWorldPos = worldPos;
                o.positionWS = worldPos;
                finalWorldPos.xyz += finalDisplacementVectorWS; 
                
                // 4. 投影到裁剪空间 (Instancing 友好的最终步骤)
                o.vertex = TransformWorldToHClip(finalWorldPos.xyz);
                
                // ... 传递颜色和 UV ...
                o.vcolor = v.vcolor;
                // o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.normal = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // UNITY_SETUP_INSTANCE_ID(i); // necessary only if any instanced properties are going to be accessed in the fragment Shader.
                // return UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                // float4 col = tex2D(_MainTex, i.uv);
                // clip(col.a - _AlphaClip);
                Light light = GetMainLight();
                real cos = dot(i.normal, light.direction);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                float shadowAmount = MainLightRealtimeShadow(shadowCoord);
                return i.vcolor.r * _Color * shadowAmount * cos;
            }
            ENDHLSL
        }
        // UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
