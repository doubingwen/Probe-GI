using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public enum ProbeVolumeDebugMode
{
    None = 0,
    ProbeGrid = 1,
    ProbeRadiance = 2
}

[ExecuteAlways]
public class ProbeVolume : MonoBehaviour
{
    const int ShCoefficientCount = 9 * 3;

    public GameObject probePrefab;

    [FormerlySerializedAs("probeSizeX")]
    [Min(1)] public int probeCountX = 8;

    [FormerlySerializedAs("probeSizeY")]
    [Min(1)] public int probeCountY = 4;

    [FormerlySerializedAs("probeSizeZ")]
    [Min(1)] public int probeCountZ = 8;

    [FormerlySerializedAs("probeGridSize")]
    [Min(0.01f)] public float probeSpacing = 2.0f;

    [FormerlySerializedAs("data")]
    public ProbeVolumeData probeData;

    public ComputeBuffer coefficientBuffer;
    public ComputeBuffer previousFrameCoefficientBuffer;

    [Range(0.0f, 50.0f)]
    public float skyLightIntensity = 1.0f;

    [FormerlySerializedAs("GIIntensity")]
    [Range(0.0f, 50.0f)]
    public float indirectLightIntensity = 1.0f;

    public ProbeVolumeDebugMode debugMode = ProbeVolumeDebugMode.ProbeRadiance;

    [FormerlySerializedAs("probes")]
    public GameObject[] probeObjects;

    int[] coefficientClearValues;

    public Vector3Int ProbeCounts => new Vector3Int(probeCountX, probeCountY, probeCountZ);
    public int ProbeCount => probeCountX * probeCountY * probeCountZ;
    public bool HasValidCoefficientBuffers => coefficientBuffer != null && previousFrameCoefficientBuffer != null;

    void OnEnable()
    {
        TryInitializeVolume();
    }

    void Start()
    {
        debugMode = ProbeVolumeDebugMode.ProbeGrid;
    }

    public void TryInitializeVolume()
    {
        bool hasCompleteProbeGrid = probeObjects != null && probeObjects.Length == ProbeCount;
        if (hasCompleteProbeGrid)
        {
            foreach (GameObject probeObject in probeObjects)
            {
                if (probeObject == null)
                {
                    hasCompleteProbeGrid = false;
                    break;
                }
            }
        }

        if (!hasCompleteProbeGrid)
        {
            GenerateProbes();
        }
        else
        {
            for (int probeIndex = 0; probeIndex < probeObjects.Length; probeIndex++)
            {
                Probe probe = probeObjects[probeIndex].GetComponent<Probe>();
                if (probe == null)
                    continue;

                probe.volumeProbeIndex = probeIndex;
                probe.TryInitialize();
            }

            if (!HasValidCoefficientBuffers || coefficientClearValues == null)
                AllocateCoefficientBuffers();
        }

        if (probeData != null && probeObjects != null)
            probeData.TryLoadSurfelData(this);
    }

