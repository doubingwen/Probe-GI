#if UNITY_EDITOR
using Dou.GI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RadianceProbe))]
public sealed class RadianceProbeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Bake Surface Samples"))
            ((RadianceProbe)target).BakeSurfaceCache();
    }
}
#endif
