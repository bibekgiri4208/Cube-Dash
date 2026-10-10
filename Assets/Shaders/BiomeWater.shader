Shader "CubeDash/Biome Water"
{
    Properties
    {
        _BaseColor ("Deep water", Color) = (0.025, 0.24, 0.34, 1)
        _ShallowColor ("Lagoon shallows", Color) = (0.12, 0.64, 0.63, 1)
        _FoamColor ("Foam", Color) = (0.86, 0.96, 0.93, 1)
        _ReflectionColor ("Sky reflection", Color) = (0.56, 0.79, 0.88, 1)
        _WaveHeight ("Swell amplitude", Range(0, 1.2)) = 0.65
        _WaveSpeed ("Wave speed", Range(0.3, 2)) = 1
        _Choppiness ("Crest choppiness", Range(0, 0.9)) = 0.55
        _FoamStrength ("Surf and whitecaps", Range(0, 1.5)) = 0.9
        _TideHeight ("Tide height", Range(0, 0.25)) = 0.12
        _TideDistance ("Tidal shoreline travel", Range(0, 1.5)) = 0.85
        _TidePeriod ("Tide period (seconds)", Range(15, 120)) = 40
        _ShoreMode ("Coastal surf", Range(0, 1)) = 1
        _ShoreX ("Shoreline X", Float) = 7
        _ShallowWidth ("Shallows width", Float) = 22
        _CoastLimits ("Boundary bay (entrance, exit, start Z, end Z)", Vector) = (0, 0, 0, 42)
        _RiverCenter ("River center X", Float) = -29
        _RiverHalfWidth ("River half width", Float) = 2.5
        _FlowSpeed ("River current speed", Range(0, 3)) = 1.2
        _RiverFoam ("River bank foam", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "BeachTides.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor, _FoamColor, _ReflectionColor;
            float _WaveHeight, _ShoreMode, _ShoreX, _ShallowWidth;
            float _WaveSpeed, _Choppiness, _FoamStrength, _TideHeight, _TideDistance, _TidePeriod;
            float4 _CoastLimits;
            float _RiverCenter, _RiverHalfWidth, _FlowSpeed, _RiverFoam;
        CBUFFER_END
        float _CubeDashEnvironmentTime;
        struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 world : TEXCOORD0;
            float2 metres : TEXCOORD1;
            float3 surface : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        float BoundaryDistance(float2 metres)
        {
            float nearRoad = 1 - smoothstep(7, 120, metres.x);
            float bank = 8 + nearRoad * 22 + sin(metres.x * 0.055) * 2.8 + sin(metres.x * 0.17) * 1.2;
            float entry = metres.y - (_CoastLimits.z + bank);
            float exit = (_CoastLimits.w - bank) - metres.y;
            return min(_CoastLimits.x > 0.5 ? entry : 10000, _CoastLimits.y > 0.5 ? exit : 10000);
        }
        void Swell(float2 p, float2 direction, float wavelength, float weight, float amplitude,
            inout float3 offset, inout float3 tangentX, inout float3 tangentZ, inout float crest)
        {
            direction = normalize(direction);
            float k = 6.2831853 / wavelength;
            float phase = dot(p, direction) * k - _CubeDashEnvironmentTime * sqrt(9.81 * k) * _WaveSpeed;
            float s, c; sincos(phase, s, c);
            float a = amplitude * weight, chop = _Choppiness * a;
            offset += float3(direction.x * chop * c, a * s, direction.y * chop * c);
            tangentX += float3(-chop * k * direction.x * direction.x * s, a * k * direction.x * c,
                -chop * k * direction.x * direction.y * s);
            tangentZ += float3(-chop * k * direction.x * direction.y * s, a * k * direction.y * c,
                -chop * k * direction.y * direction.y * s);
            crest += (s * 0.5 + 0.5) * weight;
        }
        float3 WaterMotion(float3 surface, float2 metres, out float3 normal, out float crest)
        {
            float t = _CubeDashEnvironmentTime;
            float boundary = smoothstep(0, 3, BoundaryDistance(metres));
            if (_ShoreMode < 0.5)
            {
                // River ripples advect downstream and flatten against both banks; no ocean tides.
                float2 p = surface.xz - float2(0, t * _FlowSpeed);
                float bank = max(0, _RiverHalfWidth - abs(surface.x - _RiverCenter));
                float edge = smoothstep(0, 0.65, bank);
                float a = dot(p, float2(2.1, 1.35));
                float b = dot(p, float2(-3.4, 2.6));
                float c = dot(p, float2(5.8, 4.1));
                float height = (sin(a) + sin(b) * 0.4 + sin(c) * 0.12) * _WaveHeight * boundary;
                float2 slope = (float2(2.1, 1.35) * cos(a)
                    + float2(-3.4, 2.6) * cos(b) * 0.4
                    + float2(5.8, 4.1) * cos(c) * 0.12) * _WaveHeight * boundary * edge;
                float u = saturate(bank / 0.65);
                slope.x += height * (6 * u * (1 - u) / 0.65) * -sign(surface.x - _RiverCenter);
                normal = normalize(float3(-slope.x, 1, -slope.y)); crest = 0;
                return float3(0, height * edge, 0);
            }
            float coast = surface.x - BeachShoreline(surface.xz, t, _ShoreX, _TideDistance, _TidePeriod);
            float attenuation = smoothstep(0, 8, max(0, coast)) * boundary;
            float3 offset = 0, tangentX = float3(1, 0, 0), tangentZ = float3(0, 0, 1);
            crest = 0;
            Swell(surface.xz, float2(-0.96, 0.28), 20, 1, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
            Swell(surface.xz, float2(-0.81, -0.59), 11, 0.45, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
            Swell(surface.xz, float2(-0.52, 0.85), 6, 0.22, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
            Swell(surface.xz, float2(0.65, 0.76), 3.2, 0.1, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
            crest /= 1.77;
            normal = normalize(cross(tangentZ, tangentX));
            offset.y += BeachTide(t, _TidePeriod) * _TideHeight * boundary
                - (1 - smoothstep(0, 3, max(0, coast))) * 0.24;
            return offset;
        }
        void WaterClip(Varyings input)
        {
            clip(BoundaryDistance(input.metres));
            if (_ShoreMode > 0.5)
                clip(input.surface.x - BeachShoreline(input.surface.xz, _CubeDashEnvironmentTime, _ShoreX, _TideDistance, _TidePeriod));
        }
        float FoamNoise(float2 p)
        {
            float2 cell = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            float a = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            float b = frac(sin(dot(cell + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
            float c = frac(sin(dot(cell + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
            float d = frac(sin(dot(cell + 1, float2(127.1, 311.7))) * 43758.5453);
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.world = TransformObjectToWorld(input.positionOS.xyz);
            output.metres = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz).xz;
            output.surface = output.world;
            float3 normal; float crest;
            output.world += WaterMotion(output.surface, output.metres, normal, crest);
            output.positionCS = TransformWorldToHClip(output.world);
            return output;
        }
        half4 NormalFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            WaterClip(input);
            float3 normal; float crest;
            WaterMotion(input.surface, input.metres, normal, crest);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        half4 DepthFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            WaterClip(input);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "Water Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float bayDistance = BoundaryDistance(input.metres);
                WaterClip(input);
                float t = _CubeDashEnvironmentTime;
                float3 normal; float crest;
                float3 motion = WaterMotion(input.surface, input.metres, normal, crest);
                float distanceToCamera = distance(_WorldSpaceCameraPos, input.world);
                float detailFade = 1 - smoothstep(25, 110, distanceToCamera);
                if (_ShoreMode < 0.5)
                {
                    float2 flow = input.surface.xz - float2(0, t * _FlowSpeed);
                    float bank = max(0, _RiverHalfWidth - abs(input.surface.x - _RiverCenter));
                    float shallow = 1 - smoothstep(0.05, 1.8, bank);
                    float current = FoamNoise(float2(flow.x * 3.5, flow.y * 0.38));
                    float eddy = FoamNoise(flow * float2(1.4, 0.75) + float2(sin(flow.y * 0.6) * 0.3, 0));
                    half3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, shallow * 0.85);
                    color *= 0.94 + current * 0.12 * detailFade;
                    color += _ShallowColor.rgb * pow(saturate(eddy * 1.35), 5) * shallow * detailFade * 0.07;
                    float3 view = normalize(_WorldSpaceCameraPos - input.world);
                    float fresnel = 0.035 + 0.42 * pow(1 - saturate(dot(normal, view)), 5);
                    color = lerp(color, lerp(unity_FogColor.rgb, _ReflectionColor.rgb, 0.5), fresnel);
                    Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                    color *= 0.7 + saturate(dot(normal, sun.direction)) * 0.35 * sun.shadowAttenuation;
                    color += sun.color * pow(saturate(dot(normal, normalize(view + sun.direction))), 110) * 0.28 * sun.shadowAttenuation;
                    float foam = (1 - smoothstep(0.08, 0.6, bank)) * smoothstep(0.48, 0.8, eddy)
                        + smoothstep(0.82, 0.96, current) * 0.16 * detailFade;
                    color = lerp(color, _FoamColor.rgb, saturate(foam * _RiverFoam));
                    half fog = ComputeFogFactorZ0ToFar(max(0, distanceToCamera - _ProjectionParams.y));
                    return half4(MixFog(color, fog), 1);
                }
                normal.xz += float2(sin(input.surface.z * 3.7 + input.surface.x * 1.1 + t * 2.1),
                    cos(input.surface.x * 4.3 - input.surface.z * 0.9 - t * 1.7)) * 0.045 * detailFade;
                normal = normalize(normal);
                float3 view = normalize(_WorldSpaceCameraPos - input.world);
                float coast = max(0, input.surface.x - BeachShoreline(input.surface.xz, t, _ShoreX, _TideDistance, _TidePeriod));
                float shallow = _ShoreMode * (1 - smoothstep(0, _ShallowWidth, coast));
                shallow = max(shallow, _ShoreMode * (1 - smoothstep(0, 12, bayDistance)));
                half3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, shallow * 0.86 + 0.09);
                float caustic = pow(saturate(sin(input.world.x * 0.95 + t * 0.4)
                    * cos(input.world.z * 1.15 - t * 0.3)), 7);
                color += _ShallowColor.rgb * caustic * shallow * detailFade * 0.10;
                float fresnel = 0.025 + 0.55 * pow(1 - saturate(dot(normal, view)), 5);
                half3 reflection = lerp(unity_FogColor.rgb, _ReflectionColor.rgb, saturate(reflect(-view, normal).y) * 0.7);
                color = lerp(color, reflection, fresnel);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                color *= 0.62 + saturate(dot(normal, sun.direction)) * 0.48 * sun.shadowAttenuation;
                color += _ShallowColor.rgb * saturate(motion.y / max(0.01, _WaveHeight)) * shallow * 0.14;
                float glint = pow(saturate(dot(normal, normalize(view + sun.direction))), lerp(90, 180, detailFade));
                color += sun.color * glint * 0.52 * sun.shadowAttenuation;
                float shoreFoam = 1 - smoothstep(0.18, 1.1, coast);
                float breaker = pow(saturate(sin(coast * 0.62 + t * 1.45 + sin(input.surface.z * 0.18) * 0.35)), 7);
                float foamNoise = FoamNoise(input.surface.xz * 1.7 + float2(-t * 0.4, t * 0.35)) * 0.65 + 0.35;
                float foam = (shoreFoam * 0.92 + breaker * (1 - smoothstep(4, 18, coast)) * 0.55)
                    * foamNoise * _ShoreMode;
                float baySurf = 1 - smoothstep(0.25, 1.5, bayDistance);
                foam = max(foam, baySurf * foamNoise * _ShoreMode * (0.65 + 0.15 * sin(t + input.metres.x * 0.2)));
                float whitecap = smoothstep(0.67, 0.88, crest) * foamNoise * detailFade * 0.4 * _ShoreMode;
                color = lerp(color, _FoamColor.rgb, saturate((foam + whitecap) * _FoamStrength));
                // Distance fog hides lateral edges too, unlike depth-only fog at a wide FOV.
                half fog = ComputeFogFactorZ0ToFar(max(0, distanceToCamera - _ProjectionParams.y));
                return half4(MixFog(color, fog), 1);
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
