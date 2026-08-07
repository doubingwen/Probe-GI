using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class PRTRelight : ScriptableRendererFeature
{
    class ProbeRelightingPass : ScriptableRenderPass
    {
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer commandBuffer = CommandBufferPool.Get("Probe GI Relighting");
            ProbeVolume[] volumes = Object.FindObjectsByType<ProbeVolume>(FindObjectsSortMode.None);

            foreach (ProbeVolume volume in volumes)
            {
                volume.SwapCoefficientHistory();
                volume.ClearCurrentCoefficients(commandBuffer);
            }

            ProbeVolume compositeVolume = null;
            foreach (ProbeVolume volume in volumes)
            {
                if (volume.HasValidCoefficientBuffers)
                {
                    compositeVolume = volume;
                    break;
                }
            }

            if (compositeVolume != null)
                SetCompositeGlobals(commandBuffer, compositeVolume);

            Probe[] probes = Object.FindObjectsByType<Probe>(FindObjectsSortMode.None);
            foreach (Probe probe in probes)
            {
                if (probe == null)
                    continue;

                probe.TryInitialize();
                probe.Relight(commandBuffer, probe.GetComponentInParent<ProbeVolume>());
            }

            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        }

        static void SetCompositeGlobals(CommandBuffer commandBuffer, ProbeVolume volume)
        {
            Vector3 corner = volume.GetMinimumCorner();
            Vector3Int counts = volume.ProbeCounts;
            commandBuffer.SetGlobalFloat("_coefficientVoxelGridSize", volume.probeSpacing);
            commandBuffer.SetGlobalVector("_coefficientVoxelSize", new Vector4(counts.x, counts.y, counts.z, 0.0f));
            commandBuffer.SetGlobalVector("_coefficientVoxelCorner", new Vector4(corner.x, corner.y, corner.z, 0.0f));
            commandBuffer.SetGlobalBuffer("_coefficientVoxel", volume.coefficientBuffer);
            commandBuffer.SetGlobalBuffer("_lastFrameCoefficientVoxel", volume.previousFrameCoefficientBuffer);
            commandBuffer.SetGlobalFloat("_skyLightIntensity", volume.skyLightIntensity);
            commandBuffer.SetGlobalFloat("_GIIntensity", volume.indirectLightIntensity);
        }
    }

    ProbeRelightingPass relightingPass;

    public override void Create()
    {
        relightingPass = new ProbeRelightingPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (camera == null || camera.GetComponent<ProbeCaptureCameraTag>() != null)
            return;

        renderer.EnqueuePass(relightingPass);
    }
}
