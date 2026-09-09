using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using World;
using world.blocks;
using World.blocks;

namespace render.ui
{
    public class SpriteBaker : MonoBehaviour
    {
        private static GameObject _spawnedModel;
        private static MeshFilter _modelMesh;
        private static GameObject _camObj;
        private static Camera _bakeCam;
        private static MeshRenderer _meshRenderer;
        private static RenderTexture _rt;
        private static Mesh _bakeMesh;
        private static readonly MeshBuilder Builder = new();
        
        private static Material _material;
        private static Material _transparentMaterial;

        private const int Resolution = 1024;
        private const int BakeLayer = 31;

        private class FakeChunk : IBlockProvider
        {
            public BlockState GetBlock(Vector3Int position) => Blocks.Air.AsState(position);

            public BlockState GetBlock(int x, int y, int z) => Blocks.Air.AsState(x, y, z);
        }
        
        private static readonly FakeChunk Chunk = new();
        
        public static void PrepareBaking()
        {
            
            _spawnedModel = new GameObject("Inventory Sprite Bake Model")
            {
                layer = BakeLayer,
                transform =
                {
                    position = new Vector3(-0.7f, -0.4f, 2.5f),
                    // -30, 45, 0, XYZ order
                    rotation = new Quaternion(-0.2391176f, 0.3696438f, -0.0990458f, 0.8923991f)
                }
            };

            _meshRenderer = _spawnedModel.AddComponent<MeshRenderer>(); 
            _modelMesh = _spawnedModel.AddComponent<MeshFilter>();
            _bakeMesh = new Mesh { name = "Inventory Sprite Bake Mesh" };
            _bakeMesh.MarkDynamic();
            
            Material voxelMaterial = Resources.Load<Material>("VoxelMaterial");
            Material transparentVoxelMaterial = Resources.Load<Material>("TransparentVoxelMaterial");
            ValidateMaterial(voxelMaterial, "VoxelMaterial");
            ValidateMaterial(transparentVoxelMaterial, "TransparentVoxelMaterial");

            Shader bakeShader = Resources.Load<Shader>("InventorySpriteBake");
            if (bakeShader == null || !bakeShader.isSupported)
                throw new InvalidOperationException("The inventory sprite bake shader is missing or unsupported.");

            _material = CreateBakeMaterial(bakeShader, voxelMaterial, false);
            _transparentMaterial = CreateBakeMaterial(bakeShader, transparentVoxelMaterial, true);

            _camObj = new GameObject("Inventory Sprite Bake Camera")
            {
                transform =
                {
                    position = Vector3.zero,
                    rotation = Quaternion.identity
                }
            };
            _bakeCam = _camObj.AddComponent<Camera>();
            _bakeCam.enabled = false;
            
            _bakeCam.clearFlags = CameraClearFlags.SolidColor;
            _bakeCam.backgroundColor = new Color(0, 0, 0, 0); // Completely transparent
            _bakeCam.orthographic = true;
            _bakeCam.orthographicSize = 1f; // Adjust based on model size
            _bakeCam.cullingMask = 1 << BakeLayer;
            _bakeCam.allowHDR = false;
            _bakeCam.allowMSAA = false;
            _bakeCam.useOcclusionCulling = false;
            
            _rt = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
        }

        public static Sprite BakeToSprite(Block block)
        {
            Builder.Clear();
            if (block.IsAir)
            {
                Texture2D emptyTexture = new(1, 1, TextureFormat.RGBA32, false);
                emptyTexture.SetPixel(0, 0, Color.clear);
                emptyTexture.Apply();
                return Sprite.Create(emptyTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            block.Render(block.AsState(Vector3Int.zero), Chunk, Builder, Vector3Int.zero, Vector3.zero);
            
            MeshBuilder.TexturedMeshHolder meshHolder = block.Transparent ? Builder.TransparentMesh : Builder.OpaqueMesh;
            meshHolder.UploadTo(_bakeMesh);
            
            _meshRenderer.sharedMaterial = block.Transparent ? _transparentMaterial : _material;
            return BakeToSprite(_bakeMesh);
        }

        public static Sprite BakeToSprite(Mesh mesh)
        {
            // The baker mutates and renders the same dynamic mesh immediately. Force every
            // vertex stream (especially UV1, which stores the texture-array slice) to the GPU
            // before submitting the standalone camera render.
            mesh.UploadMeshData(false);
            _modelMesh.sharedMesh = mesh;
            Texture2D texture = new(Resolution, Resolution, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                // Camera.Render does not reliably enter URP's standalone camera path in a Player.
                // A render request is the supported way to render a URP camera outside its normal loop.
                UniversalRenderPipeline.SingleCameraRequest request = new() { destination = _rt };
                if (!RenderPipeline.SupportsRenderRequest(_bakeCam, request))
                    throw new NotSupportedException("The active render pipeline cannot bake inventory sprites.");

                RenderPipeline.SubmitRenderRequest(_bakeCam, request);

                // ReadPixels reads RenderTexture.active, not Camera.targetTexture. URP restores its
                // previous render target when the request finishes, so bind the bake target here.
                RenderTexture.active = _rt;
                texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                texture.Apply();
            }
            catch
            {
                Destroy(texture);
                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
                _modelMesh.sharedMesh = null;
            }

            return Sprite.Create(
                texture, 
                new Rect(0, 0, Resolution, Resolution), 
                new Vector2(0.5f, 0.5f), // Pivot in the center
                100f // Pixels Per Unit
            );
        }

        public static void FinalizeBaking()
        {
            if (_rt != null) RenderTexture.ReleaseTemporary(_rt);
            _rt = null;

            if (_modelMesh != null) _modelMesh.sharedMesh = null;
            if (_bakeMesh != null) Destroy(_bakeMesh);
            _bakeMesh = null;

            if (_material != null) Destroy(_material);
            if (_transparentMaterial != null) Destroy(_transparentMaterial);
            _material = null;
            _transparentMaterial = null;
            
            Destroy(_camObj);
            Destroy(_spawnedModel);
        }

        private static void ValidateMaterial(Material material, string resourceName)
        {
            if (material == null)
                throw new InvalidOperationException($"Missing Resources/{resourceName}.mat.");
            if (material.shader == null || !material.shader.isSupported)
                throw new InvalidOperationException($"The shader used by Resources/{resourceName}.mat is not supported.");
        }

        private static Material CreateBakeMaterial(Shader shader, Material source, bool transparent)
        {
            Texture atlas = source.GetTexture("_TerrainTextures");
            if (atlas == null)
                throw new InvalidOperationException($"{source.name} has no _TerrainTextures texture array.");

            Material material = new(shader)
            {
                name = transparent ? "Transparent Inventory Sprite Bake" : "Opaque Inventory Sprite Bake",
                renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry
            };
            material.SetTexture("_TerrainTextures", atlas);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            return material;
        }
    }
}
