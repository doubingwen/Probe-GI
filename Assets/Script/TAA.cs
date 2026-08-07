using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class TAA : ScriptableRendererFeature
{
    [System.Serializable]
    public class Setting
    {
        [Header("Data")]
        [Range(0f, 5f)] public float jitter = 1f;//intensity
        [Range(0f, 1f)] public float blend = 0.05f;//blend

        public RenderPassEvent evt = RenderPassEvent.BeforeRenderingPostProcessing;
    }
    class CustomRenderPass : ScriptableRenderPass
    {
        public RTHandle src;
        public RTHandle temp;
        private RTHandle preRT;//记录上一帧图像
        //标记第一帧信息
        private bool hasHistory = false;
        private const string shaderName = "Hidden/TAA";
        private Material mat;
        //相机抖动
        private Vector2[] HaltonSequence9 = new Vector2[]
        {
            new Vector2(0.5f, 1.0f / 3f),
            new Vector2(0.25f, 2.0f / 3f),
            new Vector2(0.75f, 1.0f / 9f),
            new Vector2(0.125f, 4.0f / 9f),
            new Vector2(0.625f, 7.0f / 9f),
            new Vector2(0.375f, 2.0f / 9f),
            new Vector2(0.875f, 5.0f / 9f),
            new Vector2(0.0625f, 8.0f / 9f),
            new Vector2(0.5625f, 1.0f / 27f),
        };
        private int index = 0;//当前halton序号
        private TAA ft;//当前的renderer feature类
        private Camera curCam;//当前相机


        public CustomRenderPass(TAA f)
        {
            ft = f;
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {   // 会随相机渲染频繁调用
            //创建后处理材质
            if(mat==null)
            {
                Shader shader=Shader.Find(shaderName);
                if(shader==null) return;
                mat=CoreUtils.CreateEngineMaterial(shader);
            }

            //读取相机纹理格式
            RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
            rtDesc.depthBufferBits = 0;
            //创建temp的纹理
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref temp,
                rtDesc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_temp"
            );
            //创建preRT的纹理
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref preRT,
                rtDesc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_preRT_temp"
            );

            curCam = renderingData.cameraData.camera;
            curCam.ResetProjectionMatrix();//设置为unity默认状态
            //给投影矩阵加偏移
            Matrix4x4 pm = curCam.projectionMatrix;
            //将0-1映射到-0.5-0.5。然后归一化到屏幕空间
            Vector2 jitter = new Vector2((HaltonSequence9[index].x - 0.5f) / curCam.pixelWidth, (HaltonSequence9[index].y - 0.5f) / curCam.pixelHeight);
            
            jitter *= ft.setting.jitter;
            //透视投影矩阵m02 m12 m22 分别是x，y，z的位置
            pm.m02 -= jitter.x * 2;
            pm.m12 -= jitter.y * 2;
            curCam.projectionMatrix = pm;
            index = (index + 1) % 9;//循环使用这9个点
        }
        

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            if(mat==null||!renderingData.cameraData.postProcessEnabled) 
            {
                Debug.Log("TAA Return");
                return;
            }
            CommandBuffer cmd=CommandBufferPool.Get(shaderName);

            mat.SetFloat("_Blend", ft.setting.blend);

            //上一帧图像
            if (!hasHistory)
            {
                Blitter.BlitCameraTexture(cmd,src,preRT);
                mat.SetTexture("_PreTex",preRT);
                hasHistory=true;
            }


            Blitter.BlitCameraTexture(cmd,src,temp);

            Blitter.BlitCameraTexture(cmd,temp,src, mat,0);

            Blitter.BlitCameraTexture(cmd,src,preRT);
            mat.SetTexture("_PreTex",preRT);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {   //清空引用，不能Release 因为他引用的是相机的 释放会错误
            //相机每帧可能改变，所有放在每帧。防止使用已被清除的信息
            src=null;
        }

        public void Dispose()
        {
            temp?.Release();
            preRT?.Release();
            preRT=null;
            temp = null;
        }

    }

    CustomRenderPass m_ScriptablePass;
    public Setting setting = new Setting();
    public Material TAAMaterial;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(this);
        m_ScriptablePass.renderPassEvent = setting.evt;
        
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {

        renderer.EnqueuePass(m_ScriptablePass);
    }
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        
        m_ScriptablePass.src=renderer.cameraColorTargetHandle;
    }
    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
    }
}
