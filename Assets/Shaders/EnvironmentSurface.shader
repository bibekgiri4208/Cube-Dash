Shader "CubeDash/Environment Surface"
{
    Properties
    {
        _BaseColor ("Base tint", Color) = (0.4, 0.5, 0.4, 1)
        _SecondaryColor ("Weathered tint", Color) = (0.3, 0.4, 0.3, 1)
        _DetailType ("0 Stone / 1 Bark / 2 Leaf / 3 Sand / 4 Wood / 5 Snow / 6 Grass", Float) = 0
        _DetailScale ("Grain scale", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
        _LandEnd ("Exit blend (enabled, end Z, width)", Vector) = (0, 42, 18, 0)
        _EndTint ("Next region ground tint", Color) = (0.5, 0.61, 0.6, 1)
        _CoastalWetness ("Tidal wet sand", Range(0, 1)) = 0
        _ShoreX ("Shoreline X", Float) = 7
        _TideDistance ("Tidal shoreline travel", Range(0, 1.5)) = 0.85
        _TidePeriod ("Tide period (seconds)", Range(15, 120)) = 40
        _BiomeBoundary ("Boundary (enabled, local Z, direction, half width)", Vector) = (0, 42, 1, 42)
        _NeighborColor ("Neighbor ground", Color) = (0.4, 0.5, 0.4, 1)
        _NeighborSecondary ("Neighbor grain tint", Color) = (0.3, 0.4, 0.3, 1)
        _NeighborSurface ("Neighbor detail, scale, smoothness", Vector) = (0, 1, 0.2, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "BeachTides.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _SecondaryColor;
            half4 _EndTint;
            float4 _LandEnd;
            float _DetailType, _DetailScale, _Smoothness, _Metallic;
            float _CoastalWetness, _ShoreX, _TideDistance, _TidePeriod;
            float4 _BiomeBoundary, _NeighborSurface;
            half4 _NeighborColor, _NeighborSecondary;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float3 metres : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.world = TransformObjectToWorld(input.positionOS.xyz);
            output.normal = TransformObjectToWorldNormal(input.normalOS);
            // Follow the recycled section; textures never swim across the baked models.
            output.metres = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz);
            // Raise submerged beds back to the common land elevation at region ends.
            float endDistance = abs(output.metres.z - _BiomeBoundary.y);
            float bedBlend = _BiomeBoundary.x * (1 - smoothstep(0, 18, endDistance));
            output.world.y = lerp(output.world.y, max(output.world.y, -9), bedBlend);
            output.positionCS = TransformWorldToHClip(output.world);
            return output;
        }
        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 cell = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        float GroundGrain(float3 p, float type)
        {
            float noise = Noise(p.xz * 1.4 + p.y * 0.27);
            if (type > 0.5 && type < 1.5) return smoothstep(-0.5, 0.65, sin((p.x + p.z) * 14 + Noise(float2(p.y * 0.6, p.x * 3)) * 4));
            if (type > 2.5 && type < 3.5) return sin(p.x * 4.2 + sin(p.z * 0.55) * 1.7) * 0.22 + noise * 0.25 + 0.42;
            if (type > 3.5 && type < 4.5) return sin((p.y + p.z * 0.24) * 15 + noise * 3) * 0.2 + noise * 0.35 + 0.4;
            if (type > 4.5 && type < 5.5) return noise * 0.25 + 0.65;
            if (type > 5.5) return Noise(p.xz * 4) * 0.35 + noise * 0.4;
            return noise;
        }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float3 normal = normalize(input.normal);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "Surface Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 samplePosition = input.metres;
                if (_BiomeBoundary.x > 0.5)
                {
                    samplePosition.z -= _BiomeBoundary.y;
                    samplePosition.y = input.world.y;
                }
                float grain = GroundGrain(samplePosition * _DetailScale, _DetailType);
                float fade = 1 - smoothstep(45, 190, distance(input.world, _WorldSpaceCameraPos));
                grain = lerp(0.5, grain, fade);
                half3 albedo = lerp(_SecondaryColor.rgb, _BaseColor.rgb, saturate(grain * 0.62 + 0.40));
                float width = max(1, _BiomeBoundary.w);
                float warp = (Noise(float2(samplePosition.x * 0.065, samplePosition.z * 0.05)) - 0.5) * 9;
                warp *= 1 - smoothstep(width * 0.7, width, abs(samplePosition.z));
                float neighborBlend = _BiomeBoundary.x * smoothstep(-width, width, (samplePosition.z + warp) * _BiomeBoundary.z);
                float neighborGrain = lerp(0.5, GroundGrain(samplePosition * _NeighborSurface.y, _NeighborSurface.x), fade);
                half3 neighborAlbedo = lerp(_NeighborSecondary.rgb, _NeighborColor.rgb, saturate(neighborGrain * 0.62 + 0.40));
                albedo = lerp(albedo, neighborAlbedo, neighborBlend);
                float edgeBlend = (1 - _BiomeBoundary.x) * _LandEnd.x * smoothstep(_LandEnd.y - max(1, _LandEnd.z), _LandEnd.y, input.metres.z);
                albedo = lerp(albedo, _EndTint.rgb, edgeBlend);
                float wet = _CoastalWetness * smoothstep(-2, 0.8, input.world.x
                    - BeachShoreline(input.world.xz, _CubeDashEnvironmentTime, _ShoreX, _TideDistance, _TidePeriod));
                wet *= (1 - edgeBlend) * (1 - _BiomeBoundary.x * (1 - smoothstep(0, 18, abs(samplePosition.z))));
                albedo *= 1 - wet * 0.34;
                InputData data = (InputData)0;
                data.positionWS = input.world;
                data.normalWS = normalize(input.normal);
                data.viewDirectionWS = normalize(_WorldSpaceCameraPos - input.world);
                data.shadowCoord = TransformWorldToShadowCoord(input.world);
                data.bakedGI = SampleSH(data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.specular = 0.04;
                surface.smoothness = lerp(lerp(_Smoothness, _NeighborSurface.z, neighborBlend), 0.68, wet);
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;
                half4 color = UniversalFragmentPBR(data, surface);
                half fog = ComputeFogFactorZ0ToFar(max(0, distance(input.world, _WorldSpaceCameraPos) - _ProjectionParams.y));
                color.rgb = MixFog(color.rgb, fog);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - output.world);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.world, normalize(output.normal), direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
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
