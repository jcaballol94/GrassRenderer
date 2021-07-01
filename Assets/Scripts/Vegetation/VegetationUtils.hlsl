#ifndef VEGETATION_UTILS_HLSL
#define VEGETATION_UTILS_HLSL

#include "noise.hlsl"

float4 _GlobalWindDir;
float4 _GlobalWindParams;
float4 _GlobalWindIntDir;
float4 _GlobalWindIntParams;

void Billboard_float(in float3 InPos, in float3 ViewDir, out float3 OutPos)
{
	// Get the flat view dir (reversed)
	float3 forward = -ViewDir;
	forward.y = 0;
	normalize(forward);

	// Get the right vector
	float3 right = normalize(cross(float3(0,1,0), forward));

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
	float2 dir = normalize(rand_2_10(noiseUv) - 0.5);

	// Get a random angle based on the sinus
	float time = rand_1_05(noiseUv * 7.235);
	time += Time;
	time *= WindFrequency;
	time = frac(time) * 360;
	float angle = WindAmplitude * sin(radians(time));

	// Return
	Wind = dir * angle;
}

float ComputeWindWave (in float3 WorldPivot, in float Time, in float3 Dir, in float3 Params)
{
    float time = Time;
	time *= Params.y;
    time -= dot(Dir.xyz, WorldPivot) / Params.x;
	time = frac(time) * 360;
	return Params.z * (sin(radians(time)) * 0.5 + 0.5);
}

void BigWind_float(in float3 WorldPivot, in float Time, out float2 Wind, out float WindNorm)
{
	// Extract the dir
	float2 dir = _GlobalWindDir.xz;

	// Calculate the waves
    float angle = ComputeWindWave(WorldPivot, Time, _GlobalWindDir.xyz, _GlobalWindParams.xyz);
    float angle2 = ComputeWindWave(WorldPivot, Time, _GlobalWindIntDir.xyz, _GlobalWindIntParams.xyz);

    float avgAngle = (angle + angle2) * 0.5;
    Wind = dir * avgAngle;
    WindNorm = (avgAngle / _GlobalWindParams.z);
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