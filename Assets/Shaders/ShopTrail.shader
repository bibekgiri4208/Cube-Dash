Shader "CubeDash/Shop Cosmetic Trail"
{
    Properties
    {
        [HDR] _TintA ("Primary", Color) = (0.04, 1, 0.82, 1)
        [HDR] _TintB ("Secondary", Color) = (0.62, 0.2, 1, 1)
        _Style ("Style", Float) = 1
        _Age ("Game clock", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RoadCurve.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _TintA, _TintB;
                float _Style, _Age;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 data : TEXCOORD1; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float2 data : TEXCOORD2; half4 color : COLOR; float3 normal : TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(CubeDashBend(TransformObjectToWorld(input.positionOS.xyz)));
                o.uv = input.uv; o.data = input.data; o.color = input.color;
                o.normal = TransformObjectToWorldNormal(input.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 tint = input.color.rgb;
                if (input.data.x > 1.5)
                {
                    // Real rotating octahedra: per-face shading makes the ice/gems read as solid 3D forms.
                    if (!IS_FRONT_VFACE(frontFace, true, false)) discard;
                    float light = 0.43 + saturate(dot(SafeNormalize(input.normal), normalize(float3(-0.4, 0.8, -0.6)))) * 0.72;
                    half alpha = input.color.a;
                    half3 rgb = tint * light + pow(saturate(abs(input.normal.y)), 6) * 0.16;
                    return half4(MixFogColor(rgb, half3(0,0,0), input.fog) * alpha, alpha * 0.9);
                }
                float2 p = input.uv * 2 - 1;
                if (input.data.x > 0.75)
                {
                    float r2 = dot(p,p), halo = exp(-r2 * 4), core = exp(-r2 * 24);
                    float star = pow(saturate(1 - abs(p.x * p.y) * 18 - sqrt(r2) * 0.65), 4) * saturate(1 - sqrt(r2));
                    if (_Style > 1.5 && _Style < 2.5) star = core;
                    float alpha = input.color.a * (halo * 0.42 + core + star * 0.5);
                    half3 rgb = tint * (1.4 + core * 2.5) + star * 0.55;
                    return half4(MixFogColor(rgb, half3(0,0,0), input.fog) * alpha, alpha * 0.08);
                }
                float v = input.uv.y;
                float fade = pow(saturate(1-v), 1.15) * smoothstep(0, 0.04, v);
                float soft = pow(saturate(1-p.x*p.x), 2);
                float core = exp(-p.x*p.x*5);
                float pulse = _Style > 1.5 && _Style < 2.5 ? 0.78 + 0.22*sin(v*32-_Age*10) : 1;
                float opacity = soft * fade * pulse * input.color.a;
                half3 rgb = tint * (input.data.x < 0.25 ? 1.0 : 1.65 + core * 0.75) + core * 0.13;
                return half4(MixFogColor(rgb, half3(0,0,0), input.fog) * opacity, opacity * 0.12);
            }
            ENDHLSL
        }
    }
}
