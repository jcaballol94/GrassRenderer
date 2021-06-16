using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaballolDev{
    [ExecuteAlways]
    public class GrassRenderer : MonoBehaviour
    {
        [Header("Mesh")]
        [SerializeField][HideInInspector] private Mesh m_mesh;
        [SerializeField][HideInInspector] private int m_meshVersion = 0;
        [SerializeField][Min(0.01f)] private float m_width = 0.1f;
        [SerializeField][Min(0.01f)] private Vector2 m_heightRange = new Vector2(0.5f, 1f);

        [Header("Placement")]
        [SerializeField] private Material m_material;
        //[SerializeField] private Terrain m_terrain;
        [SerializeField][Min(0.01f)] private float m_tileSize = 2f;
        [SerializeField][HideInInspector] private Vector2Int m_resolution;
        [SerializeField] ComputeShader m_computeShader;

        [SerializeField] private bool m_castShadows = false;

        #region Property ID
        static readonly int m_positionsId = Shader.PropertyToID("_Positions");
        static readonly int m_terrainSizeId = Shader.PropertyToID("_TerrainSize");
        static readonly int m_terrainPositionId = Shader.PropertyToID("_TerrainPosition");
        static readonly int m_resolutionId = Shader.PropertyToID("_Resolution");
        static readonly int m_frustumId = Shader.PropertyToID("_Frustum");
        static readonly int m_heightRangeId = Shader.PropertyToID("_HeightRange");
        #endregion

        [Header("Temp debug")]
        [SerializeField] private Vector3 m_terrainSize;
        [SerializeField] private Vector3 m_terrainPosition;
        [SerializeField] private bool m_cullMainCamera = false;

        private ComputeBuffer m_argsBuffer;
        private ComputeBuffer m_positionsBuffer;
        private int m_bufferSize;
        private int m_argsVersion;
        private Vector4[] m_planes = new Vector4[6];

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += RenderCamera;

            m_argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

            // if (!m_terrain) return;

            CreateBuffer();
        }

        private void OnDisable()
        {
            ReleaseBuffer();

            if (m_argsBuffer != null) m_argsBuffer.Release();
            m_argsBuffer = null;

            RenderPipelineManager.beginCameraRendering -= RenderCamera;
        }

        private void OnValidate()
        {
            ++m_meshVersion;

            m_heightRange.y = Mathf.Max(m_heightRange.x, m_heightRange.y);

            // if (m_terrain)
            // {
            //     var terrainSize = m_terrain.terrainData.size;
                m_resolution.x = Mathf.CeilToInt(m_terrainSize.x / m_tileSize);
                m_resolution.y = Mathf.CeilToInt(m_terrainSize.z / m_tileSize);
            //}
        }

        private void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            if (m_computeShader == null) return;
            if (!m_mesh) return;
            if (!m_material) return;

            // Ensure that the args buffer matches the mesh we are using
            if (m_argsVersion != m_meshVersion)
                FillArgsBuffer();

            // Ensure that the buffer exists and is big enough
            if (m_positionsBuffer == null || m_resolution.x * m_resolution.y > m_bufferSize)
                CreateBuffer();

            // Setup the data for the compute shader
            m_positionsBuffer.SetCounterValue(0);
            m_computeShader.SetInts(m_resolutionId, m_resolution.x, m_resolution.y);
            m_computeShader.SetFloats(m_terrainSizeId, m_terrainSize.x, m_terrainSize.y, m_terrainSize.z);
            m_computeShader.SetFloats(m_terrainPositionId, m_terrainPosition.x, m_terrainPosition.y, m_terrainPosition.z);
            m_computeShader.SetFloats(m_heightRangeId, m_heightRange.x, m_heightRange.y);
            m_computeShader.SetBuffer(0, m_positionsId, m_positionsBuffer);

            // Setup the culling
            var planes = GeometryUtility.CalculateFrustumPlanes(m_cullMainCamera ? Camera.main : camera);
            for (int i = 0; i < planes.Length; ++i)
            {
                var normal = planes[i].normal.normalized;
                m_planes[i].x = normal.x;
                m_planes[i].y = normal.y;
                m_planes[i].z = normal.z;
                m_planes[i].w = planes[i].distance;
            }
            m_computeShader.SetVectorArray(m_frustumId, m_planes);

            // Dispatch the compute shader
            int xGroups = Mathf.CeilToInt(m_resolution.x / 8f);
            int yGroups = Mathf.CeilToInt(m_resolution.y / 8f);
            m_computeShader.Dispatch(0, xGroups, yGroups, 1);

            // Retrieve the amount of instances to draw
            ComputeBuffer.CopyCount(m_positionsBuffer, m_argsBuffer, sizeof(uint));

            // Render
            var bounds = new Bounds(m_terrainPosition, m_terrainSize);
            m_material.SetBuffer(m_positionsId, m_positionsBuffer);
            Graphics.DrawMeshInstancedIndirect(m_mesh, 0, m_material, bounds, m_argsBuffer, 0,
                null, m_castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, true, gameObject.layer, camera);
        }

        private void CreateBuffer()
        {

            ReleaseBuffer();
            // 3*4 = 3 float of 4 bytes each
            m_bufferSize = m_resolution.x * m_resolution.y;
            m_positionsBuffer = new ComputeBuffer(m_bufferSize, 4*4, ComputeBufferType.Append);
        }

        private void ReleaseBuffer()
        {
            if (m_positionsBuffer != null) m_positionsBuffer.Release();
            m_positionsBuffer = null;
        }

        private void FillArgsBuffer()
        {
            GenerateMesh();
            var args = new uint[] {m_mesh.GetIndexCount(0), 0, m_mesh.GetIndexStart(0), m_mesh.GetBaseVertex(0), 0};
            m_argsBuffer.SetData(args);
            m_argsVersion = m_meshVersion;
        }

        private void GenerateMesh()
        {
            var halfWidth = m_width * 0.5f;
            var positions = new Vector3[]
            {
                new Vector3(-halfWidth, 0f, 0f),
                Vector3.up,
                new Vector3(halfWidth, 0f, 0f)
            };

            var normals = new Vector3[] {Vector3.forward, Vector3.forward, Vector3.forward};
            
            var uv = new Vector2[]
            {
                Vector2.zero,
                new Vector2(0.5f, 1f),
                Vector2.right
            };

            var triangles = new int[] { 0, 1, 2 };

            m_mesh = new Mesh();
            m_mesh.name = name + "_mesh";
            m_mesh.vertices = positions;
            m_mesh.normals = normals;
            m_mesh.uv = uv;
            m_mesh.triangles = triangles;
        }
    }
}