#ifndef CUBE_DASH_BEACH_TIDES
#define CUBE_DASH_BEACH_TIDES
float BeachTide(float time, float period)
{
    return sin(time * 6.2831853 / max(1, period));
}
float BeachShoreline(float2 world, float time, float shore, float distance, float period)
{
    // Accelerated visual tide plus smaller, irregular swash; shared by water and wet sand.
    return shore - BeachTide(time, period) * distance
        + sin(world.y * 0.16 + time * 0.9) * 0.28 + sin(world.y * 0.37 + time * 0.43) * 0.12;
}
#endif
