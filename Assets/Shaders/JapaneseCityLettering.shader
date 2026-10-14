Shader "CubeDash/Japanese City Lettering"
{
    Properties
    {
        _BaseMap ("Japanese glyph atlas", 2D) = "white" {}
        _BaseColor ("Letter color", Color) = (1, 0.95, 0.85, 1)
        _SceneryRise ("Scenery rise", Vector) = (0, 0, 80, 1.25)
        _SceneryStagger ("Scenery stagger", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "RoadCurve.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float4 _SceneryRise;
        float _SceneryStagger;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 anchor : TEXCOORD2; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float3 normal : TEXCOORD2; };
        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.world = TransformObjectToWorld(input.positionOS.xyz); o.normal = TransformObjectToWorldNormal(input.normalOS);
            float delay = (saturate(input.anchor.y / 42) * 0.75 + frac(abs(input.anchor.x) * 0.137) * 0.25) * _SceneryStagger;
            float reveal = smoothstep(0, 1, saturate((_CubeDashEnvironmentTime - _SceneryRise.y - delay) / max(0.1, _SceneryRise.w)));
            o.world.y -= _SceneryRise.x * _SceneryRise.z * (1 - reveal);
            CubeDashBendVertex(o.world, o.normal); o.positionCS = TransformWorldToHClip(o.world); o.uv = input.uv; return o;
        }
        half4 Depth(Varyings input) : SV_Target
        { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - 0.35); return 0; }
        ENDHLSL
        Pass
        {
            Name "Letters"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            half4 Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - 0.35);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                half light = 0.65 + 0.35 * saturate(dot(normalize(input.normal), sun.direction)) * sun.shadowAttenuation;
                half3 color = _BaseColor.rgb * light;
                return half4(MixFog(color, ComputeFogFactorZ0ToFar(max(0, distance(input.world, _WorldSpaceCameraPos) - _ProjectionParams.y))), 1);
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
            #pragma fragment Depth
            ENDHLSL
        }
    }
}
