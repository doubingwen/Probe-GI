using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
[ExecuteAlways]
public class PrtComposite : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        public Material blitMaterial;

        private RTHandle tempRT;
        private RTHandle blitSrc;

        public void SetTarget(RTHandle source)
        {
            blitSrc = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            //ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
            rtDesc.depthBufferBits = 0;
            
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref tempRT,
                rtDesc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_PRTCompositeTemp"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (blitMaterial == null || blitSrc == null)
                return;

            Camera cam = renderingData.cameraData.camera;
            if (cam == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("PRT Composite");

            ProbeVolume[] volumes = Object.FindObjectsByType<ProbeVolume>(FindObjectsSortMode.None);
            ProbeVolume volume = null;
            foreach (ProbeVolume candidate in volumes)
            {
                if (candidate.HasValidCoefficientBuffers)
                {
                    volume = candidate;
                    break;
                }
            }

            if (volume != null)
            {
                BindVolumeToMaterial(blitMaterial, volume);
                Blitter.BlitCameraTexture(cmd, blitSrc, tempRT, blitMaterial, 0);
                Blitter.BlitCameraTexture(cmd, tempRT, blitSrc);
                
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static void BindVolumeToMaterial(Material material, ProbeVolume volume)
        {
            Vector3 corner = volume.GetMinimumCorner();
            Vector3Int counts = volume.ProbeCounts;
            material.SetFloat("_coefficientVoxelGridSize", volume.probeSpacing);
            material.SetVector("_coefficientVoxelSize", new Vector4(counts.x, counts.y, counts.z, 0.0f));
            material.SetVector("_coefficientVoxelCorner", new Vector4(corner.x, corner.y, corner.z, 0.0f));
            material.SetBuffer("_coefficientVoxel", volume.coefficientBuffer);
            material.SetFloat("_GIIntensity", volume.indirectLightIntensity);
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            blitSrc = null;
        }

        public void Dispose()
        {
            tempRT?.Release();
            tempRT = null;
            blitSrc = null;
        }
    }

    public Material compositeMaterial;
    CustomRenderPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        m_ScriptablePass.blitMaterial = compositeMaterial;
        m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        m_ScriptablePass.SetTarget(renderer.cameraColorTargetHandle);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera cam = renderingData.cameraData.camera;
        if (cam == null)
            return;

        if (renderingData.cameraData.isPreviewCamera)
            return;

        if (cam.GetComponent<ProbeCaptureCameraTag>() != null)
            return;

        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }
}
