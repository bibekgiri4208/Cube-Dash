Shader "CubeDash/Horizon Skybox"
{
    Properties
    {
        _ZenithColor ("Upper sky", Color) = (0.10, 0.17, 0.36, 1)
        _HorizonColor ("Horizon", Color) = (0.75, 0.51, 0.67, 1)
        _GroundColor ("Below horizon", Color) = (0.20, 0.21, 0.36, 1)
        _GradientPower ("Gradient softness", Range(0.1, 3)) = 0.55
        [HDR] _SunColor ("Sun glow", Color) = (1.5, 0.8, 0.5, 1)
        _SunDirection ("Sun direction", Vector) = (0.15, 0.07, 1, 0)
        _SunSize ("Sun radius (degrees)", Range(0.1, 8)) = 1.8
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
                float _GradientPower, _SunSize;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float above = pow(saturate(direction.y), _GradientPower);
                float below = pow(saturate(-direction.y * 3), 0.6);
                half3 sky = direction.y >= 0 ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, above)
                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, below);
                float sunAngle = dot(direction, normalize(_SunDirection.xyz));
                float radius = radians(_SunSize);
                float disc = smoothstep(cos(radius), cos(radius * 0.82), sunAngle);
                float halo = pow(saturate(sunAngle), 96) * 0.12;
                sky += _SunColor.rgb * (disc + halo);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
