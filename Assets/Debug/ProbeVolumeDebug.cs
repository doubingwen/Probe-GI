#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProbeVolume))]
public class ProbeVolumeDebug : Editor
{
    public override void OnInspectorGUI() 
    {
        DrawDefaultInspector();

        if(GUILayout.Button("Generate Probes")) 
        {
            ProbeVolume probeVolume = (ProbeVolume)target;
            probeVolume.GenerateProbes();
        }

        if(GUILayout.Button("Capture Scene Probes")) 
        {
            ProbeVolume probeVolume = (ProbeVolume)target;
            probeVolume.CaptureProbes();
        }

        if (GUILayout.Button("Log Volume SH"))
        {
            ProbeVolume probeVolume = (ProbeVolume)target;
            probeVolume.LogCoefficientDiagnostics();
        }
    }
}
#endif
