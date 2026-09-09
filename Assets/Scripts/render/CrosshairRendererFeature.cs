using player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace render
{
    /// <summary>
    /// Draws the gameplay crosshair after the world has rendered, before Screen Space Overlay UI is composited.
    /// </summary>
    public sealed class CrosshairRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material crosshairMaterial;

        private CrosshairRenderPass _pass;

        public override void Create()
        {
            _pass = new CrosshairRenderPass();
            _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game || camera.GetComponentInParent<Player>() == null) return;

            if (crosshairMaterial == null)
            {
                Debug.LogWarning("Adaptive Crosshair is missing its material and will not render.", this);
                return;
            }

            _pass.Setup(crosshairMaterial);
            renderer.EnqueuePass(_pass);
        }

        private sealed class CrosshairRenderPass : ScriptableRenderPass
        {
            private const string PassName = "Adaptive Crosshair";
            private Material _material;

            public void Setup(Material material)
            {
                _material = material;
                // Sampling the completed camera color requires an intermediate texture.
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    Debug.LogError("Adaptive Crosshair requires an intermediate camera color texture.");
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDescription = renderGraph.GetTextureDesc(source);
                destinationDescription.name = "CameraColor-AdaptiveCrosshair";
                destinationDescription.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescription);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, _material, 0);
                renderGraph.AddBlitPass(parameters, PassName);

                // Subsequent URP work, including the final blit, consumes the crosshair-enhanced color buffer.
                resourceData.cameraColor = destination;
            }
        }
    }
}
