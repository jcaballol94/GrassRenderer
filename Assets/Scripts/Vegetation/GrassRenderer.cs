using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaballolDev{
    [ExecuteAlways]
    public class GrassRenderer : MonoBehaviour
    {
        [System.Flags]
        public enum TerrainLayer
        {
            LAYER_0 = 0x01,
            LAYER_1 = 0x02,
            LAYER_2 = 0x04,
            LAYER_3 = 0x08
        }

        [Header("Mesh")]
        [SerializeField][HideInInspector] private Mesh m_mesh;
        [SerializeField][HideInInspector] private int m_meshVersion = 0;
        [SerializeField][Min(0.01f)] private float m_width = 0.1f;
        [SerializeField][Min(0.01f)] private Vector2 m_heightRange = new Vector2(0.5f, 0.7f);
        [SerializeField][Min(0)] private int m_subdivisions = 3;

        [Header("Placement")]
        [SerializeField] private Material m_material;
        [SerializeField] private Terrain m_terrain;
        [SerializeField] private TerrainLayer m_terrainLayer = TerrainLayer.LAYER_0;
        [SerializeField] [HideInInspector] private Vector4 m_terrainLayerMask;
        [SerializeField][Min(0.01f)] private float m_tileSize = 0.1f;
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
        static readonly int m_heightMapId = Shader.PropertyToID("_HeightMap");
        static readonly int m_terrainHeightId = Shader.PropertyToID("_TerrainHeight");
        static readonly int m_splatMapId = Shader.PropertyToID("_SplatMap");
        static readonly int m_terrainLayerId = Shader.PropertyToID("_TerrainLayer");
        #endregion

        [Header("Temp debug")]
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

            if (!m_terrain) return;

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

            if (!m_terrain)
            {
                m_terrain = GetComponent<Terrain>();
            }

            if (m_terrain)
            {
                var terrainSize = m_terrain.terrainData.size;
                m_resolution.x = Mathf.CeilToInt(terrainSize.x / m_tileSize);
                m_resolution.y = Mathf.CeilToInt(terrainSize.z / m_tileSize);
            }

            m_terrainLayerMask = new Vector4(
                m_terrainLayer.HasFlag(TerrainLayer.LAYER_0) ? 1f : 0f,
                m_terrainLayer.HasFlag(TerrainLayer.LAYER_1) ? 1f : 0f,
                m_terrainLayer.HasFlag(TerrainLayer.LAYER_2) ? 1f : 0f,
                m_terrainLayer.HasFlag(TerrainLayer.LAYER_3) ? 1f : 0f);
        }

        private void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            if (m_computeShader == null) return;
            if (!m_material) return;
            if (!m_terrain) return;

            // Ensure that the args buffer matches the mesh we are using
            if (m_argsVersion != m_meshVersion)
                FillArgsBuffer();

            // Couldn't generate mesh, abort
            if (!m_mesh) return;

            // Ensure that the buffer exists and is big enough
            if (m_positionsBuffer == null || m_resolution.x * m_resolution.y > m_bufferSize)
                CreateBuffer();

            // Setup the data for the compute shader
            m_positionsBuffer.SetCounterValue(0);
            m_computeShader.SetInts(m_resolutionId, m_resolution.x, m_resolution.y);
            var terrainSize = m_terrain.terrainData.size;
            m_computeShader.SetFloats(m_terrainSizeId, terrainSize.x, terrainSize.y, terrainSize.z);
            var terrainPos = m_terrain.transform.position;
            m_computeShader.SetFloats(m_terrainPositionId, terrainPos.x, terrainPos.y, terrainPos.z);
            m_computeShader.SetFloats(m_heightRangeId, m_heightRange.x, m_heightRange.y);
            m_computeShader.SetBuffer(0, m_positionsId, m_positionsBuffer);
            m_computeShader.SetFloat(m_terrainHeightId, m_terrain.terrainData.size.y);
            m_computeShader.SetTexture(0, m_heightMapId, m_terrain.terrainData.heightmapTexture);
            m_computeShader.SetVector(m_terrainLayerId, m_terrainLayerMask);
            m_computeShader.SetTexture(0, m_splatMapId, m_terrain.terrainData.alphamapTextures[0]);

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
            int xGroups = Mathf.CeilToInt(m_resolution.x / 32f);
            int yGroups = Mathf.CeilToInt(m_resolution.y / 32f);
            m_computeShader.Dispatch(0, xGroups, yGroups, 1);

            // Retrieve the amount of instances to draw
            ComputeBuffer.CopyCount(m_positionsBuffer, m_argsBuffer, sizeof(uint));

            // Render
            var bounds = new Bounds(terrainPos + 0.5f * terrainSize, terrainSize);
            m_material.SetBuffer(m_positionsId, m_positionsBuffer);
            Graphics.DrawMeshInstancedIndirect(m_mesh, 0, m_material, bounds, m_argsBuffer, 0,
                null, m_castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, true, gameObject.layer, camera);
        }

        private void CreateBuffer()
        {
            ReleaseBuffer();
            // 3*4 = 3 float of 4 bytes each
            m_bufferSize = m_resolution.x * m_resolution.y;
            m_positionsBuffer = new ComputeBuffer(m_bufferSize, 4*8, ComputeBufferType.Append);
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

            // There's a quad for every subdivision and a triangle at the top
            var numVertices = 4 * m_subdivisions + 3;
            var numTriangles = 6 * m_subdivisions + 3;

            var heightStep = 1f / (m_subdivisions + 1);

            // Create the buffers
            var positions = new Vector3[numVertices];
            var normals = new Vector3[numVertices];
            var uv = new Vector2[numVertices];
            var triangles = new int[numTriangles];

            // Fill with the quads
            for (int i = 0; i <= m_subdivisions; ++i)
            {
                var height = heightStep * i;
                var width = (1f - height) * halfWidth;
                var vertIdx = i * 2;

                // Positions
                positions[vertIdx] = new Vector3(-width, height, 0f);
                positions[vertIdx + 1] = new Vector3(width, height, 0f);

                // Normals
                normals[vertIdx] = Vector3.up;
                normals[vertIdx + 1] = Vector3.up;

                // UV
                uv[vertIdx] = new Vector2(0f, height);
                uv[vertIdx + 1] = new Vector2(1f, height);

                // If needed, add the tris
                if (i > 0)
                {
                    var triIdx = (i - 1) * 6;
                    triangles[triIdx] = vertIdx - 1;
                    triangles[triIdx + 1] = vertIdx - 2;
                    triangles[triIdx + 2] = vertIdx;

                    triangles[triIdx + 3] = vertIdx - 1;
                    triangles[triIdx + 4] = vertIdx;
                    triangles[triIdx + 5] = vertIdx + 1;
                }
            }

            // Fill the tip vertex
            var lastVertIdx = (m_subdivisions + 1) * 2;
            positions[lastVertIdx] = new Vector3(0f, 1f, 0f);
            normals[lastVertIdx] = Vector3.up;
            uv[lastVertIdx] = new Vector2(0.5f, 1f);
            
            // Fill the last tri
            var lastTriIdx = (m_subdivisions) * 6;
            triangles[lastTriIdx] = lastVertIdx - 1;
            triangles[lastTriIdx + 1] = lastVertIdx - 2;
            triangles[lastTriIdx + 2] = lastVertIdx;

            // Create the unity mesh
            m_mesh = new Mesh();
            m_mesh.name = name + "_mesh";
            m_mesh.vertices = positions;
            m_mesh.normals = normals;
            m_mesh.uv = uv;
            m_mesh.triangles = triangles;
        }
    }
}