
Shader "GI/Composite"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

            #include "Assets/Shaders/SH.hlsl"
            TEXTURE2D_X(_CameraDepthTexture);
            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer1);
            TEXTURE2D_X_HALF(_GBuffer2);
            SamplerState my_point_clamp_sampler;

            float _coefficientVoxelGridSize;
            float4 _coefficientVoxelCorner;
            float4 _coefficientVoxelSize;
            StructuredBuffer<int> _coefficientVoxel; 
            StructuredBuffer<int> _lastFrameCoefficientVoxel;

            float _GIIntensity;

            float4 GetFragmentWorldPos(float2 screenPos)
            {
                float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, my_point_clamp_sampler, screenPos);
                return float4(ComputeWorldSpacePosition(screenPos, sceneRawDepth, UNITY_MATRIX_I_VP), 1.0);
            }

            float4 frag (Varyings i) : SV_Target
            {
                float2 sourceUV = i.texcoord;
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUV);

                // decode from gbuffer
                float4 worldPos = GetFragmentWorldPos(screenUV);
                float3 albedo = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, screenUV, 0).xyz;
                float3 packedNormal = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, screenUV, 0).xyz;
                float3 normal = normalize(UnpackNormal(packedNormal));


                float3 gi = SampleSHVoxel(
                    worldPos, 
                    albedo, 
                    normal,
                    _coefficientVoxel,
                    _coefficientVoxelGridSize,
                    _coefficientVoxelCorner,
                    _coefficientVoxelSize
                );
                color.rgb += gi*3.0;
                
                return color;
                
                
            }
            ENDHLSL
        }
    }
}
