using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Dou.GI
{
    [MovedFrom(true, null, null, "PrtComposite")]
    public sealed class RadianceFieldCompositeFeature : ScriptableRendererFeature
    {
        sealed class CompositeRadianceFieldPass : ScriptableRenderPass
        {
            static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("Dou GI: Composite Radiance Field");

            readonly Material compositeMaterial;
            RTHandle cameraColor;
            RTHandle temporaryColor;

            internal CompositeRadianceFieldPass(Material material)
            {
                compositeMaterial = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            internal void SetCameraColor(RTHandle target)
            {
                cameraColor = target;
            }

            public override void OnCameraSetup(CommandBuffer commandBuffer, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref temporaryColor,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_DouGICompositeColor");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                RadianceFieldVolume volume = RadianceFieldRegistry.PrimaryVolume;
                if (compositeMaterial == null || cameraColor == null || volume == null)
                    return;

                CommandBuffer commandBuffer = CommandBufferPool.Get();
                using (new ProfilingScope(commandBuffer, ProfilingSampler))
                {
                    volume.BindCompositeMaterial(compositeMaterial);
                    Blitter.BlitCameraTexture(commandBuffer, cameraColor, temporaryColor, compositeMaterial, 0);
                    Blitter.BlitCameraTexture(commandBuffer, temporaryColor, cameraColor);
                }

                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }

            public override void OnCameraCleanup(CommandBuffer commandBuffer)
            {
                cameraColor = null;
            }

            internal void Dispose()
            {
                temporaryColor?.Release();
                temporaryColor = null;
                cameraColor = null;
            }
        }

        [FormerlySerializedAs("compositeMaterial")]
        [SerializeField] Material radianceCompositeMaterial;

        CompositeRadianceFieldPass compositePass;

        public override void Create()
        {
            compositePass = new CompositeRadianceFieldPass(radianceCompositeMaterial);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            compositePass.SetCameraColor(renderer.cameraColorTargetHandle);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || renderingData.cameraData.isPreviewCamera || camera.GetComponent<RadianceCaptureCamera>() != null)
                return;

            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            compositePass?.Dispose();
        }
    }
}
