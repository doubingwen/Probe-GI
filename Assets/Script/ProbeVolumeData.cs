using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
[CreateAssetMenu(fileName = "ProbeVolumeData", menuName = "Scriptable Objects/ProbeVolumeData")]
public class ProbeVolumeData : ScriptableObject
{
    //suferl提前保存，不用每帧都对每个probe进行cubemap采样。复用，物体场景只能是静太，但是光源计算可以是动态
    const int FloatsPerSurfel = 10;

    [SerializeField]
    public Vector3 volumePosition;

    [FormerlySerializedAs("surfelStorageBuffer")]
    [SerializeField]
    public float[] serializedSurfelData;

    public void StoreSurfelData(ProbeVolume volume)
    {
        if (volume == null || volume.probeObjects == null)
            return;

        int requiredFloatCount = volume.ProbeCount * Probe.SurfelCount * FloatsPerSurfel;
        Array.Resize(ref serializedSurfelData, requiredFloatCount);

        int dataIndex = 0;
        foreach (GameObject probeObject in volume.probeObjects)
        {
            Probe probe = probeObject.GetComponent<Probe>();
            foreach (Surfel surfel in probe.surfelReadbackData)
            {
                serializedSurfelData[dataIndex++] = surfel.position.x;
                serializedSurfelData[dataIndex++] = surfel.position.y;
                serializedSurfelData[dataIndex++] = surfel.position.z;
                serializedSurfelData[dataIndex++] = surfel.normal.x;
                serializedSurfelData[dataIndex++] = surfel.normal.y;
                serializedSurfelData[dataIndex++] = surfel.normal.z;
                serializedSurfelData[dataIndex++] = surfel.albedo.x;
                serializedSurfelData[dataIndex++] = surfel.albedo.y;
                serializedSurfelData[dataIndex++] = surfel.albedo.z;
                serializedSurfelData[dataIndex++] = surfel.skyMask;
            }
        }

        volumePosition = volume.transform.position;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    public bool TryLoadSurfelData(ProbeVolume volume)
    {
        if (volume == null || volume.probeObjects == null)
            return false;

        int requiredFloatCount = volume.ProbeCount * Probe.SurfelCount * FloatsPerSurfel;
        bool hasValidData = serializedSurfelData != null && serializedSurfelData.Length == requiredFloatCount;
        bool positionMatches = volume.transform.position == volumePosition;
        if (!hasValidData || !positionMatches)
        {
            Debug.LogWarning("Probe volume data is missing or outdated. Capture the probes again.", this);
            return false;
        }

        int dataIndex = 0;
        foreach (GameObject probeObject in volume.probeObjects)
        {
            Probe probe = probeObject.GetComponent<Probe>();
            for (int i = 0; i < probe.surfelReadbackData.Length; i++)
            {
                Surfel surfel = probe.surfelReadbackData[i];
                surfel.position.x = serializedSurfelData[dataIndex++];
                surfel.position.y = serializedSurfelData[dataIndex++];
                surfel.position.z = serializedSurfelData[dataIndex++];
                surfel.normal.x = serializedSurfelData[dataIndex++];
                surfel.normal.y = serializedSurfelData[dataIndex++];
                surfel.normal.z = serializedSurfelData[dataIndex++];
                surfel.albedo.x = serializedSurfelData[dataIndex++];
                surfel.albedo.y = serializedSurfelData[dataIndex++];
                surfel.albedo.z = serializedSurfelData[dataIndex++];
                surfel.skyMask = serializedSurfelData[dataIndex++];
                probe.surfelReadbackData[i] = surfel;
            }

            probe.surfelBuffer.SetData(probe.surfelReadbackData);
        }

        return true;
    }
}
