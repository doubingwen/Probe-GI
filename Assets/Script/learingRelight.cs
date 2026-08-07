// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;
// [ExecuteAlways]
// public class Composite : ScriptableRendererFeature
// {
//     class CustomRenderPass : ScriptableRenderPass
//     {
//         public Material blitMaterial;

//         private RTHandle tempRT;
//         private RTHandle blitSrc;

//         public void SetTarget(RTHandle source)
//         {
//             blitSrc = source;
//         }

//         public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
//         {
//             RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
//             rtDesc.depthBufferBits = 0;

//             RenderingUtils.ReAllocateHandleIfNeeded(
//                 ref tempRT,
//                 rtDesc,
//                 FilterMode.Bilinear,
//                 TextureWrapMode.Clamp,
//                 name: "_PRTCompositeTemp"
//             );
//         }

//         public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//         {
//             if (blitMaterial == null || blitSrc == null)
//                 return;

//             CommandBuffer cmd = CommandBufferPool.Get("PRT Composite");

//             ProbeVolume[] volumes = GameObject.FindObjectsOfType<ProbeVolume>();
//             ProbeVolume volume = volumes.Length == 0 ? null : volumes[0];

//             if (volume != null)
//             {
//                 Blitter.BlitCameraTexture(cmd, blitSrc, tempRT, blitMaterial, 0);
//                 Blitter.BlitCameraTexture(cmd, tempRT, blitSrc);
//             }

//             context.ExecuteCommandBuffer(cmd);
//             CommandBufferPool.Release(cmd);
//         }

//         public void Dispose()
//         {
//             tempRT?.Release();
//             tempRT = null;
//             blitSrc = null;
//         }
//     }

//     public Material compositeMaterial;
//     CustomRenderPass m_ScriptablePass;

//     public override void Create()
//     {
//         m_ScriptablePass = new CustomRenderPass();
//         m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
//         m_ScriptablePass.blitMaterial = compositeMaterial;
//     }

//     public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
//     {
//         m_ScriptablePass.SetTarget(renderer.cameraColorTargetHandle);
//     }

//     public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//     {
//         renderer.EnqueuePass(m_ScriptablePass);
//     }

//     protected override void Dispose(bool disposing)
//     {
//         m_ScriptablePass?.Dispose();
//     }
// }