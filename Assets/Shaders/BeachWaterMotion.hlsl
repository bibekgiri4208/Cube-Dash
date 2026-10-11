#ifndef CUBE_DASH_BEACH_WATER_MOTION
#define CUBE_DASH_BEACH_WATER_MOTION
#include "BeachTides.hlsl"

float BoundaryDistance(float2 metres)
{
    float nearRoad = 1 - smoothstep(7, 120, metres.x);
    float bank = 8 + nearRoad * 22 + sin(metres.x * 0.055) * 2.8 + sin(metres.x * 0.17) * 1.2;
    return min(_CoastLimits.x > 0.5 ? metres.y - (_CoastLimits.z + bank) : 10000,
        _CoastLimits.y > 0.5 ? (_CoastLimits.w - bank) - metres.y : 10000);
}
float RiverWidth(float2 metres)
{
    float endDistance = min(_RiverEnds.x > 0.5 ? metres.y - _RiverEnds.z : 10000,
        _RiverEnds.y > 0.5 ? _RiverEnds.w - metres.y : 10000);
    return _RiverHalfWidth * smoothstep(4, 20, endDistance);
}

float BeachBedColumnHeight(float x, float waterY)
{
    return waterY + _BeachLandOffset - smoothstep(5.2, 12, x) * _BeachBedDepth
        - clamp((x - 12) * 0.015, 0, 8);
}
float RaisedBeachBed(float x, float z, float waterY)
{
    float height = BeachBedColumnHeight(x, waterY);
    float distance = min(_CoastLimits.x > 0.5 ? abs(z - _CoastLimits.z) : 10000,
        _CoastLimits.y > 0.5 ? abs(_CoastLimits.w - z) : 10000);
    return lerp(height, max(height, waterY + _BeachLandOffset), 1 - smoothstep(0, 18, distance));
}
float BeachRenderedBed(float2 world, float2 metres, float waterY)
{
    // Match the saved floor columns and the GPU-raised, one-metre transition rows.
    // Analytic height alone does not match the actual triangulated shoreline.
    float a, b;
    float x = world.x;
    if (x <= 5.2) { a = 4.8; b = 5.2; }
    else if (x < 7) { a = 5.2 + floor((x - 5.2) / 0.4) * 0.4; b = a + 0.4; }
    else if (x < 8) { a = 7; b = 8; }
    else if (x < 10.5) { a = 8 + floor((x - 8) / 0.5) * 0.5; b = a + 0.5; }
    else if (x < 12) { a = 10.5; b = 12; }
    else if (x < 20) { a = 12; b = 20; }
    else if (x < 64) { a = 20; b = 64; }
    else if (x < 320) { a = 64; b = 320; }
    else { a = 320; b = 1024; }
    float tx = saturate((x - a) / (b - a));
    if (_CoastLimits.x < 0.5 && _CoastLimits.y < 0.5)
        return lerp(BeachBedColumnHeight(a, waterY), BeachBedColumnHeight(b, waterY), tx);
    float z = floor(metres.y - _CoastLimits.z) + _CoastLimits.z;
    float tz = saturate(metres.y - z);
    float h00 = RaisedBeachBed(a, z, waterY), h10 = RaisedBeachBed(b, z, waterY);
    float h01 = RaisedBeachBed(a, z + 1, waterY), h11 = RaisedBeachBed(b, z + 1, waterY);
    return tx + tz <= 1 ? h00 + (h10 - h00) * tx + (h01 - h00) * tz
        : h11 + (h01 - h11) * (1 - tx) + (h10 - h11) * (1 - tz);
}
float OceanDepth(float3 world, float3 surface, float2 metres)
{
    return world.y - BeachRenderedBed(world.xz, metres + world.xz - surface.xz, surface.y);
}
float WaterCoverage(float3 world, float3 surface, float2 metres)
{
    if (_ShoreMode < 0.5)
        return min(BoundaryDistance(metres), RiverWidth(metres) - abs(surface.x - _RiverCenter) - 0.001);
    // The beach tile ends are already dry land. Do not extend boundary water aprons
    // across the adjacent biome, but never cut an interior water edge above the bed.
    if ((_CoastLimits.x > 0.5 && metres.y < _CoastLimits.z)
        || (_CoastLimits.y > 0.5 && metres.y > _CoastLimits.w)) return -1;
    return OceanDepth(world, surface, metres);
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
    tangentX += float3(-chop * k * direction.x * direction.x * s, a * k * direction.x * c, -chop * k * direction.x * direction.y * s);
    tangentZ += float3(-chop * k * direction.x * direction.y * s, a * k * direction.y * c, -chop * k * direction.y * direction.y * s);
    crest += (s * 0.5 + 0.5) * weight;
}
float3 WaterMotion(float3 surface, float2 metres, out float3 normal, out float crest)
{
    float t = _CubeDashEnvironmentTime;
    if (_ShoreMode < 0.5)
    {
        float boundary = smoothstep(0, 3, BoundaryDistance(metres));
        float2 p = surface.xz - float2(0, t * _FlowSpeed);
        float bank = max(0, RiverWidth(metres) - abs(surface.x - _RiverCenter));
        float edge = smoothstep(0, 0.65, bank);
        float a = dot(p, float2(2.1, 1.35)), b = dot(p, float2(-3.4, 2.6)), c = dot(p, float2(5.8, 4.1));
        float height = (sin(a) + sin(b) * 0.4 + sin(c) * 0.12) * _WaveHeight * boundary;
        float2 slope = (float2(2.1, 1.35) * cos(a) + float2(-3.4, 2.6) * cos(b) * 0.4
            + float2(5.8, 4.1) * cos(c) * 0.12) * _WaveHeight * boundary * edge;
        float u = saturate(bank / 0.65);
        slope.x += height * (6 * u * (1 - u) / 0.65) * -sign(surface.x - _RiverCenter);
        normal = normalize(float3(-slope.x, 1, -slope.y)); crest = 0;
        return float3(0, height * edge, 0);
    }
    float coast = surface.x - BeachShoreline(surface.xz, t, _ShoreX, _TideDistance, _TidePeriod);
    float depth = max(0, surface.y - BeachRenderedBed(surface.xz, metres, surface.y));
    float boundary = smoothstep(0, _ShoreContactWidth, depth);
    float attenuation = smoothstep(0, 8, max(0, coast)) * boundary;
    float3 offset = 0, tangentX = float3(1, 0, 0), tangentZ = float3(0, 0, 1);
    crest = 0;
    Swell(surface.xz, float2(-0.96, 0.28), 20, 1, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
    Swell(surface.xz, float2(-0.81, -0.59), 11, 0.45, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
    Swell(surface.xz, float2(-0.52, 0.85), 6, 0.22, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
    Swell(surface.xz, float2(0.65, 0.76), 3.2, 0.1, _WaveHeight * attenuation, offset, tangentX, tangentZ, crest);
    crest /= 1.77;
    normal = normalize(cross(tangentZ, tangentX));
    offset.y += BeachTide(t, _TidePeriod) * _TideHeight - (1 - smoothstep(0, 3, max(0, coast))) * 0.24;
    return offset;
}
#endif
