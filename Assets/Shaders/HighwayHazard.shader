Shader "CubeDash/Highway Hazard"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Surface albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Fine surface relief", 2D) = "bump" {}
        _BumpScale ("Relief strength", Range(0,1)) = 0.25
        _SurfaceMap ("AO / gloss / metal mask", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        [HDR] _EmissionColor ("Restrained lamp emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoadCurve.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _EmissionColor;
            float4 _BaseMap_ST;
            float _BumpScale, _Metallic, _Smoothness;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            half3 normal : TEXCOORD1;
            half4 tangent : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            float3 normal = TransformObjectToWorldNormal(input.normalOS);
            float3 tangent = TransformObjectToWorldDir(input.tangentOS.xyz);
            float offset, slope;
            CubeDashCurveSample(world.z, offset, slope);
            CubeDashBendVertex(world, normal);
            tangent.x += slope * tangent.z;
            tangent = normalize(tangent - normal * dot(normal, tangent));
            output.world = world; output.normal = normal;
            output.tangent = half4(tangent, input.tangentOS.w * GetOddNegativeScale());
            output.positionCS = TransformWorldToHClip(world);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap); output.color = input.color;
            return output;
        }
        half3 SurfaceNormal(Varyings input)
        {
            half3 n = normalize(input.normal), t = normalize(input.tangent.xyz);
            half3 b = cross(n, t) * input.tangent.w;
            half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
            return normalize(mul(detail, half3x3(t, b, n)));
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            half3 normal = SurfaceNormal(input);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
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
                half3 mask = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, input.uv).rgb;
                InputData data = (InputData)0;
                data.positionWS = input.world; data.normalWS = SurfaceNormal(input);
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                data.shadowCoord = TransformWorldToShadowCoord(input.world);
                data.bakedGI = SampleSH(data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS); data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb * input.color.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                surface.metallic = _Metallic * mask.b; surface.specular = 0.04;
                surface.smoothness = _Smoothness * mask.g; surface.normalTS = half3(0,0,1);
                surface.occlusion = mask.r; surface.alpha = 1;
                surface.emission = _EmissionColor.rgb;
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
