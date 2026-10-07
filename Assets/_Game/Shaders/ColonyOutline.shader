Shader "ColonyFlow/Colony Outline"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-1"
        }

        Pass
        {
            Name "ColonyOutlineMask"
            Tags { "LightMode" = "ColonyOutlineMask" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float eyeDepth : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                return half4(1, input.eyeDepth, 0, 0);
            }
            ENDHLSL
        }
    }
}
