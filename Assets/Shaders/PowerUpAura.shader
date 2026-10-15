Shader "CubeDash/Power Up Aura"
{
    Properties
    {
        [HDR] _Tint ("Aura tint", Color) = (0.22, 0.85, 1, 0.65)
        _Phase ("Animation phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoadCurve.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Phase;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Bend the center with the road, then face the camera from every viewing angle.
                float3 center = CubeDashBend(TransformObjectToWorld(float3(0, 0, 0)));
                float2 scale = float2(length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
                    length(TransformObjectToWorldDir(float3(0, 1, 0), false)));
                float3 world = center + mul((float3x3)UNITY_MATRIX_I_V, float3(input.positionOS.xy * scale, 0));
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv * 2 - 1;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv);
                float edge = 1 - smoothstep(0.74, 1, radius);
                float mist = exp2(-radius * radius * 4.5) * edge;
                float ring = exp2(-pow((radius - 0.64) * 23, 2));
                float angle = atan2(input.uv.y, input.uv.x);
                float wisps = pow(saturate(0.5 + 0.5 * sin(angle * 3 - _Phase * 1.6 + radius * 7)), 5);
                float pulse = 0.92 + 0.08 * sin(_Phase * 2.4);
                float alpha = (mist * 0.48 + ring * (0.16 + wisps * 0.3)) * _Tint.a * pulse;
                // Additive transparent glow remains visible without requiring bloom on every map.
                return half4(MixFogColor(_Tint.rgb * 1.8, half3(0, 0, 0), input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
