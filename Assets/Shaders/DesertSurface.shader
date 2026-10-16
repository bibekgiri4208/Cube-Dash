Shader "CubeDash/Desert Surface"
{
    Properties
    {
        _BaseColor ("Surface tint", Color) = (1,1,1,1)
        _SurfaceMap ("Mineral grain / sand ripples / mottling", 2D) = "gray" {}
        _SurfaceType ("0 Sand / 1 Sandstone / 2 Cactus / 3 Dry grass", Float) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _DetailStrength ("Texture contrast", Range(0,1)) = 0.35
        _WindStrength ("Plant flex", Float) = 0
        _SceneryRise ("Scenery rise", Vector) = (0,0,80,1.25)
        _SceneryStagger ("Scenery stagger", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoadCurve.hlsl"
        TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _SurfaceType, _Smoothness, _DetailStrength, _WindStrength;
            float4 _SceneryRise;
            float _SceneryStagger;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 wind : TEXCOORD1;
            float2 anchor : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float3 normal : TEXCOORD1;
            float3 metres : TEXCOORD2;
            half3 tint : TEXCOORD3;
            float2 uv : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        Varyings Vert(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, o);
            float3 position = input.positionOS.xyz;
            position.x += sin(_CubeDashEnvironmentTime * 1.6 + input.wind.y) * input.wind.x * _WindStrength;
            o.world = TransformObjectToWorld(position); o.normal = TransformObjectToWorldNormal(input.normalOS);
            float4x4 m = GetObjectToWorldMatrix();
            float3 scale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
            o.metres = input.positionOS.xyz * scale;
            float3 axis = abs(input.normalOS);
            o.uv = axis.y > max(axis.x, axis.z) ? o.metres.xz : axis.x > axis.z ? o.metres.zy : o.metres.xy;
            o.tint = input.color.rgb;
            float delay = (saturate(input.anchor.y / 42) * 0.75 + frac(abs(input.anchor.x) * 0.137) * 0.25) * _SceneryStagger;
            float reveal = smoothstep(0, 1, saturate((_CubeDashEnvironmentTime - _SceneryRise.y - delay) / max(0.1, _SceneryRise.w)));
            o.world.y -= _SceneryRise.x * _SceneryRise.z * (1 - reveal);
            CubeDashBendVertex(o.world, o.normal); o.positionCS = TransformWorldToHClip(o.world);
            return o;
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
            Name "Desert Forward"
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
                float dist = distance(input.world, _WorldSpaceCameraPos);
                float fade = 1 - smoothstep(25, 100, dist);
                half3 grain = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, input.uv * 2).rgb;
                half macro = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, input.uv / 3.5).b;
                float detail = (grain.r - 0.5) * 0.16 * fade + (macro - 0.5) * (_SurfaceType > 0.5 && _SurfaceType < 1.5 ? 0.45 : 0.13);
                if (_SurfaceType < 0.5)
                {
                    half ripple = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, input.metres.xz / 3.5).g;
                    detail += (ripple - 0.5) * 0.17 * fade;
                }
                half3 albedo = _BaseColor.rgb * input.tint * (1 + detail * _DetailStrength);
                float3 normal = normalize(input.normal);
                float relief = grain.r * (_SurfaceType < 1.5 ? 0.035 : 0.006) * fade;
                float3 dx = ddx(input.world), dy = ddy(input.world);
                float3 rx = cross(dy, normal), ry = cross(normal, dx);
                float det = dot(dx, rx);
                float3 gradient = sign(det) * (ddx(relief) * rx + ddy(relief) * ry) / max(abs(det), 0.0001);
                normal = normalize(normal - clamp(gradient, -0.12, 0.12));
                InputData data = (InputData)0;
                data.positionWS = input.world; data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                VertexPositionInputs shadowInput = (VertexPositionInputs)0;
                shadowInput.positionWS = input.world; shadowInput.positionCS = TransformWorldToHClip(input.world);
                data.shadowCoord = GetShadowCoord(shadowInput);
                half3 fill = lerp(unity_AmbientEquator.rgb, normal.y >= 0 ? unity_AmbientSky.rgb : unity_AmbientGround.rgb, abs(normal.y));
                data.bakedGI = max(SampleSH(normal), fill);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS); data.shadowMask = 1;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo; surface.specular = 0.04; surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1); surface.occlusion = 1; surface.alpha = 1;
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, ComputeFogFactorZ0ToFar(max(0, dist - _ProjectionParams.y)));
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
                Varyings o = Vert(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - o.world);
                #else
                    float3 direction = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.world, normalize(o.normal), direction)));
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
