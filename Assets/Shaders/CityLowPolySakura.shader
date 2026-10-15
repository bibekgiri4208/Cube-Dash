Shader "CubeDash/City Low Poly Sakura"
{
    Properties
    {
        _BaseColor ("Vertex palette tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Matte surface", Range(0, 1)) = 0.08
        _WindStrength ("Gentle whole-tree flex", Range(0, 0.12)) = 0.055
        _SurfaceMap ("Timber grain", 2D) = "gray" {}
        _BarkDetail ("Timber only texture", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Smoothness, _WindStrength, _BarkDetail;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float3 normal : TEXCOORD1;
            half4 color : TEXCOORD2;
            float2 timberUV : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 position = input.positionOS.xyz;
            float weight = smoothstep(2.2, 7, position.y);
            float phase = _CubeDashEnvironmentTime * 1.2 + position.y * 0.13;
            float sway = sin(phase) * _WindStrength * weight * weight;
            position.xz += float2(sway, sway * 0.35);
            // The saved CitySakuraTrees roots already follow road bend/reveal. Do not bend twice.
            // Bark and blossom vertices share this flex in every pass and every LOD.
            o.world = TransformObjectToWorld(position);
            o.normal = TransformObjectToWorldNormal(input.normalOS);
            o.positionCS = TransformWorldToHClip(o.world); o.color = input.color;
            o.timberUV = abs(input.normalOS.x) > abs(input.normalOS.z) ? input.positionOS.zy : input.positionOS.xy;
            return o;
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalFrag(Varyings input) : SV_Target
        {
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
            Name "Low Poly Sakura Forward"
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
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normal);
                InputData data = (InputData)0;
                data.positionWS = input.world; data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                VertexPositionInputs shadowInput = (VertexPositionInputs)0;
                shadowInput.positionWS = input.world; shadowInput.positionCS = TransformWorldToHClip(input.world);
                data.shadowCoord = GetShadowCoord(shadowInput);
                half3 hemisphere = lerp(unity_AmbientEquator.rgb,
                    normal.y >= 0 ? unity_AmbientSky.rgb : unity_AmbientGround.rgb, abs(normal.y));
                data.bakedGI = max(SampleSH(normal), hemisphere * 0.85);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS); data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = input.color.rgb * _BaseColor.rgb;
                half fibre = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, input.timberUV * float2(2, 0.12)).b;
                float fade = 1 - smoothstep(25, 85, distance(input.world, _WorldSpaceCameraPos));
                surface.albedo *= 1 + (fibre - 0.5) * 0.4 * _BarkDetail * fade;
                surface.specular = 0.04; surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1); surface.occlusion = input.color.a; surface.alpha = 1;
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
                o.positionCS = ApplyShadowClamping(o.positionCS); return o;
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