    void OnDestroy()
    {
        ReleaseCoefficientBuffers();
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawCube(GetMinimumCorner(), Vector3.one);

        if (probeObjects == null)
            return;

        foreach (GameObject probeObject in probeObjects)
        {
            if (probeObject == null)
                continue;

            Probe probe = probeObject.GetComponent<Probe>();
            if (probe != null && debugMode == ProbeVolumeDebugMode.ProbeGrid)
            {
                Vector3 cellSize = Vector3.one * probeSpacing;
                Gizmos.DrawWireCube(probe.transform.position + cellSize * 0.5f, cellSize);
            }

            MeshRenderer meshRenderer = probeObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null && (Application.isPlaying || debugMode == ProbeVolumeDebugMode.None))
                meshRenderer.enabled = false;
        }
    }

    public void GenerateProbes()
    {
        DestroyProbeObjects();
        ReleaseCoefficientBuffers();

        if (probePrefab == null)
        {
            Debug.LogError("Cannot generate probe volume without a probe prefab.", this);
            return;
        }

        probeObjects = new GameObject[ProbeCount];
        for (int x = 0; x < probeCountX; x++)
        {
            for (int y = 0; y < probeCountY; y++)
            {
                for (int z = 0; z < probeCountZ; z++)
                {
                    int probeIndex = ToLinearIndex(x, y, z);
                    Vector3 localPosition = new Vector3(x, y, z) * probeSpacing;
                    GameObject probeObject = Instantiate(probePrefab, transform);
                    probeObject.transform.position = transform.position + localPosition;

                    Probe probe = probeObject.GetComponent<Probe>();
                    if (probe != null)
                    {
                        probe.volumeProbeIndex = probeIndex;
                        probe.TryInitialize();
                    }

                    probeObjects[probeIndex] = probeObject;
                }
            }
        }

        AllocateCoefficientBuffers();
    }

    public void CaptureProbes()
    {
        if (probeObjects == null)
            return;

        foreach (GameObject probeObject in probeObjects)
        {
            MeshRenderer meshRenderer = probeObject != null ? probeObject.GetComponent<MeshRenderer>() : null;
            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }

        foreach (GameObject probeObject in probeObjects)
        {
            Probe probe = probeObject != null ? probeObject.GetComponent<Probe>() : null;
            if (probe != null)
                probe.CaptureGBufferCubemaps();
        }

        if (probeData != null)
            probeData.StoreSurfelData(this);
    }

    public void ClearCurrentCoefficients(CommandBuffer commandBuffer)
    {
        if (coefficientBuffer != null && coefficientClearValues != null)
            commandBuffer.SetBufferData(coefficientBuffer, coefficientClearValues);
    }

    public void SwapCoefficientHistory()
    {
        if (coefficientBuffer != null && previousFrameCoefficientBuffer != null)
            (coefficientBuffer, previousFrameCoefficientBuffer) = (previousFrameCoefficientBuffer, coefficientBuffer);
    }

    public Vector3 GetMinimumCorner()
    {
        return transform.position;
    }

    public void LogCoefficientDiagnostics()
    {
        if (!HasValidCoefficientBuffers)
            TryInitializeVolume();

        if (!HasValidCoefficientBuffers)
        {
            Debug.LogWarning("Probe volume coefficient buffers are not initialized.", this);
            return;
        }

        int[] coefficients = new int[ProbeCount * ShCoefficientCount];
        coefficientBuffer.GetData(coefficients);

        int nonZeroCount = 0;
        long maximumAbsoluteValue = 0;
        foreach (int coefficient in coefficients)
        {
            if (coefficient != 0)
                nonZeroCount++;

            long absoluteValue = System.Math.Abs((long)coefficient);
            if (absoluteValue > maximumAbsoluteValue)
                maximumAbsoluteValue = absoluteValue;
        }

        Debug.Log($"Probe Volume SH: {nonZeroCount}/{coefficients.Length} non-zero coefficients, max encoded value = {maximumAbsoluteValue}.", this);
    }

    int ToLinearIndex(int x, int y, int z)
    {
        return x * probeCountY * probeCountZ + y * probeCountZ + z;
    }

    void AllocateCoefficientBuffers()
    {
        ReleaseCoefficientBuffers();

        int coefficientElementCount = ProbeCount * ShCoefficientCount;
        coefficientClearValues = new int[coefficientElementCount];
        coefficientBuffer = new ComputeBuffer(coefficientElementCount, sizeof(int));
        previousFrameCoefficientBuffer = new ComputeBuffer(coefficientElementCount, sizeof(int));
        coefficientBuffer.SetData(coefficientClearValues);
        previousFrameCoefficientBuffer.SetData(coefficientClearValues);
    }

    void DestroyProbeObjects()
    {
        if (probeObjects == null)
            return;

        foreach (GameObject probeObject in probeObjects)
        {
            if (probeObject != null)
                DestroyImmediate(probeObject);
        }
    }

    void ReleaseCoefficientBuffers()
    {
        coefficientBuffer?.Release();
        previousFrameCoefficientBuffer?.Release();
        coefficientBuffer = null;
        previousFrameCoefficientBuffer = null;
    }
}
