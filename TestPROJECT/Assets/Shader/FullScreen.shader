Shader "Custom/Volume"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)

    }
    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

        ENDHLSL
        Pass
        {
            Name "Eyes"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct appdata
            {
                float4 vertex : POSITION;
                uint vertexID : SV_vertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv: TEXCOORD;
            };

            v2f vert (appdata i)
            {
                v2f o;
                // o.vertex = TransformObjectToHClip(i.vertex.xyz);
                // GetQuadVertexPosition 
                o.vertex = GetFullScreenTriangleVertexPosition(i.vertexID);
                // GetQuadTexCoord
                o.uv = GetFullScreenTriangleTexCoord(i.vertexID);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }


    }
}
