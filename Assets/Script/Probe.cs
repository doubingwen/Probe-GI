using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public struct Surfel
{
    public Vector3 position;
    public Vector3 normal;
    public Vector3 albedo;
    public float skyMask;
}

public enum ProbeDebugMode
{
    None = 0,
    SphereDistribution = 1,
    SampleDirection = 2,
    Surfel = 3,
    SurfelRadiance = 4
}

[ExecuteAlways]
public class Probe : MonoBehaviour
{
    //采样数
    public const int ThreadGroupSizeX = 32;
    public const int ThreadGroupSizeY = 16;
    public const int SurfelCount = ThreadGroupSizeX * ThreadGroupSizeY;

    const int SurfelStride = sizeof(float) * 10;
    //SH值
    const int ShCoefficientCount = 9 * 3;

    //存放材质参数的容器MaterialPropertyBlock
    MaterialPropertyBlock materialPropertyBlock;
    Vector3[] radianceReadbackData;
    int[] shCoefficientClearValues;
    ComputeBuffer fallbackBuffer;

    public Surfel[] surfelReadbackData;
    public ComputeBuffer surfelBuffer;
    public ComputeBuffer surfelRadianceBuffer;
    public ComputeBuffer shCoefficientBuffer;

    //纹理
    [FormerlySerializedAs("RT_WorldPos")]
    public RenderTexture worldPositionCubemap;

    [FormerlySerializedAs("RT_Normal")]
    public RenderTexture normalCubemap;

    [FormerlySerializedAs("RT_Albedo")]
    public RenderTexture albedoCubemap;
    //computer shader
    [FormerlySerializedAs("surfelSampleCS")]
    public ComputeShader surfelSamplingComputeShader;

    [FormerlySerializedAs("surfelReLightCS")]
    public ComputeShader surfelRelightingComputeShader;

    [HideInInspector]
    [FormerlySerializedAs("indexInProbeVolume")]
    public int volumeProbeIndex = -1;

    public ProbeDebugMode debugMode;

    void Start()
    {
        TryInitialize();
    }

    public void TryInitialize()
    {
        surfelBuffer ??= new ComputeBuffer(SurfelCount, SurfelStride);
        shCoefficientClearValues ??= new int[ShCoefficientCount];

        if (shCoefficientBuffer == null)
        {
            shCoefficientBuffer = new ComputeBuffer(ShCoefficientCount, sizeof(int));
            shCoefficientBuffer.SetData(shCoefficientClearValues);
        }

        surfelReadbackData ??= new Surfel[SurfelCount];
        surfelRadianceBuffer ??= new ComputeBuffer(SurfelCount, sizeof(float) * 3);
        radianceReadbackData ??= new Vector3[SurfelCount];
        materialPropertyBlock ??= new MaterialPropertyBlock();
        fallbackBuffer ??= new ComputeBuffer(1, sizeof(int));
    }

    void OnDestroy()
    {
        ReleaseBuffer(ref surfelBuffer);
        ReleaseBuffer(ref shCoefficientBuffer);
        ReleaseBuffer(ref surfelRadianceBuffer);
        ReleaseBuffer(ref fallbackBuffer);
    }

