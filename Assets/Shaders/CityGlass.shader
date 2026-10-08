Shader "CubeDash/City Glass"
{
    Properties
    {
        _BaseColor ("Glass tint", Color) = (0.045, 0.095, 0.16, 1)
        [HDR] _CoolWindows ("Cool windows", Color) = (0.3, 0.72, 0.88, 1)
        [HDR] _WarmWindows ("Warm windows", Color) = (0.95, 0.65, 0.35, 1)
        _WindowSpacing ("Window spacing (metres)", Vector) = (0.7, 1.4, 0, 0)
        _LitWindows ("Lit window fraction", Range(0, 1)) = 0.48
        _Seed ("Window pattern", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _CoolWindows, _WarmWindows;
                float4 _WindowSpacing;
                float _LitWindows, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 localMetres : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Scale with the tower, but don't move the window pattern as the track scrolls.
                output.localMetres = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            float Hash(float2 value) { return frac(sin(dot(value, float2(127.1, 311.7)) + _Seed * 13.7) * 43758.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = normalize(input.normalWS);
                float horizontal = abs(normal.z) > abs(normal.x) ? input.localMetres.x : input.localMetres.z;
                float2 grid = float2(horizontal, input.localMetres.y) / max(_WindowSpacing.xy, 0.01);
                float2 cell = floor(grid);
                float2 uv = frac(grid);
                float frame = step(0.16, uv.x) * step(uv.x, 0.84) * step(0.18, uv.y) * step(uv.y, 0.77);
                float facade = 1 - step(0.5, abs(normal.y));
                float random = Hash(cell);
                float lit = step(1 - _LitWindows, random) * frame * facade;
                float mullion = step(uv.x, 0.07) + step(uv.y, 0.07);
                Light key = GetMainLight();
                half light = 0.35 + saturate(dot(normal, key.direction)) * 0.7;
                half3 glass = _BaseColor.rgb * light + mullion * 0.015;
                half3 windows = lerp(_CoolWindows.rgb, _WarmWindows.rgb, step(0.83, random)) * lit;
                half3 color = glass + windows * lerp(0.55, 1.1, Hash(cell + 7));
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
