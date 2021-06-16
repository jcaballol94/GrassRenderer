#ifndef NOISE_HLSL
#define NOISE_HLSL

float rand_1_05(in float2 uv)
{
    float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453));
    return abs(noise.x + noise.y) * 0.5;
}

float2 rand_2_10(in float2 uv) {
    float noiseX = (frac(sin(dot(uv, float2(12.9898,78.233) * 2.0)) * 43758.5453));
    float noiseY = sqrt(1 - noiseX * noiseX);
    return float2(noiseX, noiseY);
}

void noise_float(in float2 uv, out float noise)
{
    noise = rand_1_05(uv);
}

void noise2d_float(in float2 uv, out float2 noise)
{
    noise = rand_2_10(uv);
}

#endif