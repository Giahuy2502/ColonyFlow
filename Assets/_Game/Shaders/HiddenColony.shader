Shader "ColonyFlow/Hidden Colony"
{
    Properties
    {
        [MainTexture] _QuestionTex ("Question Source", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _FaceBounds ("Face Bounds", Vector) = (-0.5, -0.5, 1, 1)
        _TopColor ("Top Color", Color) = (0.451, 0.478, 0.549, 1)
        _SideColor ("Side Color", Color) = (0.302, 0.325, 0.4, 1)
        _MainQuestionColor ("Main Question Color", Color) = (1, 1, 1, 1)
        _SymbolColor ("Small Question Color", Color) = (0.957, 0.937, 0.894, 1)
        _SymbolOutlineColor ("Symbol Outline", Color) = (0.255, 0.275, 0.333, 1)
        _QuestionSourceRect ("Question Source Rect", Vector) = (0.375, 0.325, 0.25, 0.38)
        _QuestionPlacement ("Question Placement", Vector) = (0.3, 0.17, 0.4, 0.66)
        _QuestionThreshold ("Question Threshold", Range(0, 1)) = 0.45
        _QuestionFeather ("Question Feather", Range(0.001, 0.25)) = 0.12
        _QuestionOutlineSize ("Question Outline Size", Range(0, 0.08)) = 0.025
        _SmallQuestionSize ("Small Question Size", Vector) = (0.11, 0.18, 0, 0)
        _SmallQuestionOutlineSize ("Small Question Outline", Range(0, 0.04)) = 0.01
        _SmallQuestionRotationStrength ("Small Question Rotation", Range(0, 2)) = 1
        _SmallQuestionInset ("Small Question Edge Inset", Range(0, 0.2)) = 0.08
        _SmallQuestionThreshold ("Small Question Threshold", Range(0, 1)) = 0.45
        _Ambient ("Ambient", Range(0, 1)) = 0.72
        _LightStrength ("Main Light Strength", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HiddenColonyVertex
            #pragma fragment HiddenColonyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_QuestionTex);
            SAMPLER(sampler_QuestionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _FaceBounds;
                half4 _TopColor;
                half4 _SideColor;
                half4 _MainQuestionColor;
                half4 _SymbolColor;
                half4 _SymbolOutlineColor;
                float4 _QuestionSourceRect;
                float4 _QuestionPlacement;
                half _QuestionThreshold;
                half _QuestionFeather;
                half _QuestionOutlineSize;
                float4 _SmallQuestionSize;
                half _SmallQuestionOutlineSize;
                half _SmallQuestionRotationStrength;
                half _SmallQuestionInset;
                half _SmallQuestionThreshold;
                half _Ambient;
                half _LightStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                half3 normalOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HiddenColonyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = normalize(input.normalOS);
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half QuestionMask(float2 faceUv, float2 offset)
            {
                float2 placementUv = (faceUv - _QuestionPlacement.xy) /
                    _QuestionPlacement.zw + offset;
                half inside = step(0.0, placementUv.x) * step(placementUv.x, 1.0) *
                    step(0.0, placementUv.y) * step(placementUv.y, 1.0);
                float2 sourceUv = _QuestionSourceRect.xy +
                    placementUv * _QuestionSourceRect.zw;
                half3 source = SAMPLE_TEXTURE2D(
                    _QuestionTex, sampler_QuestionTex, sourceUv).rgb;
                half luminance = dot(source, half3(0.299, 0.587, 0.114));
                return inside * smoothstep(
                    _QuestionThreshold,
                    _QuestionThreshold + _QuestionFeather,
                    luminance);
            }

            half QuestionMaskAt(
                float2 faceUv,
                float2 center,
                float2 size,
                half rotation)
            {
                half sine;
                half cosine;
                sincos(rotation * _SmallQuestionRotationStrength, sine, cosine);
                // Clamp the entire rotated glyph, not just its center, inside the top face.
                float2 margin = min(0.49, _SmallQuestionInset + 0.5 * size * (abs(sine) + abs(cosine)));
                center = clamp(center, margin, 1.0 - margin);
                float2 placementUv = (faceUv - center) / size;
                placementUv = float2(
                    cosine * placementUv.x + sine * placementUv.y,
                    -sine * placementUv.x + cosine * placementUv.y) + 0.5;
                half inside = step(0.0, placementUv.x) * step(placementUv.x, 1.0) *
                    step(0.0, placementUv.y) * step(placementUv.y, 1.0);
                float2 sourceUv = _QuestionSourceRect.xy +
                    placementUv * _QuestionSourceRect.zw;
                half3 source = SAMPLE_TEXTURE2D(
                    _QuestionTex, sampler_QuestionTex, sourceUv).rgb;
                half luminance = dot(source, half3(0.299, 0.587, 0.114));
                return inside * smoothstep(
                    _SmallQuestionThreshold,
                    _SmallQuestionThreshold + _QuestionFeather,
                    luminance);
            }

            half SmallQuestionMask(float2 faceUv, half expansion)
            {
                float2 baseSize = _SmallQuestionSize.xy;
                // Size is a bounding box; preserve the source glyph's aspect instead
                // of stretching a tall question mark into a wide smear.
                float sourceAspect = _QuestionSourceRect.z / max(_QuestionSourceRect.w, 0.00001);
                baseSize = float2(min(baseSize.x, baseSize.y * sourceAspect),
                    min(baseSize.y, baseSize.x / max(sourceAspect, 0.00001)));
                half mask = QuestionMaskAt(
                    faceUv, float2(0.16, 0.76),
                    baseSize * float2(0.88, 0.9) + expansion * 2.0, -0.38h);
                mask = max(mask, QuestionMaskAt(
                    faceUv, float2(0.39, 0.9),
                    baseSize * float2(1.05, 1.08) + expansion * 2.0, 0.26h));
                mask = max(mask, QuestionMaskAt(
                    faceUv, float2(0.66, 0.87),
                    baseSize * float2(0.82, 0.86) + expansion * 2.0, -0.17h));
                mask = max(mask, QuestionMaskAt(
                    faceUv, float2(0.85, 0.71),
                    baseSize * float2(1.1, 1.04) + expansion * 2.0, 0.49h));
                mask = max(mask, QuestionMaskAt(
                    faceUv, float2(0.88, 0.34),
                    baseSize * float2(0.9, 0.94) + expansion * 2.0, -0.31h));
                mask = max(mask, QuestionMaskAt(
                    faceUv, float2(0.74, 0.13),
                    baseSize * float2(1.04, 1.1) + expansion * 2.0, 0.21h));
                mask = max(mask, QuestionMaskAt(
                    faceUv, float2(0.3, 0.11),
                    baseSize * float2(0.84, 0.82) + expansion * 2.0, -0.52h));
                return max(mask, QuestionMaskAt(
                    faceUv, float2(0.12, 0.31),
                    baseSize * float2(1.08, 1.0) + expansion * 2.0, 0.35h));
            }

            half4 HiddenColonyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalOS = normalize(input.normalOS);
                half3 normalWS = normalize(input.normalWS);
                half topBlend = smoothstep(0.15h, 0.88h, normalOS.y);
                half symbolSurface = smoothstep(0.92h, 0.995h, normalOS.y);
                // Edge decals cover the upper bevel too. A flat-only mask cuts the
                // hooks/dots apart on the new mesh's smoothed vertex normals.
                half smallSymbolSurface = smoothstep(0.35h, 0.65h, normalOS.y);
                float2 faceUv = (input.positionOS.xz - _FaceBounds.xy) /
                    max(_FaceBounds.zw, float2(0.00001, 0.00001));

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half attenuation = mainLight.distanceAttenuation *
                    mainLight.shadowAttenuation;
                half3 lighting = _Ambient.xxx +
                    mainLight.color * (ndotl * attenuation * _LightStrength);

                half3 baseColor = lerp(_SideColor.rgb, _TopColor.rgb, topBlend);
                half3 color = baseColor * saturate(lighting) * _BaseColor.rgb;

                half questionFill = QuestionMask(faceUv, float2(0.0, 0.0));
                half outlineStep = _QuestionOutlineSize;
                half questionExpanded = questionFill;
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(outlineStep, 0.0)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(-outlineStep, 0.0)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(0.0, outlineStep)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(0.0, -outlineStep)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(outlineStep, outlineStep)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(-outlineStep, outlineStep)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(outlineStep, -outlineStep)));
                questionExpanded = max(questionExpanded, QuestionMask(faceUv, float2(-outlineStep, -outlineStep)));

                half smallQuestionFill = SmallQuestionMask(faceUv, 0.0h);
                half smallQuestionOutline = SmallQuestionMask(
                    faceUv, _SmallQuestionOutlineSize);
                half symbolOutline = max(questionExpanded * symbolSurface,
                    smallQuestionOutline * smallSymbolSurface);
                questionFill *= symbolSurface;
                smallQuestionFill *= smallSymbolSurface;
                half3 symbolLighting = lerp(1.0h.xxx, saturate(lighting), 0.35h);

                color = lerp(color, _SymbolOutlineColor.rgb * symbolLighting,
                    symbolOutline);
                color = lerp(color, _SymbolColor.rgb * symbolLighting,
                    smallQuestionFill);
                color = lerp(color, _MainQuestionColor.rgb * symbolLighting,
                    questionFill);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
