Shader "Sport/Dissolve"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DissolveTex ("DissolveTex", 2D) = "white" {}
        _DissolveY("DissolveY", Range(-2, 2)) = 0
        
        [Space(20)]
        _DissolveRange("DissolveRange", Range(0, 2)) = 0
        [HDR]_EdgeColor("EdgeColor", Color) = (1, 1, 1, 1)
        _EdgeRange("EdgeRange", Range(0, 0.1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Tags {"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _EdgeColor;
            half _EdgeRange;

            half4 _MainTex_ST;
            half4 _DissolveTex_ST;
            half _DissolveRange;
            half _DissolveY;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);

            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 posOS : TEXCOORD3;
                float4 uv : TEXCOORD0;
            };

            float Remap(float value, float inMin, float inMax, float outMin, float outMax)
            {
                // 1. 标准化 (Normalize) 到 [0, 1] 范围
                float t = saturate((value - inMin) / (inMax - inMin));
                
                // 2. 线性插值 (Lerp) 到输出范围
                return lerp(outMin, outMax, t);
            }

            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = positionInputs.positionCS;
                o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv.xy, _DissolveTex);
                o.posOS = v.vertex;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                float dissolveTex = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, i.uv.zw).r;
                float dissolve = Remap(0, 1, 0, _DissolveRange, dissolveTex);

                float dissolveTest = _DissolveY - dissolve - i.posOS.y;

                if(dissolveTest < 0) discard;
                
                float edge = smoothstep(0, dissolveTest, _EdgeRange);

                return col + edge * _EdgeColor;
            }
            ENDHLSL
        }
    }
}
