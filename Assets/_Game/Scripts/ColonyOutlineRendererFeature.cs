using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ColonyFlow
{
    /// <summary>One exterior silhouette for selectable Colony meshes, without internal seams.</summary>
    public sealed class ColonyOutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader compositeShader;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField, Range(1f, 12f), Tooltip("Outline pixels at a render width of 1080. Scales with render resolution.")]
        private float thicknessAt1080 = 6f;
        private Material compositeMaterial;
        private MaskPass maskPass;
        private CompositePass compositePass;

        public override void Create()
        {
            CoreUtils.Destroy(compositeMaterial);
            compositeMaterial = compositeShader != null ? CoreUtils.CreateEngineMaterial(compositeShader) : null;
            maskPass = new MaskPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            compositePass = new CompositePass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
            compositePass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (compositeMaterial == null || camera.cameraType != CameraType.Game || !camera.CompareTag("MainCamera"))
                return;
            compositePass.Setup(compositeMaterial, outlineColor, Mathf.Clamp(thicknessAt1080, 1f, 12f));
            renderer.EnqueuePass(maskPass);
            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(compositeMaterial);
            compositeMaterial = null;
        }

        // Handles belong to this camera's RenderGraph frame, not a previous camera or frame.
        private sealed class OutlineFrame : ContextItem
        {
            public OutlineFrame() { }
            public TextureHandle mask;
            public override void Reset() => mask = TextureHandle.nullHandle;
        }

        private sealed class MaskPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId MaskTag = new ShaderTagId("ColonyOutlineMask");
            private sealed class PassData { public RendererListHandle renderers; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var light = frameData.Get<UniversalLightData>();
                var descriptor = camera.cameraTargetDescriptor;
                var mask = graph.CreateTexture(new TextureDesc(descriptor.width, descriptor.height)
                {
                    name = "Colony Outline Coverage + Eye Depth",
                    format = GraphicsFormat.R16G16_SFloat,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = true,
                    clearColor = Color.clear
                });
                var depth = graph.CreateTexture(new TextureDesc(descriptor.width, descriptor.height)
                {
                    name = "Colony Outline Private Depth",
                    format = GraphicsFormat.D16_UNorm,
                    clearBuffer = true
                });
                frameData.GetOrCreate<OutlineFrame>().mask = mask;
                using (var builder = graph.AddRasterRenderPass<PassData>("Colony Outline Mask", out var data))
                {
                    var drawing = RenderingUtils.CreateDrawingSettings(MaskTag, rendering, camera, light, camera.defaultOpaqueSortFlags);
                    var filtering = new FilteringSettings(RenderQueueRange.opaque, camera.camera.cullingMask);
                    data.renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                    builder.UseRendererList(data.renderers);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) => context.cmd.DrawRendererList(pass.renderers));
                }
            }
        }

        private sealed class CompositePass : ScriptableRenderPass
        {
            private static readonly int MaskId = Shader.PropertyToID("_BlitTexture");
            private static readonly int DepthId = Shader.PropertyToID("_OutlineSceneDepth");
            private static readonly int ScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int RadiusId = Shader.PropertyToID("_OutlineRadiusUV");
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private Material material;
            private Color color;
            private float thickness;

            private sealed class PassData
            {
                public TextureHandle mask, depth;
                public Material material;
                public MaterialPropertyBlock properties;
                public Color color;
                public Vector4 radius;
            }

            public void Setup(Material value, Color tint, float width)
            {
                material = value;
                color = tint;
                thickness = width;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var outline = frameData.GetOrCreate<OutlineFrame>();
                if (!outline.mask.IsValid() || !resources.cameraDepthTexture.IsValid()) return;
                var descriptor = frameData.Get<UniversalCameraData>().cameraTargetDescriptor;
                // Normalized offsets preserve apparent width when URP upscales Mobile's render scale.
                float pixels = thickness * descriptor.width / 1080f;
                using (var builder = graph.AddRasterRenderPass<PassData>("Colony Outline Composite", out var data))
                {
                    data.mask = outline.mask;
                    data.depth = resources.cameraDepthTexture;
                    data.material = material;
                    data.properties = properties;
                    data.color = color;
                    data.radius = new Vector4(pixels / descriptor.width, pixels / descriptor.height, 0, 0);
                    builder.UseTexture(data.mask, AccessFlags.Read);
                    builder.UseTexture(data.depth, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        pass.properties.Clear();
                        pass.properties.SetTexture(MaskId, pass.mask);
                        pass.properties.SetTexture(DepthId, pass.depth);
                        pass.properties.SetVector(ScaleBiasId, new Vector4(1, 1, 0, 0));
                        pass.properties.SetColor(ColorId, pass.color);
                        pass.properties.SetVector(RadiusId, pass.radius);
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1, pass.properties);
                    });
                }
            }
        }
    }
}
