Shader "CubeDash/Japanese City Sakura"
{
    Properties
    {
        _BaseMap ("Original blossom cutout", 2D) = "white" {}
        _BaseColor ("Lower blush petals", Color) = (0.965, 0.604, 0.694, 1)
        _TopColor ("Upper pale petals", Color) = (1, 0.816, 0.855, 1)
        _Cutoff ("Petal cutout", Range(0, 1)) = 0.35
        _Smoothness ("Matte petals", Range(0, 1)) = 0.08
        _WindStrength ("Gentle petal sway", Range(0, 0.3)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _TopColor;
            float _Cutoff, _Smoothness, _WindStrength;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float height : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 position = input.positionOS.xyz;
            o.height = saturate(position.y / 12);
            float phase = _CubeDashEnvironmentTime * 1.1 + dot(position.xz, float2(0.18, 0.13));
            float sway = (sin(phase) + sin(phase * 1.73) * 0.3) * _WindStrength * o.height * o.height;
            position.xz += float2(sway, sway * 0.38);
            // CitySakuraTrees already bends/reveals the native prefab roots. Never bend twice.
            o.world = TransformObjectToWorld(position);
            o.normal = TransformObjectToWorldNormal(input.normalOS);
            o.positionCS = TransformWorldToHClip(o.world);
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap); return o;
        }
        half4 Petal(Varyings input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            clip(texel.a - _Cutoff); return texel;
        }
        half4 DepthFrag(Varyings input) : SV_Target { Petal(input); return 0; }
        half4 NormalFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            Petal(input);
            float3 n = normalize(input.normal) * IS_FRONT_VFACE(face, 1, -1);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "City Sakura Forward"
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
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 texel = Petal(input);
                float3 normal = normalize(input.normal) * IS_FRONT_VFACE(face, 1, -1);
                InputData data = (InputData)0;
                data.positionWS = input.world; data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                VertexPositionInputs shadowInput = (VertexPositionInputs)0;
                shadowInput.positionWS = input.world;
                shadowInput.positionCS = TransformWorldToHClip(input.world);
                data.shadowCoord = GetShadowCoord(shadowInput);
                // Scene-driven sky/bounce, like the City's plaster/paving. Thin petals gather
                // more diffuse skylight than opaque masonry, without an emissive shade floor.
                half3 hemisphere = lerp(unity_AmbientEquator.rgb,
                    normal.y >= 0 ? unity_AmbientSky.rgb : unity_AmbientGround.rgb, abs(normal.y));
                data.bakedGI = max(SampleSH(normal), hemisphere * 1.15);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS); data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = texel.rgb * lerp(_BaseColor.rgb, _TopColor.rgb, smoothstep(0.15, 0.9, input.height));
                surface.specular = 0.04; surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1); surface.occlusion = 1; surface.alpha = 1;
                surface.emission = 0;
                half4 color = UniversalFragmentPBR(data, surface);
                half fog = ComputeFogFactorZ0ToFar(max(0, distance(input.world, _WorldSpaceCameraPos) - _ProjectionParams.y));
                color.rgb = MixFog(color.rgb, fog); return color;
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
                Varyings o = Vert(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - o.world);
                #else
                    float3 direction = _LightDirection;
                #endif
                // Petal cards are two-sided. Bias toward the light-facing side, not an arbitrary
                // imported card normal, to avoid dark self-shadow stripes on back-facing petals.
                float3 normal = normalize(o.normal);
                normal *= dot(normal, direction) < 0 ? -1 : 1;
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.world, normal, direction));
                o.positionCS = ApplyShadowClamping(o.positionCS);
                return o;
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
