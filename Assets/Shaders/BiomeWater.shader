Shader "CubeDash/Biome Water"
{
    Properties
    {
        _BaseColor ("Deep water", Color) = (0.04, 0.43, 0.55, 1)
        _ShallowColor ("Sunlit ripples", Color) = (0.24, 0.76, 0.77, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShallowColor;
            CBUFFER_END
            float _CubeDashEnvironmentTime;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float t = _CubeDashEnvironmentTime;
                float ripple = sin(input.world.x * 1.4 + input.world.z * 0.32 + t * 1.8)
                    * sin(input.world.z * 0.9 - t * 1.2);
                half3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, smoothstep(0.1, 0.95, ripple) * 0.65 + 0.12);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
