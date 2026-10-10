using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CubeDash
{
    /// <summary>Single spatial pass for a fixed-camera runner; no history or motion-vector ghosting.</summary>
    public sealed class RunnerMotionBlurFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader blurShader = null;
        [SerializeField, Range(0.01f, 0.06f)] private float maximumRadius = 0.035f;
        private Material material;
        private BlurPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = blurShader != null ? CoreUtils.CreateEngineMaterial(blurShader) : null;
            pass = new BlurPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                requiresIntermediateTexture = true };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (material == null || camera.cameraType != CameraType.Game
                || renderingData.cameraData.renderType != CameraRenderType.Base
                || !renderingData.cameraData.postProcessEnabled
                || !camera.TryGetComponent(out RunnerGraphics graphics) || graphics.BlurAmount < 0.001f) return;
            pass.Setup(material, graphics.BlurAmount * maximumRadius);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        private sealed class BlurPass : ScriptableRenderPass
        {
            private Material material;
            private float radius;
            public void Setup(Material value, float amount) { material = value; radius = amount; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var description = graph.GetTextureDesc(source);
                description.name = "Cube Dash peripheral motion blur";
                description.clearBuffer = false;
                var destination = graph.CreateTexture(description);
                // Per-pass properties, rather than mutating a shared material between cameras.
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_BlurRadius", radius);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0)
                    { propertyBlock = properties };
                graph.AddBlitPass(parameters, passName: "Cube Dash speed blur");
                resources.cameraColor = destination;
            }
        }
    }
}
