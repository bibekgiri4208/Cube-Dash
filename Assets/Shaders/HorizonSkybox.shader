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
        _CloudCoverage ("Cloud coverage", Range(0, 1)) = 0.48
        _CloudScale ("Cloud scale", Range(0.4, 4)) = 0.9
        _CloudOpacity ("Cloud opacity", Range(0, 1)) = 0.78
        _CloudSpeed ("Cloud drift", Range(0, 0.1)) = 0.012
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
                float _CloudCoverage, _CloudScale, _CloudOpacity, _CloudSpeed;
            CBUFFER_END
            float _CubeDashEnvironmentTime;
            float CloudHash(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }
            float CloudVolume(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float lower = lerp(lerp(CloudHash(cell), CloudHash(cell + float3(1, 0, 0)), f.x),
                    lerp(CloudHash(cell + float3(0, 1, 0)), CloudHash(cell + float3(1, 1, 0)), f.x), f.y);
                float upper = lerp(lerp(CloudHash(cell + float3(0, 0, 1)), CloudHash(cell + float3(1, 0, 1)), f.x),
                    lerp(CloudHash(cell + float3(0, 1, 1)), CloudHash(cell + 1), f.x), f.y);
                return lerp(lower, upper, f.z);
            }
            float CloudLayers(float3 p)
            {
                return CloudVolume(p) * 0.5 + CloudVolume(p * 2.03 + 13.7) * 0.28
                    + CloudVolume(p * 4.11 + 27.2) * 0.14 + CloudVolume(p * 8.23 + 41.9) * 0.08;
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
                // Layered cumulus and higher wisps, faded into the horizon to match biome fog.
                // Direction-space noise avoids projection seams and stretched cloud bands near the horizon.
                float3 cloudPosition = direction * _CloudScale * 6;
                cloudPosition += float3(_CubeDashEnvironmentTime * _CloudSpeed, 0, 0);
                float cloud = CloudLayers(cloudPosition);
                float threshold = lerp(0.78, 0.3, _CloudCoverage);
                float altitude = smoothstep(0.06, 0.28, direction.y);
                float mask = smoothstep(threshold, threshold + 0.14, cloud) * altitude * _CloudOpacity;
                float depth = smoothstep(threshold + 0.04, threshold + 0.28, cloud);
                float litEdge = saturate(CloudVolume(cloudPosition + normalize(_SunDirection.xyz) * 0.3) - cloud + 0.5);
                half3 shadow = lerp(_HorizonColor.rgb, _ZenithColor.rgb, 0.35) * 0.8;
                half3 cloudColor = lerp(_CloudColor.rgb, shadow, depth * 0.48);
                cloudColor += _SunColor.rgb * pow(saturate(sunAngle), 12) * litEdge * (1 - depth) * 0.08;
                float wisps = smoothstep(0.68, 0.86,
                    CloudVolume(cloudPosition * float3(1, 2.7, 1) + float3(17, 31, 9)));
                sky = lerp(sky, _CloudColor.rgb, wisps * altitude * _CloudOpacity * 0.2);
                sky = lerp(sky, cloudColor, mask);
                float disc = smoothstep(cos(radius), cos(radius * 0.8), sunAngle);
                float halo = pow(saturate(sunAngle), 48) * 0.1 + pow(saturate(sunAngle), 8) * 0.025;
                sky += _SunColor.rgb * (disc * (1 - mask * 0.85) + halo);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
