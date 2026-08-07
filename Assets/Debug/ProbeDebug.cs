#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Probe))]
public class ProbeDebug : Editor
{
    public override void OnInspectorGUI() 
    {
        DrawDefaultInspector();

        if(GUILayout.Button("Probe Capture")) 
        {
            Probe probe = (Probe)target;
            probe.CaptureGBufferCubemaps();
        }
    }
}
#endif
