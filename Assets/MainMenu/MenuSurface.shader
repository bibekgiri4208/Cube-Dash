Shader "CubeDash/Menu Surface"
{
    Properties
    {
        [MainColor] _BaseColor ("Surface color", Color) = (0.4,0.4,0.4,1)
        _WeatherColor ("Weathered color", Color) = (0.3,0.3,0.3,1)
        _SurfaceType ("0 Asphalt / 1 Concrete", Float) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WeatherColor;
            float _SurfaceType, _Smoothness;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.world = TransformObjectToWorld(input.positionOS.xyz);
            output.normal = TransformObjectToWorldNormal(input.normalOS);
            output.positionCS = TransformWorldToHClip(output.world);
            return output;
        }
        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 c = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(c), Hash(c + float2(1,0)), f.x), lerp(Hash(c + float2(0,1)), Hash(c + 1), f.x), f.y);
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            float3 normal = normalize(input.normal);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal,0);
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = normalize(input.normal);
                float2 p = abs(normal.y) > 0.6 ? input.world.xz : abs(normal.z) > 0.6 ? input.world.xy : input.world.zy;
                float coarse = Noise(p * 0.37), grain = Noise(p * 26);
                grain = lerp(grain, 0.5, smoothstep(0.5,2,max(length(ddx(p * 26)),length(ddy(p * 26)))));
                float weather = smoothstep(0.35,0.72,coarse + Noise(p * 1.9) * 0.17);
                half3 albedo = lerp(_BaseColor.rgb, _WeatherColor.rgb, weather * 0.8);
                albedo *= 0.87 + grain * 0.22 + Noise(p * 8) * 0.06;
                if (_SurfaceType < 0.5)
                {
                    float2 cell = floor(p * 0.7), f = frac(p * 0.7);
                    float first = 100, second = 100;
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                    {
                        float2 n = float2(x,y);
                        float2 site = n + float2(Hash(cell + n), Hash(cell + n + 17)) - f;
                        float d = dot(site,site);
                        if (d < first) { second = first; first = d; } else second = min(second,d);
                    }
                    float crack = (1 - smoothstep(0.005,0.027,second - first)) * smoothstep(0.48,0.7,Noise(p * 0.47 + 21));
                    albedo *= 1 - crack * 0.42;
                    float aggregate = smoothstep(0.83,0.97,grain);
                    albedo += half3(0.07,0.056,0.041) * aggregate;
                }
                else
                {
                    float leak = Noise(float2(p.x * 4,p.y * 0.18));
                    albedo *= 1 - smoothstep(0.65,0.87,leak) * 0.12;
                    albedo *= 0.92 + Noise(p * 75) * 0.09;
                }
                InputData data = (InputData)0;
                data.positionWS = input.world; data.normalWS = normal;
                float3 dx = ddx(input.world), dy = ddy(input.world);
                float3 rx = cross(dy,normal), ry = cross(normal,dx);
                float determinant = dot(dx,rx);
                data.normalWS = normalize(normal - sign(determinant) * (ddx(grain) * rx + ddy(grain) * ry) * 0.0025 / max(abs(determinant),0.0001));
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                data.shadowCoord = TransformWorldToShadowCoord(input.world);
                data.bakedGI = SampleSH(data.normalWS); data.shadowMask = 1;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo; surface.specular = 0.04; surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1); surface.occlusion = 1; surface.alpha = 1;
                half4 color = UniversalFragmentPBR(data,surface);
                color.rgb = MixFog(color.rgb,ComputeFogFactorZ0ToFar(max(0,distance(input.world,_WorldSpaceCameraPos) - _ProjectionParams.y)));
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
            float3 _LightDirection;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.world,output.normal,_LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
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
