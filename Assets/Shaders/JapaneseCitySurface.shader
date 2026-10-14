Shader "CubeDash/Japanese City Surface"
{
    Properties
    {
        _BaseColor ("Surface color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Lower surface tint", Color) = (0.85, 0.88, 0.96, 1)
        _SurfaceType ("0 Solid / 1 Wood / 2 Plaster / 3 Glass / 4 Blossom / 5 Paving / 6 Metal / 7 Lantern", Float) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.18
        _Metallic ("Metallic", Range(0, 1)) = 0
        _OcclusionStrength ("Baked crevice shading", Range(0, 1)) = 0
        _EmissionStrength ("Lantern glow", Range(0, 2)) = 0
        _SceneryRise ("Scenery rise", Vector) = (0, 0, 80, 1.25)
        _SceneryStagger ("Scenery stagger", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoadCurve.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShadeColor;
            float _SurfaceType, _Smoothness, _Metallic, _OcclusionStrength, _EmissionStrength;
            float4 _SceneryRise;
            float _SceneryStagger;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 anchor : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float3 metres : TEXCOORD2;
            half occlusion : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
            o.world = TransformObjectToWorld(input.positionOS.xyz);
            o.normal = TransformObjectToWorldNormal(input.normalOS);
            // Local coordinates travel with the saved mesh, including across floating-origin resets.
            o.metres = input.positionOS.xyz;
            o.occlusion = lerp(1, input.color.r, _OcclusionStrength);
            float delay = (saturate(input.anchor.y / 42) * 0.75 + frac(abs(input.anchor.x) * 0.137) * 0.25) * _SceneryStagger;
            float reveal = smoothstep(0, 1, saturate((_CubeDashEnvironmentTime - _SceneryRise.y - delay) / max(0.1, _SceneryRise.w)));
            o.world.y -= _SceneryRise.x * _SceneryRise.z * (1 - reveal);
            CubeDashBendVertex(o.world, o.normal);
            o.positionCS = TransformWorldToHClip(o.world);
            return o;
        }
        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 c = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(c), Hash(c + float2(1, 0)), f.x), lerp(Hash(c + float2(0, 1)), Hash(c + 1), f.x), f.y);
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            float3 n = normalize(input.normal);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "City Surface"
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
                float3 normal = normalize(input.normal);
                float3 view = GetWorldSpaceNormalizeViewDir(input.world);
                half3 albedo = _BaseColor.rgb;
                float grain = Noise(input.metres.xz * 3 + input.metres.y * 0.7);
                float detailFade = 1 - smoothstep(30, 120, distance(input.world, _WorldSpaceCameraPos));
                if (_SurfaceType > 0.5 && _SurfaceType < 1.5)
                    albedo *= 0.95 + 0.05 * sin(input.metres.y * 23 + grain * 3) * detailFade;
                if (_SurfaceType > 1.5 && _SurfaceType < 2.5)
                    albedo *= 1 + (grain - 0.5) * 0.055 * detailFade;
                // Gentle facade/foliage gradients, not a uniform bright ambient wash.
                float height = _SurfaceType > 3.5 && _SurfaceType < 4.5
                    ? saturate((input.metres.y - 1.8) / 5.5) : saturate(input.metres.y / 9);
                albedo *= lerp(_ShadeColor.rgb, half3(1, 1, 1), height);
                albedo *= lerp(0.86, 1, input.occlusion);
                InputData data = (InputData)0;
                data.positionWS = input.world; data.normalWS = normal; data.viewDirectionWS = view;
                data.shadowCoord = TransformWorldToShadowCoord(input.world);
                // These pooled meshes have no baked light probes. Use the scene's current trilight
                // as a reliable sky/bounce floor rather than a stale or empty ambient SH sample.
                half3 hemisphere = lerp(unity_AmbientEquator.rgb,
                    normal.y >= 0 ? unity_AmbientSky.rgb : unity_AmbientGround.rgb, abs(normal.y));
                data.bakedGI = max(SampleSH(normal), hemisphere * 0.7);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS); data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo; surface.metallic = _Metallic; surface.specular = 0.04;
                surface.smoothness = _Smoothness; surface.normalTS = half3(0, 0, 1);
                surface.occlusion = input.occlusion; surface.alpha = 1;
                surface.emission = _BaseColor.rgb * _EmissionStrength;
                if (_SurfaceType > 2.5 && _SurfaceType < 3.5)
                {
                    float3 reflected = reflect(-view, normal);
                    half3 reflection = lerp(half3(0.17, 0.25, 0.39), half3(0.48, 0.71, 1), saturate(reflected.y * 1.5 + 0.35));
                    float fresnel = 0.12 + 0.42 * pow(1 - saturate(dot(normal, view)), 4);
                    float band = smoothstep(0.68, 0.72, frac((input.metres.y + input.metres.z * 0.26) * 0.16));
                    surface.emission += reflection * (fresnel + band * 0.09) * input.occlusion;
                }
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
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.world, normalize(o.normal), direction));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
                #endif
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
