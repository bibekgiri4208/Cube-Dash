Shader "Hidden/CubeDash/Peripheral Motion Blur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Peripheral Speed Blur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _BlurRadius;
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // Protect a widening corridor toward the player, including all three lanes.
                // Sky/clouds also stay crisp: blur belongs to the passing roadside scenery.
                float corridor = lerp(0.17, 0.34, 1 - smoothstep(0.1, 0.65, uv.y));
                float mask = smoothstep(corridor, corridor + 0.13, abs(uv.x - 0.5));
                mask *= 1 - smoothstep(0.60, 0.83, uv.y);
                float2 stepUV = (uv - float2(0.5, 0.57)) * (_BlurRadius * mask);
                half4 blurred = original * 0.28;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv - stepUV)) * 0.22;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv + stepUV)) * 0.22;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv - stepUV * 2)) * 0.14;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv + stepUV * 2)) * 0.14;
                return half4(blurred.rgb, original.a);
            }
            ENDHLSL
        }
    }
}
