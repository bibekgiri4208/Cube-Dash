Shader "CubeDash/Vehicle Armor Shield"
{
    Properties
    {
        [HDR] _Tint ("Armor field cyan", Color) = (0.12, 1.25, 1.8, 0.65)
        _EffectAge ("Game clock", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoadCurve.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _EffectAge;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 edge : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; float2 uv : TEXCOORD2; float2 edge : TEXCOORD3; half fog : TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.normal = TransformObjectToWorldNormal(input.normalOS);
                CubeDashBendVertex(output.world, output.normal);
                output.positionCS = TransformWorldToHClip(output.world);
                output.uv = input.uv; output.edge = input.edge;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float rim = pow(1 - saturate(dot(normalize(input.normal), normalize(_WorldSpaceCameraPos - input.world))), 2.5);
                // Fine staggered energy cells, not an opaque skin over the bodywork.
                float2 grid = input.uv * 8;
                grid.x += floor(grid.y) * 0.5;
                float2 cell = abs(frac(grid) - 0.5);
                float cellEdge = max(cell.x * 0.866 + cell.y * 0.5, cell.y);
                float hex = smoothstep(0.43, 0.46, cellEdge) * (1 - smoothstep(0.48, 0.5, cellEdge));
                float borderDistance = min(min(input.edge.x, 1 - input.edge.x), min(input.edge.y, 1 - input.edge.y));
                float border = 1 - smoothstep(0.008, 0.025, borderDistance);
                float scan = pow(saturate(1 - abs(frac(input.uv.y * 0.7 - _EffectAge * 0.35) - 0.5) * 12), 2);
                float pulse = 0.9 + 0.1 * sin(_EffectAge * 3);
                float alpha = saturate(0.018 + rim * 0.18 + hex * 0.10 + border * 0.52 + scan * 0.10);
                alpha *= _Tint.a * pulse * smoothstep(0, 0.12, _EffectAge);
                return half4(MixFog(_Tint.rgb * (0.8 + border * 0.5), input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
