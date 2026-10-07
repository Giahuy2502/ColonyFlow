Shader "Hidden/ColonyFlow/ColonyOutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Exterior Outline"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_OutlineSceneDepth);
            float4 _OutlineRadiusUV;
            half4 _OutlineColor;
            static const float2 Directions[16] = {
                float2(1, 0), float2(0.9238795, 0.3826834), float2(0.7071068, 0.7071068), float2(0.3826834, 0.9238795),
                float2(0, 1), float2(-0.3826834, 0.9238795), float2(-0.7071068, 0.7071068), float2(-0.9238795, 0.3826834),
                float2(-1, 0), float2(-0.9238795, -0.3826834), float2(-0.7071068, -0.7071068), float2(-0.3826834, -0.9238795),
                float2(0, -1), float2(0.3826834, -0.9238795), float2(0.7071068, -0.7071068), float2(0.9238795, -0.3826834)
            };
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float inside = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
                if (inside >= 1) return 0;
                float rawDepth = SAMPLE_TEXTURE2D_X(_OutlineSceneDepth, sampler_PointClamp, uv).r;
                // LinearEyeDepth alone is not valid for an orthographic camera.
                float sceneDepth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(rawDepth)
                    : LinearEyeDepth(rawDepth, _ZBufferParams);
                float dilation = 0;
                [unroll] for (int i = 0; i < 16; ++i)
                {
                    float2 sampleUV = uv + Directions[i] * _OutlineRadiusUV.xy;
                    float2 mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV).rg;
                    float sourceDepth = mask.g / max(mask.r, 0.0001);
                    if (mask.r > dilation && sourceDepth <= sceneDepth + 0.02) dilation = mask.r;
                }
                return half4(_OutlineColor.rgb, _OutlineColor.a * saturate(dilation - inside));
            }
            ENDHLSL
        }
    }
}
