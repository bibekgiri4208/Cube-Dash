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
        _CloudColor ("Cloud tint", Color) = (0.94, 0.97, 1, 1)
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
                half4 _CloudColor;
                float4 _SunDirection;
                float _GradientPower, _SunSize;
            CBUFFER_END
            float _CubeDashEnvironmentTime;
            float CloudNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(cell + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(cell + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(cell + 1, float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
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
                float2 cloudUV = direction.xz / max(0.15, direction.y) * 2.5;
                cloudUV += float2(_CubeDashEnvironmentTime * 0.025, 0);
                float cloud = CloudNoise(cloudUV) * 0.7 + CloudNoise(cloudUV * 2.4) * 0.3;
                float mask = smoothstep(0.58, 0.78, cloud) * smoothstep(0.02, 0.2, direction.y) * 0.65;
                sky = lerp(sky, _CloudColor.rgb, mask);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
