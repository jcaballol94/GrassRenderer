struct instance
{
    float4 positions;
    float4 normals;
};

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<instance> _Positions;
#endif

UNITY_DEFINE_INSTANCED_PROP(float3, _Normal);

void ConfigureProcedural () {
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
		float4 position = _Positions[unity_InstanceID].positions;

		unity_ObjectToWorld = 0.0;
		unity_ObjectToWorld._m03_m13_m23_m33 = float4(position.xyz, 1.0);
		unity_ObjectToWorld._m00_m11_m22 = 1;
		unity_ObjectToWorld._m11 = position.w;

        _Normal = _Positions[unity_InstanceID].normals;
	#endif
}

void GetNormal_float(out float3 Normal)
{
    Normal = _Normal;
}

void ShaderGraphDummy_float (in float3 In, out float3 Out) {
	Out = In;
}

void ShaderGraphDummy_half (in half3 In, out half3 Out) {
	Out = In;
}