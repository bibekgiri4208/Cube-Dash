#ifndef CUBE_DASH_COUNTRY_WATER_MOTION
#define CUBE_DASH_COUNTRY_WATER_MOTION

// Metre-based, unscrolled route coordinates keep ripples attached to travelling scenery.
// Use the run-owned environment clock, not _Time: pause freezes water, retry resets it.
float3 CountryWaterNormal(float2 metres, float time, float2 flow, float scale, float strength)
{
    float2 p = (metres - flow * time) * scale;
    float warpA = dot(p, float2(0.43, 0.71)) - time * 0.13;
    float warpB = dot(p, float2(-0.61, 0.37)) + time * 0.18;
    float a = dot(p, float2(1.8, 3.1)) + sin(warpA) * 1.4 + time * 0.75;
    float b = dot(p, float2(-3.8, 1.6)) + sin(warpB) * 1.1 - time * 0.56;
    float c = dot(p, float2(7.4, 5.2)) + time * 1.05;
    float2 slope = (float2(1.8, 3.1) + float2(0.43, 0.71) * cos(warpA) * 1.4) * cos(a) * 0.36
        + (float2(-3.8, 1.6) + float2(-0.61, 0.37) * cos(warpB) * 1.1) * cos(b) * 0.20
        + float2(7.4, 5.2) * cos(c) * 0.055;
    return normalize(float3(-slope.x * strength, 1, -slope.y * strength));
}

#endif
