#ifndef CUBE_DASH_ROAD_CURVE
#define CUBE_DASH_ROAD_CURVE
// Distance, seed phase, strength, player contact Z. Keep in sync with RoadCurve.cs.
float4 _CubeDashRoadCurve;
float4 _CubeDashRoadOrigin;
void CubeDashPath(float distance, out float path, out float slope)
{
    float t = saturate((distance - 60) / 180);
    float sa, ca, sb, cb;
    sincos(distance * 0.012 + _CubeDashRoadCurve.y, sa, ca);
    sincos(distance * 0.021 + _CubeDashRoadCurve.y * 1.7, sb, cb);
    float basePath = 22 * sa + 10 * sb;
    float envelope = t * t * (3 - 2 * t);
    path = envelope * basePath;
    slope = envelope * (0.264 * ca + 0.21 * cb) + t * (1 - t) / 30 * basePath;
}
void CubeDashCurveSample(float z, out float offset, out float slope)
{
    float ahead = max(0, z - _CubeDashRoadCurve.w - 6);
    offset = 0; slope = 0;
    if (ahead <= 0 || _CubeDashRoadCurve.z <= 0) return;
    float sample = min(180, ahead);
    float path, derivative;
    CubeDashPath(_CubeDashRoadCurve.x + sample, path, derivative);
    slope = (derivative - _CubeDashRoadOrigin.y) * _CubeDashRoadCurve.z;
    offset = (path - _CubeDashRoadOrigin.x - _CubeDashRoadOrigin.y * sample) * _CubeDashRoadCurve.z
        + slope * (ahead - sample);
}
float CubeDashCurveOffset(float z)
{
    float offset, slope;
    CubeDashCurveSample(z, offset, slope);
    return offset;
}
void CubeDashBendVertex(inout float3 world, inout float3 normal)
{
    float offset, slope;
    CubeDashCurveSample(world.z, offset, slope);
    world.x += offset;
    normal = normalize(float3(normal.xy, normal.z - normal.x * slope));
}
float3 CubeDashBend(float3 world)
{
    world.x += CubeDashCurveOffset(world.z);
    return world;
}
float3 CubeDashBendNormal(float3 normal, float z)
{
    float offset, slope;
    CubeDashCurveSample(z, offset, slope);
    return normalize(float3(normal.xy, normal.z - normal.x * slope));
}
float3 CubeDashUnbend(float3 world)
{
    world.x -= CubeDashCurveOffset(world.z);
    return world;
}
#endif
