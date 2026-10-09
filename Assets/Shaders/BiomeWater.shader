Shader "CubeDash/Biome Water"
{
    Properties
    {
        _BaseColor ("Deep water", Color) = (0.025, 0.24, 0.34, 1)
        _ShallowColor ("Lagoon shallows", Color) = (0.12, 0.64, 0.63, 1)
        _FoamColor ("Foam", Color) = (0.86, 0.96, 0.93, 1)
        _ReflectionColor ("Sky reflection", Color) = (0.56, 0.79, 0.88, 1)
        _WaveHeight ("Wave height", Range(0, 0.5)) = 0.18
        _ShoreMode ("Coastal surf", Range(0, 1)) = 1
        _ShoreX ("Shoreline X", Float) = 7
        _ShallowWidth ("Shallows width", Float) = 22
        _CoastLimits ("Boundary bay (entrance, exit, start Z, end Z)", Vector) = (0, 0, 0, 42)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor, _FoamColor, _ReflectionColor;
            float _WaveHeight, _ShoreMode, _ShoreX, _ShallowWidth;
            float4 _CoastLimits;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float2 metres : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        float BoundaryDistance(float2 metres)
        {
            float nearRoad = 1 - smoothstep(7, 120, metres.x);
            float bank = 8 + nearRoad * 22 + sin(metres.x * 0.055) * 2.8 + sin(metres.x * 0.17) * 1.2;
            float entry = metres.y - (_CoastLimits.z + bank);
            float exit = (_CoastLimits.w - bank) - metres.y;
            return min(_CoastLimits.x > 0.5 ? entry : 10000, _CoastLimits.y > 0.5 ? exit : 10000);
        }
        float Wave(float2 p, out float2 slope)
        {
            float t = _CubeDashEnvironmentTime;
            float a = dot(p, float2(0.94, 0.34)) * 0.48 - t * 1.05;
            float b = dot(p, float2(-0.46, 0.89)) * 0.83 - t * 1.6;
            float c = dot(p, float2(0.22, -0.98)) * 1.31 + t * 0.78;
            slope = (float2(0.94, 0.34) * cos(a) * 0.48
                + float2(-0.46, 0.89) * cos(b) * 0.83 * 0.42
                + float2(0.22, -0.98) * cos(c) * 1.31 * 0.18) * _WaveHeight;
            return (sin(a) + sin(b) * 0.42 + sin(c) * 0.18) * _WaveHeight;
        }
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.world = TransformObjectToWorld(input.positionOS.xyz);
            output.metres = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz).xz;
            float2 slope;
            output.world.y += Wave(output.world.xz, slope) * smoothstep(0, 3, BoundaryDistance(output.metres));
            output.positionCS = TransformWorldToHClip(output.world);
            return output;
        }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            clip(BoundaryDistance(input.metres));
            float2 slope;
            Wave(input.world.xz, slope);
            float3 normal = normalize(float3(-slope.x, 1, -slope.y));
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        half4 DepthFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            clip(BoundaryDistance(input.metres));
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "Water Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float bayDistance = BoundaryDistance(input.metres);
                clip(bayDistance);
                float t = _CubeDashEnvironmentTime;
                float2 slope;
                float swell = Wave(input.world.xz, slope);
                float distanceToCamera = distance(_WorldSpaceCameraPos, input.world);
                float detailFade = 1 - smoothstep(25, 110, distanceToCamera);
                slope += float2(sin(input.world.z * 3.7 + input.world.x * 1.1 + t * 2.1),
                    cos(input.world.x * 4.3 - input.world.z * 0.9 - t * 1.7)) * 0.028 * detailFade;
                float3 normal = normalize(float3(-slope.x, 1, -slope.y));
                float3 view = normalize(_WorldSpaceCameraPos - input.world);
                float coast = max(0, input.world.x - _ShoreX);
                float shallow = _ShoreMode * (1 - smoothstep(0, _ShallowWidth, coast));
                shallow = max(shallow, _ShoreMode * (1 - smoothstep(0, 12, bayDistance)));
                half3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, shallow * 0.86 + 0.09);
                float caustic = pow(saturate(sin(input.world.x * 0.95 + t * 0.4)
                    * cos(input.world.z * 1.15 - t * 0.3)), 7);
                color += _ShallowColor.rgb * caustic * shallow * detailFade * 0.10;
                float fresnel = 0.025 + 0.55 * pow(1 - saturate(dot(normal, view)), 5);
                half3 reflection = lerp(unity_FogColor.rgb, _ReflectionColor.rgb, saturate(reflect(-view, normal).y) * 0.7);
                color = lerp(color, reflection, fresnel);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                color *= 0.82 + saturate(dot(normal, sun.direction)) * 0.20 * sun.shadowAttenuation;
                float glint = pow(saturate(dot(normal, normalize(view + sun.direction))), 220);
                color += sun.color * glint * 0.38 * sun.shadowAttenuation;
                float shoreLine = 0.55 + sin(input.world.z * 0.18 + t * 0.6) * 0.30 + sin(t * 0.85) * 0.48;
                float shoreFoam = 1 - smoothstep(0.16, 0.62, abs(coast - shoreLine));
                float breaker = pow(saturate(sin(coast * 0.72 - t * 1.3 + sin(input.world.z * 0.21) * 0.65)), 14);
                float foamNoise = saturate(sin(input.world.z * 2.5 + input.world.x * 3.2) * 0.35 + 0.68);
                float foam = (shoreFoam * 0.72 + breaker * (1 - smoothstep(2, 13, coast)) * 0.28)
                    * foamNoise * _ShoreMode;
                float baySurf = 1 - smoothstep(0.25, 1.5, bayDistance);
                foam = max(foam, baySurf * foamNoise * _ShoreMode * (0.65 + 0.15 * sin(t + input.metres.x * 0.2)));
                float whitecap = smoothstep(_WaveHeight * 1.32, _WaveHeight * 1.59 + 0.001, swell)
                    * detailFade * 0.18;
                color = lerp(color, _FoamColor.rgb, saturate(foam + whitecap));
                // Distance fog hides lateral edges too, unlike depth-only fog at a wide FOV.
                half fog = ComputeFogFactorZ0ToFar(max(0, distanceToCamera - _ProjectionParams.y));
                return half4(MixFog(color, fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
