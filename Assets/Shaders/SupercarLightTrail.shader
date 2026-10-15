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
                half ribbon = 1 - smoothstep(0.72, 1, across);
                half fade = pow(saturate(1 - input.uv.y), 0.85);
                return half4(MixFogColor(_Tint.rgb, half3(0, 0, 0), input.fog), _Tint.a * ribbon * fade);
            }
            ENDHLSL
        }
    }
}
