using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Wind : MonoBehaviour
{
    [SerializeField] private float m_speed;
    [SerializeField] private float m_strength;
    [SerializeField] private float m_amplitude;
    [SerializeField][Range(-90f, 90f)] private float m_gustsAngle;
    [SerializeField] private float m_gustsSpeed;
    [SerializeField] private float m_gustsAmplitude;

    private readonly int m_globalWindDirId = Shader.PropertyToID("_GlobalWindDir");
    private readonly int m_globalWindParamsId = Shader.PropertyToID("_GlobalWindParams");
    private readonly int m_globalWindIntDirId = Shader.PropertyToID("_GlobalWindIntDir");
    private readonly int m_globalWindIntParamsId = Shader.PropertyToID("_GlobalWindIntParams");

    private void OnValidate()
    {
        UpdateData();
    }

    private void OnEnable()
    {
        UpdateData();
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            UpdateData();
            transform.hasChanged = false;
        }
    }

    private void UpdateData()
    {
        Shader.SetGlobalVector(m_globalWindDirId, transform.forward);
        Shader.SetGlobalVector(m_globalWindParamsId, new Vector4(m_amplitude, m_speed, m_strength));
        Shader.SetGlobalVector(m_globalWindIntDirId, Quaternion.AngleAxis(m_gustsAngle, Vector3.up) * transform.forward);
        Shader.SetGlobalVector(m_globalWindIntParamsId, new Vector4(m_gustsAmplitude, m_gustsSpeed, 1f));
    }
}
