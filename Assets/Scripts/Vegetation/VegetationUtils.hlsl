#ifndef VEGETATION_UTILS_HLSL
#define VEGETATION_UTILS_HLSL

#include "noise.hlsl"

void Billboard_float(in float3 InPos, in float3 ViewDir, out float3 OutPos)
{
	// Get the flat view dir (reversed)
	float3 forward = -ViewDir;
	forward.y = 0;
	normalize(forward);

	// Get the right vector
	float3 right = cross(float3(0,1,0), forward);

	// Construct a matrix with them
	float4x4 rotMatrix = 0;
	rotMatrix._m00_m10_m20 = right;
	rotMatrix._m02_m12_m22 = forward;
	rotMatrix._m11 = rotMatrix._m33 = 1;

	OutPos = mul(rotMatrix, float4(InPos, 1));
}

void SmallWind_float(in float3 WorldPivot, in float WindAmplitude, in float WindFrequency, in float Time, out float2 Wind)
{
	float2 noiseUv = WorldPivot.xz;

	// Get a random direction for this
	float3 dir = 0;
	dir.xz = normalize(rand_2_10(noiseUv));

	// Get a random angle based on the sinus
	float time = rand_1_05(noiseUv);
	time += Time;
	time *= WindFrequency;
	time = frac(time) * 360;
	float angle = WindAmplitude * sin(radians(time));

	// Return
	Wind = dir.xz * angle;
}

void ApplyWind_float(in float3 Pos, in float Height, in float2 Wind, out float3 OutPos)
{
	// Extract the data
	float angle = length(Wind);
	float2 direction = angle > 0 ? Wind / angle : 0;
	angle = radians(angle);

	OutPos = float3(0,0,0);
	OutPos.xz = Pos.xz + Height * direction * sin(angle);
	OutPos.y = Height * cos(angle);
}

#endif //VEGETATION_UTILS_HLSL