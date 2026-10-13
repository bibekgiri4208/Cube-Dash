Shader "CubeDash/Countryside Ground"
{
    Properties
    {
        _BaseColor ("Landscape tint", Color) = (1,1,1,1)
        _DetailStrength ("Fine ground texture", Range(0,0.2)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Landscape"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RoadCurve.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _DetailStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; half fog : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                CubeDashBendVertex(output.positionWS, output.normalWS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color * _BaseColor; output.fog = ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half noise = frac(sin(dot(floor(CubeDashUnbend(input.positionWS).xz * 5), float2(12.9898,78.233))) * 43758.5453);
                half3 albedo = input.color.rgb * (1 + (noise - 0.5h) * _DetailStrength);
                half3 light = SampleSH(normal) * 0.7h + sun.color * saturate(dot(normal, sun.direction)) * sun.shadowAttenuation * 0.8h;
                return half4(MixFog(albedo * light, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
