Shader "CubeDash/Countryside Water"
{
    Properties
    {
        _BaseColor ("Deep water", Color) = (0.065,0.25,0.25,1)
        _ShallowColor ("Shallow water", Color) = (0.28,0.46,0.36,1)
        _BedColor ("Visible silt and soil", Color) = (0.34,0.32,0.19,1)
        _ReflectionColor ("Sky reflection tint", Color) = (0.59,0.76,0.80,1)
        _FoamColor ("Subtle bank highlights", Color) = (0.75,0.84,0.72,1)
        _WaterSize ("Surface width and length (metres)", Vector) = (2.35,284,0,0)
        _Flow ("Current direction and speed", Vector) = (0,0.45,0,0)
        _RippleScale ("Ripple frequency", Range(0.1,3)) = 1
        _RippleStrength ("Ripple normals", Range(0,0.3)) = 0.12
        _ShallowWidth ("Soft bank transition", Range(0.05,2)) = 0.5
        _BedVisibility ("Shallow bed visibility", Range(0,1)) = 0.35
        _ReflectionStrength ("Reflection strength", Range(0,1)) = 0.65
        _Smoothness ("Surface smoothness", Range(0,1)) = 0.82
        _FoamStrength ("Quiet edge ripples", Range(0,0.5)) = 0.12
    }
    SubShader
    {
        // An opaque, analytically shaded shallow bed avoids a full-screen refraction copy,
        // transparent sorting against rice/reeds, and changes to the shared gameplay camera.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+20" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoadCurve.hlsl"
        #include "CountrySideWaterMotion.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor, _BedColor, _ReflectionColor, _FoamColor;
            float4 _WaterSize, _Flow;
            float _RippleScale, _RippleStrength, _ShallowWidth, _BedVisibility;
            float _ReflectionStrength, _Smoothness, _FoamStrength;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 metres : TEXCOORD1;
            float2 uv : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            // Undo route travel before bending: the pattern rides with the scenery, while
            // neighbouring pooled sections sample exactly the same phase at their shared end.
            output.metres = world.xz + float2(0, _CubeDashRoadCurve.x);
            output.positionWS = CubeDashBend(world);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = input.uv;
            return output;
        }
        float WaterNoise(float2 p)
        {
            float2 cell = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            float a = frac(sin(dot(cell, float2(127.1,311.7))) * 43758.5453);
            float b = frac(sin(dot(cell + float2(1,0), float2(127.1,311.7))) * 43758.5453);
            float c = frac(sin(dot(cell + float2(0,1), float2(127.1,311.7))) * 43758.5453);
            float d = frac(sin(dot(cell + 1, float2(127.1,311.7))) * 43758.5453);
            return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
        }
        float3 SurfaceNormal(Varyings input)
        {
            float footprint = max(length(ddx(input.metres)), length(ddy(input.metres)));
            float detail = 1 - smoothstep(0.15, 0.8, footprint * _RippleScale);
            float3 normal = CountryWaterNormal(input.metres, _CubeDashEnvironmentTime, _Flow.xy, _RippleScale, _RippleStrength * detail);
            return CubeDashBendNormal(normal, input.positionWS.z);
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 normal = SurfaceNormal(input);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _CubeDashEnvironmentTime;
                float3 normal = SurfaceNormal(input);
                float3 view = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                float2 edge = max(0, min(input.uv, 1 - input.uv)) * _WaterSize.xy;
                // Canals have continuous ends; only the lateral banks get a shallow fringe.
                float bank = _WaterSize.y > 100 ? edge.x : min(edge.x, edge.y);
                float shallow = 1 - smoothstep(0.02, _ShallowWidth, bank);
                float2 current = input.metres - _Flow.xy * t;
                float sediment = WaterNoise(input.metres * 2.7);
                float streak = WaterNoise(current * float2(4.5,0.65));
                half3 bed = _BedColor.rgb * (0.83 + sediment * 0.24);
                half3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, shallow * 0.85);
                color = lerp(color, bed, _BedVisibility * (0.35 + shallow * 0.55));
                color *= 0.94 + streak * 0.10;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light sun = GetMainLight(shadowCoord);
                color *= 0.62 + saturate(dot(normal, sun.direction)) * 0.40 * sun.shadowAttenuation;
                float3 reflectionVector = reflect(-view, normal);
                half3 probe = GlossyEnvironmentReflection(reflectionVector, input.positionWS, 1 - _Smoothness, 1);
                half3 sky = lerp(unity_FogColor.rgb, _ReflectionColor.rgb, saturate(reflectionVector.y) * 0.8);
                half3 reflection = lerp(sky, max(probe, sky * 0.35), 0.42);
                float fresnel = 0.10 + 0.80 * pow(1 - saturate(dot(normal, view)), 4);
                color = lerp(color, reflection, fresnel * _ReflectionStrength);
                float glint = pow(saturate(dot(normal, SafeNormalize(view + sun.direction))), lerp(180,620,_Smoothness));
                color += sun.color * glint * 0.20 * (0.35 + streak * 0.65) * sun.shadowAttenuation;
                float edgeRipple = (1 - smoothstep(0.025,0.16,bank)) * smoothstep(0.48,0.76,streak);
                color = lerp(color, _FoamColor.rgb, edgeRipple * _FoamStrength);
                // SV_POSITION.z is device depth in a fragment. Use world depth for fog instead.
                half fog = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
