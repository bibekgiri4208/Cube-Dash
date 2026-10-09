Shader "CubeDash/Jet Particle"
{
    Properties { [HDR] _Tint ("Exhaust tint", Color) = (0.5, 0.55, 0.6, 0.35) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2 - 1;
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                float softness = pow(saturate(1 - radius), 2);
                float billow = 0.86 + 0.14 * sin(input.uv.x * 8 + sin(input.uv.y * 7));
                return half4(MixFog(_Tint.rgb * input.color.rgb, input.fog),
                    _Tint.a * input.color.a * softness * billow);
            }
            ENDHLSL
        }
    }
}
