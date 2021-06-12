using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaballolDev{
    [ExecuteAlways]
    public class GrassRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh m_mesh;
        [SerializeField] private Material m_material;
        //[SerializeField] private Terrain m_terrain;
        [SerializeField][Min(0.01f)] private float m_tileSize = 2f;
        [SerializeField][HideInInspector] private Vector2Int m_resolution;
        [SerializeField] ComputeShader m_computeShader;

        #region Property ID
        static readonly int m_positionsId = Shader.PropertyToID("_Positions");
        static readonly int m_terrainSizeId = Shader.PropertyToID("_TerrainSize");
        static readonly int m_terrainPositionId = Shader.PropertyToID("_TerrainPosition");
        static readonly int m_resolutionId = Shader.PropertyToID("_Resolution");
        #endregion

        [Header("Temp debug")]
        [SerializeField] private Vector3 m_terrainSize;
        [SerializeField] private Vector3 m_terrainPosition;

        private ComputeBuffer m_positionsBuffer;
        private int m_bufferSize;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += RenderCamera;

            // if (!m_terrain) return;

            CreateBuffer();
        }

        private void OnDisable()
        {
            ReleaseBuffer();

            RenderPipelineManager.beginCameraRendering -= RenderCamera;
        }

        private void OnValidate()
        {
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

            // Ensure that the buffer exists and is big enough
            if (m_positionsBuffer == null || m_resolution.x * m_resolution.y > m_bufferSize)
                CreateBuffer();

            var amount = m_resolution.x * m_resolution.y;
            if (camera == Camera.main)
            {
                m_computeShader.SetInts(m_resolutionId, m_resolution.x / 2, m_resolution.y / 2);
                amount /= 4;
            }
            else
            {
                m_computeShader.SetInts(m_resolutionId, m_resolution.x, m_resolution.y);
            }

            m_computeShader.SetFloats(m_terrainSizeId, m_terrainSize.x, m_terrainSize.y, m_terrainSize.z);
            m_computeShader.SetFloats(m_terrainPositionId, m_terrainPosition.x, m_terrainPosition.y, m_terrainPosition.z);
            m_computeShader.SetBuffer(0, m_positionsId, m_positionsBuffer);

            int xGroups = Mathf.CeilToInt(m_resolution.x / 8f);
            int yGroups = Mathf.CeilToInt(m_resolution.y / 8f);
            m_computeShader.Dispatch(0, xGroups, yGroups, 1);

            var bounds = new Bounds(m_terrainPosition, m_terrainSize);
            m_material.SetBuffer(m_positionsId, m_positionsBuffer);
            Graphics.DrawMeshInstancedProcedural(m_mesh, 0, m_material, bounds, amount,
                null, ShadowCastingMode.On, true, 0, camera);
        }

        private void CreateBuffer()
        {

            ReleaseBuffer();
            // 3*4 = 3 float of 4 bytes each
            m_bufferSize = m_resolution.x * m_resolution.y;
            m_positionsBuffer = new ComputeBuffer(m_bufferSize, 3*4);
        }

        private void ReleaseBuffer()
        {
            if (m_positionsBuffer != null) m_positionsBuffer.Release();
            m_positionsBuffer = null;
        }
    }
}