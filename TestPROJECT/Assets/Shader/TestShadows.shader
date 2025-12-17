Shader "Custom/TestShadows"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            // #pragma multi_compile _ALPHATEST_ON
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // ✅ 包含光照函数和宏
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            float4 _windspeed;
            half4 _Color;
            half4 _MainTex_ST;
            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 vcolor : COLOR;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            // UNITY_INSTANCING_BUFFER_START(Props)
            //     UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
            // UNITY_INSTANCING_BUFFER_END(Props)
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 positionWS: TEXCOORD3;
                float4 vcolor : TEXCOORD2;
            };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);

                float2 windDir = _windspeed.xy; // 获取风的方向向量
                float speed = _windspeed.w;
                float bending = _windspeed.z;

                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                float wave = sin(_Time.y * speed + dot(worldPos.xz, windDir)); 
                worldPos.xz += windDir * wave * bending * v.vertex.y;
                // float wave = sin(_Time.y * 10.0 + worldPos.x); 
                // worldPos.xz += float2(1, 0) * wave * 0.5 * v.vertex.y;

                o.positionWS = worldPos;
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.vcolor = v.vcolor;
                o.vertex = TransformWorldToHClip(worldPos.xyz);
                return o;
            }
            half4 frag (v2f i , bool isFace : SV_IsFrontFace) : SV_Target
            {
                half alpha = 1;
                BRDFData brdfData;
                InitializeBRDFData(_Color.rgb, 0, half3(1, 1, 1), 1, alpha, brdfData);

                half3 normal = isFace? i.normal: -i.normal;
                half4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half shadowAmount = MainLightRealtimeShadow(shadowCoord);
                half3 lambert = LightingLambert(light.color, light.direction, normal);
                
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 specular = DirectBRDFSpecular(brdfData, normal, light.direction, viewDir);
                half3 brdf = DirectBRDF(brdfData, normal, light.direction, viewDir) * lambert * light.shadowAttenuation ;
                // half3 brdf = (brdfData.diffuse + specular * brdfData.specular) * lambert * light.color * light.shadowAttenuation ;

                return half4(brdf, 1);
            }
            ENDHLSL
        }


    }
    FallBack "Diffuse"
}
