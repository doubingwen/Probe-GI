Shader "DouGI/RadianceFieldComposite"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeRadianceField

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "Assets/Shaders/RadianceFieldSH.hlsl"

            TEXTURE2D_X(_CameraDepthTexture);
            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer2);
            SamplerState sampler_point_clamp;

            StructuredBuffer<int> _RF_FieldCoefficients;
            float _RF_FieldSpacing;
            float4 _RF_FieldOrigin;
            float4 _RF_FieldDimensions;
            float _RF_OutputIntensity;

            float3 ReconstructWorldPosition(float2 screenUv)
            {
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, screenUv);
                return ComputeWorldSpacePosition(screenUv, depth, UNITY_MATRIX_I_VP);
            }

            float4 CompositeRadianceField(Varyings input) : SV_Target
            {
                float2 sourceUv = input.texcoord;
                float2 screenUv = input.positionCS.xy / _ScaledScreenParams.xy;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUv);

                float3 worldPosition = ReconstructWorldPosition(screenUv);
                float3 albedo = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, sampler_point_clamp, screenUv, 0).rgb;
                float3 packedNormal = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, sampler_point_clamp, screenUv, 0).xyz;
                float3 normal = normalize(UnpackNormal(packedNormal));
                float3 indirectLight = SampleRadianceField(
                    worldPosition,
                    albedo,
                    normal,
                    _RF_FieldCoefficients,
                    _RF_FieldSpacing,
                    _RF_FieldOrigin.xyz,
                    (int3)_RF_FieldDimensions.xyz);

                color.rgb += indirectLight * _RF_OutputIntensity;
                return color;
            }
            ENDHLSL
        }
    }
}
