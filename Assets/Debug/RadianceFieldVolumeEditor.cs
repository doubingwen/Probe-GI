#if UNITY_EDITOR
using Dou.GI;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(RadianceFieldVolume))]
public sealed class RadianceFieldVolumeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RadianceFieldVolume volume = (RadianceFieldVolume)target;
        if (GUILayout.Button("Rebuild Probe Grid"))
            volume.RebuildProbeGrid();

        if (GUILayout.Button("Bake Surface Data"))
            volume.BakeSurfaceData();

        if (GUILayout.Button("Log SH Summary"))
            volume.LogCoefficientSummary();
    }
}

public static class RadianceFieldAssetTools
{
    [MenuItem("Tools/Dou GI/Normalize Radiance Field Assets")]
    public static void NormalizeRadianceFieldAssets()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:RadianceFieldBakeData");
        foreach (string assetGuid in assetGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            NormalizeMainAssetName(assetPath);
        }

        NormalizeMainAssetName("Assets/Material/ProbeCaptureAlbedoCube.renderTexture");
        NormalizeMainAssetName("Assets/Material/ProbeCaptureNormalCube.renderTexture");
        NormalizeMainAssetName("Assets/Material/ProbeCaptureWorldPositionCube.renderTexture");
        NormalizeMainAssetName("Assets/RadianceFieldComposite.mat");

        Object[] rendererAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PC_Renderer.asset");
        foreach (Object rendererAsset in rendererAssets)
        {
            if (rendererAsset is RadianceFieldUpdateFeature)
                rendererAsset.name = "Radiance Field Update";
            else if (rendererAsset is RadianceFieldCompositeFeature)
                rendererAsset.name = "Radiance Field Composite";
            else
                continue;

            EditorUtility.SetDirty(rendererAsset);
        }

        AssetDatabase.SaveAssets();
    }

    public static void ValidateSampleSceneBindings()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        int missingScriptCount = 0;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
                missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        }

        RadianceFieldVolume[] volumes = Object.FindObjectsByType<RadianceFieldVolume>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (missingScriptCount != 0)
            throw new System.InvalidOperationException($"SampleScene contains {missingScriptCount} missing script references.");
        if (volumes.Length == 0)
            throw new System.InvalidOperationException("SampleScene does not contain a RadianceFieldVolume.");
        if (volumes[0].Probes.Count == 0)
            throw new System.InvalidOperationException("The radiance field volume did not restore its probe grid.");

        RequireShader("DouGI/Capture/WorldPosition");
        RequireShader("DouGI/Capture/Normal");
        RequireShader("DouGI/Capture/Albedo");
        RequireShader("DouGI/Debug/RadianceLobe");
        RequireShader("DouGI/RadianceFieldComposite");

        ComputeShader captureProgram = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Shaders/CaptureSurfaceSamples.compute");
        ComputeShader integrationProgram = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Shaders/IntegrateProbeRadiance.compute");
        captureProgram.FindKernel("CaptureSurfaceSamples");
        integrationProgram.FindKernel("IntegrateProbeRadiance");

        Debug.Log($"Dou GI validation passed: {volumes.Length} radiance field volume(s), no missing scripts.");
    }

    static void RequireShader(string shaderName)
    {
        if (Shader.Find(shaderName) == null)
            throw new System.InvalidOperationException($"Required shader '{shaderName}' was not found.");
    }

    static void NormalizeMainAssetName(string assetPath)
    {
        Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset == null)
            return;

        asset.name = Path.GetFileNameWithoutExtension(assetPath);
        EditorUtility.SetDirty(asset);
    }
}
#endif