    static void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        buffer?.Release();
        buffer = null;
    }

    void OnDrawGizmos()
    {
        TryInitialize();
        //拿到当前脚本被挂上的 物体
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            return;

        meshRenderer.enabled = !Application.isPlaying;
        Shader debugShader = Shader.Find("GI/SHDebug");
        if (meshRenderer.sharedMaterial != null && debugShader != null)
            meshRenderer.sharedMaterial.shader = debugShader;

        materialPropertyBlock.SetBuffer("_coefficientSH9", shCoefficientBuffer);
        meshRenderer.SetPropertyBlock(materialPropertyBlock);

        if (debugMode == ProbeDebugMode.None)
            return;
        //GPU->CPU数据传输  将surfelBuffer传给surfelReadbackData
        surfelBuffer.GetData(surfelReadbackData);
        surfelRadianceBuffer.GetData(radianceReadbackData);

        Vector3 probePosition = transform.position;
        for (int i = 0; i < SurfelCount; i++)
        {
            Surfel surfel = surfelReadbackData[i];
            Vector3 radiance = radianceReadbackData[i];
            Vector3 direction = (surfel.position - probePosition).normalized;
            bool isSky = surfel.skyMask >= 0.995f;

            Gizmos.color = isSky ? Color.blue : Color.yellow;

            if (debugMode == ProbeDebugMode.SphereDistribution)
                Gizmos.DrawSphere(direction + probePosition, 0.025f);

            if (debugMode == ProbeDebugMode.SampleDirection)
            {
                Gizmos.DrawLine(probePosition, isSky ? probePosition + direction * 25.0f : surfel.position);
                if (!isSky)
                    Gizmos.DrawSphere(surfel.position, 0.05f);
            }

            if (debugMode == ProbeDebugMode.Surfel && !isSky)
            {
                Gizmos.DrawSphere(surfel.position, 0.05f);
                Gizmos.DrawLine(surfel.position, surfel.position + surfel.normal * 0.25f);
            }

            if (debugMode == ProbeDebugMode.SurfelRadiance && !isSky)
            {
                Gizmos.color = new Color(radiance.x, radiance.y, radiance.z);
                Gizmos.DrawSphere(surfel.position, 0.05f);
            }
        }
    }

    //capture suferl 
    public void CaptureGBufferCubemaps()
    {
        TryInitialize();

        if (worldPositionCubemap == null || normalCubemap == null || albedoCubemap == null)
        {
            Debug.LogError("Cannot capture probe cubemaps because one or more render textures are missing.", this);
            return;
        }

        GameObject captureObject = new GameObject("Probe Capture Camera");
        captureObject.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
        Camera captureCamera = captureObject.AddComponent<Camera>();
        captureObject.AddComponent<ProbeCaptureCameraTag>();
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = Color.clear;

        Shader worldPositionShader = Shader.Find("GI/GbufferWorldPos");
        Shader normalShader = Shader.Find("GI/GbufferNormal");
        Shader albedoShader = Shader.Find("GI/GbufferAlbedo");
        if (worldPositionShader == null || normalShader == null || albedoShader == null)
        {
            Debug.LogError("Cannot capture probe cubemaps because one or more capture shaders are missing.", this);
            DestroyImmediate(captureObject);
            return;
        }

        Dictionary<Material, Shader> originalShaders = CollectMaterialShaders();
        try
        {
            ApplyShader(originalShaders.Keys, worldPositionShader);
            captureCamera.RenderToCubemap(worldPositionCubemap);

            ApplyShader(originalShaders.Keys, normalShader);
            captureCamera.RenderToCubemap(normalCubemap);

            ApplyShader(originalShaders.Keys, albedoShader);
            captureCamera.RenderToCubemap(albedoCubemap);

            SampleSurfels(worldPositionCubemap, normalCubemap, albedoCubemap);
        }
        finally
        {
            RestoreShaders(originalShaders);
            DestroyImmediate(captureObject);
        }
    }

    static Dictionary<Material, Shader> CollectMaterialShaders()
    {
        var shaderByMaterial = new Dictionary<Material, Shader>();
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Renderer sceneRenderer in renderers)
        {
            foreach (Material material in sceneRenderer.sharedMaterials)
            {
                if (material != null && !shaderByMaterial.ContainsKey(material))
                    shaderByMaterial.Add(material, material.shader);
            }
        }

        return shaderByMaterial;
    }

    static void ApplyShader(Dictionary<Material, Shader>.KeyCollection materials, Shader shader)
    {
        foreach (Material material in materials)
            material.shader = shader;
    }

    static void RestoreShaders(Dictionary<Material, Shader> originalShaders)
    {
        foreach (KeyValuePair<Material, Shader> entry in originalShaders)
        {
            if (entry.Key != null)
                entry.Key.shader = entry.Value;
        }
    }

    void SampleSurfels(RenderTexture positionTexture, RenderTexture normalTexture, RenderTexture albedoTexture)
    {
        if (surfelSamplingComputeShader == null)
            return;

        int kernelIndex = surfelSamplingComputeShader.FindKernel("CSMain");
        Vector3 probePosition = transform.position;
        surfelSamplingComputeShader.SetVector("_probePos", new Vector4(probePosition.x, probePosition.y, probePosition.z, 1.0f));
        surfelSamplingComputeShader.SetFloat("_randSeed", Random.Range(0.0f, 1.0f));
        surfelSamplingComputeShader.SetTexture(kernelIndex, "_worldPosCubemap", positionTexture);
        surfelSamplingComputeShader.SetTexture(kernelIndex, "_normalCubemap", normalTexture);
        surfelSamplingComputeShader.SetTexture(kernelIndex, "_albedoCubemap", albedoTexture);
        surfelSamplingComputeShader.SetBuffer(kernelIndex, "_surfels", surfelBuffer);
        surfelSamplingComputeShader.Dispatch(kernelIndex, 1, 1, 1);
        surfelBuffer.GetData(surfelReadbackData);

        int validSurfaceCount = 0;
        foreach (Surfel surfel in surfelReadbackData)
        {
            if (surfel.skyMask < 0.5f && surfel.normal.sqrMagnitude > 0.25f)
                validSurfaceCount++;
        }

        if (validSurfaceCount == 0)
            Debug.LogWarning("Probe capture produced no valid surface normals. Indirect lighting will be black.", this);
    }

    public void Relight(CommandBuffer commandBuffer, ProbeVolume volume)
    {
        if (surfelRelightingComputeShader == null)
            return;

        int kernelIndex = surfelRelightingComputeShader.FindKernel("CSMain");
        Vector3 probePosition = transform.position;
        bool hasValidVolumeBuffers = volume != null &&
                                     volume.coefficientBuffer != null &&
                                     volume.previousFrameCoefficientBuffer != null;
        ComputeBuffer currentCoefficients = hasValidVolumeBuffers ? volume.coefficientBuffer : fallbackBuffer;
        ComputeBuffer previousCoefficients = hasValidVolumeBuffers ? volume.previousFrameCoefficientBuffer : fallbackBuffer;
        Vector3Int probeCounts = hasValidVolumeBuffers ? volume.ProbeCounts : Vector3Int.zero;
        float probeSpacing = hasValidVolumeBuffers ? volume.probeSpacing : 1.0f;
        Vector3 volumeCorner = hasValidVolumeBuffers ? volume.GetMinimumCorner() : Vector3.zero;
        float skyIntensity = hasValidVolumeBuffers ? volume.skyLightIntensity : 0.0f;
        float indirectIntensity = hasValidVolumeBuffers ? volume.indirectLightIntensity : 0.0f;
        int probeIndex = hasValidVolumeBuffers ? volumeProbeIndex : -1;

        commandBuffer.SetComputeVectorParam(surfelRelightingComputeShader, "_probePos", new Vector4(probePosition.x, probePosition.y, probePosition.z, 1.0f));
        commandBuffer.SetComputeBufferParam(surfelRelightingComputeShader, kernelIndex, "_surfels", surfelBuffer);
        commandBuffer.SetComputeBufferParam(surfelRelightingComputeShader, kernelIndex, "_coefficientSH9", shCoefficientBuffer);
        commandBuffer.SetComputeBufferParam(surfelRelightingComputeShader, kernelIndex, "_surfelRadiance", surfelRadianceBuffer);
        //将每个的结果要存入voxel的集合中
        commandBuffer.SetComputeBufferParam(surfelRelightingComputeShader, kernelIndex, "_coefficientVoxel", currentCoefficients);
        commandBuffer.SetComputeBufferParam(surfelRelightingComputeShader, kernelIndex, "_lastFrameCoefficientVoxel", previousCoefficients);
        commandBuffer.SetComputeIntParam(surfelRelightingComputeShader, "_indexInProbeVolume", probeIndex);
        
        commandBuffer.SetComputeFloatParam(surfelRelightingComputeShader, "_coefficientVoxelGridSize", probeSpacing);
        commandBuffer.SetComputeVectorParam(surfelRelightingComputeShader, "_coefficientVoxelCorner", volumeCorner);
        commandBuffer.SetComputeVectorParam(surfelRelightingComputeShader, "_coefficientVoxelSize", new Vector4(probeCounts.x, probeCounts.y, probeCounts.z, 0.0f));
        commandBuffer.SetComputeFloatParam(surfelRelightingComputeShader, "_skyLightIntensity", skyIntensity);
        commandBuffer.SetComputeFloatParam(surfelRelightingComputeShader, "_GIIntensity", indirectIntensity);
        commandBuffer.SetBufferData(shCoefficientBuffer, shCoefficientClearValues);
        commandBuffer.DispatchCompute(surfelRelightingComputeShader, kernelIndex, 1, 1, 1);
    }
}
