#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<float4> _Positions;
#endif

void ConfigureProcedural () {
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
		float4 position = _Positions[unity_InstanceID];

		unity_ObjectToWorld = 0.0;
		unity_ObjectToWorld._m03_m13_m23_m33 = float4(position.xyz, 1.0);
		unity_ObjectToWorld._m00_m11_m22 = 1;
		unity_ObjectToWorld._m11 = position.w;
	#endif
}

void ShaderGraphDummy_float (in float3 In, out float3 Out) {
	Out = In;
}

void ShaderGraphDummy_half (in half3 In, out half3 Out) {
	Out = In;
}