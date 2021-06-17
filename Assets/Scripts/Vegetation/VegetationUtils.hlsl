#ifndef VEGETATION_UTILS_HLSL
#define VEGETATION_UTILS_HLSL

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

#endif //VEGETATION_UTILS_HLSL