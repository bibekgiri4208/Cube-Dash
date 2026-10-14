Shader "CubeDash/Japanese City Clear Sky"
{
    Properties
    {
        _ZenithColor ("Blue sky", Color) = (0.21, 0.51, 0.91, 1)
        _HorizonColor ("Horizon", Color) = (0.66, 0.82, 0.96, 1)
        _GroundColor ("Below horizon", Color) = (0.63, 0.7, 0.76, 1)
        _SunColor ("Sun", Color) = (1, 0.94, 0.86, 1)
        _SunDirection ("Sun direction", Vector) = (-0.4, 0.7, 0.3, 0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor;
            float4 _SunDirection;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.direction = i.positionOS.xyz; return o; }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(d.y), 0.35));
                if (d.y < 0) color = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-d.y * 4));
                // No camera-centered landmark or cloud artwork: passing world geometry supplies motion cues.
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
