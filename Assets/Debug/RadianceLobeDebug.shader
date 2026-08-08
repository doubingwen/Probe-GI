Shader "DouGI/Debug/RadianceLobe"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalRenderPipeline" "Queue"="Geometry"}
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/RadianceFieldSH.hlsl"

            // 使用定点数存储小数, 因为 compute shader 的 InterlockedAdd 不支持 float
            // array size: 3x9=27
            CBUFFER_START(UnityPerMaterial)
                StructuredBuffer<int> _RF_ProbeCoefficients;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.normal = normalize(o.normal);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 dir = i.normal;

                // decode sh
                
                float3 c[9];
                for(int i=0; i<9; i++)
                {
                    c[i].x = DecodeRadiance(_RF_ProbeCoefficients[i*3+0]);
                    c[i].y = DecodeRadiance(_RF_ProbeCoefficients[i*3+1]);
                    c[i].z = DecodeRadiance(_RF_ProbeCoefficients[i*3+2]);
                }
                
                // decode irradiance
                float3 irradiance = EvaluateDiffuseIrradiance(c, dir);
                float3 Lo = irradiance / PI;

                return float4(Lo, 1.0);
            }
            ENDHLSL
        }
    }
}
