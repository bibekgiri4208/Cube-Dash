Shader "CubeDash/Supercar Light Trail"
{
    Properties { [HDR] _Tint ("Rear light red", Color) = (3, 0.015, 0.025, 0.8) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoadCurve.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(CubeDashBend(TransformObjectToWorld(input.positionOS.xyz)));
                output.uv = input.uv; output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half across = abs(input.uv.x * 2 - 1);
                half halo = 1 - smoothstep(0.05, 1, across);
                half core = 1 - smoothstep(0, 0.22, across);
                half fade = pow(saturate(1 - input.uv.y), 1.35);
                half3 light = lerp(_Tint.rgb, half3(5, 0.22, 0.12), core * 0.65);
                return half4(MixFogColor(light, half3(0, 0, 0), input.fog), _Tint.a * (halo * 0.35 + core * 0.65) * fade);
            }
            ENDHLSL
        }
    }
}
