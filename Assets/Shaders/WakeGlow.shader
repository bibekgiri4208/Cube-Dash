Shader "CubeDash/Wake Glow"
{
    Properties { [HDR] _Tint ("Trail glow", Color) = (1.4, 0.35, 0.25, 0.6) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.positionOS.xz + 0.5;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half width = abs(input.uv.x * 2 - 1);
                half softEdge = 1 - smoothstep(0.25, 1, width);
                half core = 1 - smoothstep(0, 0.38, width);
                return half4(MixFog(_Tint.rgb * (1 + core * 1.4), input.fog), _Tint.a * softEdge);
            }
            ENDHLSL
        }
    }
}
