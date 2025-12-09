Shader "Volume/Flicker"
{
    Properties
    {
        // _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Range ("Range", Range(0, 1)) = 0
        _Center ("Range", Range(0, 0.5)) = 0
        _Speed ("Speed", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Transparents" }
        LOD 100

        Pass
        {
            Name "Flicker"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            real4 _Color;
            real _Range;
            real _Center;
            real _Speed;

            struct appdata
            {
                real4 vertex : POSITION;
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                real4 vertex : SV_POSITION;
                real2 uv : TEXCOORD0;
            };

            // sampler2D _MainTex;
            // float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                
                // o.vertex = TransformObjectToHClip(v.vertex.xyz);
                // o.uv = v.uv;
                // o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(v.vertexID);

                return o ;
            }

            real4 frag (v2f i) : SV_Target
            {
                // 用来修正长宽拉伸
                // real aspectRatio = _ScaledScreenParams.x / _ScaledScreenParams.y;   
                real2 uv = i.uv - 0.5;
                // uv.x *= aspectRatio;
                real l = length(uv);
                real r = smoothstep(_Center, _Range, l) * (sin(_Time.w * 8 * _Speed) + 1) * 0.5 ;
                return _Color * r;
            }
            ENDHLSL
        }
    }
}
