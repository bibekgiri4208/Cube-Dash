Shader "Hidden/CubeDash/Country Water Probe"
{
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "../../Shaders/CountrySideWaterMotion.hlsl"
            float _ProbeTime, _RippleScale, _RippleStrength;
            float4 _Flow;
            float4 Frag(v2f_img input) : SV_Target
            {
                return float4(CountryWaterNormal(input.uv * float2(8,16), _ProbeTime,
                    _Flow.xy, _RippleScale, _RippleStrength) * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
    }
}
