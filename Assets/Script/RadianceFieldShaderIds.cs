using UnityEngine;

namespace Dou.GI
{
    internal static class RadianceFieldShaderIds
    {
        internal static readonly int ProbePosition = Shader.PropertyToID("_RF_ProbePosition");
        internal static readonly int CaptureSeed = Shader.PropertyToID("_RF_CaptureSeed");
        internal static readonly int WorldPositionCube = Shader.PropertyToID("_RF_WorldPositionCube");
        internal static readonly int NormalCube = Shader.PropertyToID("_RF_NormalCube");
        internal static readonly int AlbedoCube = Shader.PropertyToID("_RF_AlbedoCube");
        internal static readonly int SurfaceSamples = Shader.PropertyToID("_RF_SurfaceSamples");
        internal static readonly int SurfaceRadiance = Shader.PropertyToID("_RF_SurfaceRadiance");
        internal static readonly int ProbeCoefficients = Shader.PropertyToID("_RF_ProbeCoefficients");
        internal static readonly int FieldCoefficients = Shader.PropertyToID("_RF_FieldCoefficients");
        internal static readonly int HistoryCoefficients = Shader.PropertyToID("_RF_HistoryCoefficients");
        internal static readonly int ProbeGridIndex = Shader.PropertyToID("_RF_ProbeGridIndex");
        internal static readonly int FieldSpacing = Shader.PropertyToID("_RF_FieldSpacing");
        internal static readonly int FieldOrigin = Shader.PropertyToID("_RF_FieldOrigin");
        internal static readonly int FieldDimensions = Shader.PropertyToID("_RF_FieldDimensions");
        internal static readonly int EnvironmentIntensity = Shader.PropertyToID("_RF_EnvironmentIntensity");
        internal static readonly int BounceIntensity = Shader.PropertyToID("_RF_BounceIntensity");
        internal static readonly int OutputIntensity = Shader.PropertyToID("_RF_OutputIntensity");
    }
}
