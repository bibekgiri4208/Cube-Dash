Shader "CubeDash/Jungle Vegetation"
{
    Properties
    {
        _BaseMap ("Idyllic texture", 2D) = "white" {}
        _BaseColor ("Jungle foliage", Color) = (0.12, 0.38, 0.22, 1)
        _TopColor ("Soft leaf tips", Color) = (0.16, 0.44, 0.25, 1)
        _Cutoff ("Leaf cutout", Range(0, 1)) = 0.4
        _WindStrength ("Gentle wind", Range(0, 0.3)) = 0.08
        _Smoothness ("Smoothness", Range(0, 1)) = 0.08
        _SceneryRise ("Scenery rise", Vector) = (0, 0, 80, 1.25)
        _SceneryStagger ("Scenery stagger", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoadCurve.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _TopColor;
            float4 _SceneryRise;
            float _Cutoff, _WindStrength, _Smoothness, _SceneryStagger;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 wind : TEXCOORD1;
            float2 anchor : TEXCOORD2;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half2 tint : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 position = input.positionOS.xyz;
            float phase = input.wind.y + _CubeDashEnvironmentTime * 1.15;
            // Each plant has its own phase and pinned feet, including after section recycling.
            float sway = (sin(phase) + sin(phase * 1.73) * 0.35) * _WindStrength * input.wind.x;
            position.x += sway;
            position.z += sway * 0.38;
            output.world = TransformObjectToWorld(position);
            output.normal = TransformObjectToWorldNormal(input.normalOS);
            float delay = (saturate(input.anchor.y / 42) * 0.75
                + frac(abs(input.anchor.x) * 0.137) * 0.25) * _SceneryStagger;
            float reveal = smoothstep(0, 1, saturate((_CubeDashEnvironmentTime - _SceneryRise.y - delay) / max(0.1, _SceneryRise.w)));
            output.world.y -= _SceneryRise.x * _SceneryRise.z * (1 - reveal);
            CubeDashBendVertex(output.world, output.normal);
            output.positionCS = TransformWorldToHClip(output.world);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.tint = input.color.rg;
            return output;
        }
        half4 LeafSample(Varyings input)
        {
            half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            clip(sampled.a - _Cutoff);
            return sampled;
        }
        half4 DepthFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            LeafSample(input);
            return 0;
        }
        half4 NormalFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            LeafSample(input);
            float3 normal = normalize(input.normal) * IS_FRONT_VFACE(face, 1, -1);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "Jungle Forward"
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
            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 sampled = LeafSample(input);
                // Keep the pack's leaf detail, not its yellow-green default palette.
                half grain = dot(sampled.rgb, half3(0.299, 0.587, 0.114));
                half3 tint = lerp(_BaseColor.rgb, _TopColor.rgb, input.tint.x);
                InputData data = (InputData)0;
                data.positionWS = input.world;
                data.normalWS = normalize(input.normal) * IS_FRONT_VFACE(face, 1, -1);
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                data.shadowCoord = TransformWorldToShadowCoord(input.world);
                data.bakedGI = SampleSH(data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = tint * (0.72 + grain * 0.55) * lerp(0.92, 1.06, input.tint.y);
                surface.specular = 0.04;
                surface.smoothness = _Smoothness;
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
