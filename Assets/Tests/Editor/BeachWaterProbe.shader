Shader "Hidden/CubeDash/Beach Water Probe"
{
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _SampleArea, _CoastLimits, _RiverEnds;
            float _WaterY, _WaveHeight, _ShoreMode, _ShoreX, _ShallowWidth, _ExportCoverage;
            float _WaveSpeed, _Choppiness, _FoamStrength, _TideHeight, _TideDistance, _TidePeriod;
            float _RiverCenter, _RiverHalfWidth, _FlowSpeed, _RiverFoam;
            float _BeachLandOffset, _BeachBedDepth, _ShoreContactWidth, _CubeDashEnvironmentTime;
            #include "../../Shaders/BeachWaterMotion.hlsl"
            float4 frag(v2f_img input) : SV_Target
            {
                float2 p = _SampleArea.xy + input.uv * _SampleArea.zw;
                float3 normal; float crest;
                float3 motion = WaterMotion(float3(p.x, _WaterY, p.y), p, normal, crest);
                if (_ExportCoverage > 0.5)
                {
                    float3 surface = float3(p.x, _WaterY, p.y), world = surface + motion;
                    return float4(BeachRenderedBed(world.xz, p + motion.xz, _WaterY), OceanDepth(world, surface, p),
                        WaterCoverage(world, surface, p), normal.y);
                }
                return float4(BeachRenderedBed(p, p, _WaterY), _WaterY + motion.y, motion.x, motion.z);
            }
            ENDHLSL
        }
    }
}
