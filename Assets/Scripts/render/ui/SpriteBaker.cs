using UnityEngine;
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
        
        private static Material _material;
        private static Material _transparentMaterial;

        private const int Resolution = 1024;

        private class FakeChunk : IBlockProvider
        {
            public Block GetBlock(Vector3Int position) => Blocks.Air;

            public Block GetBlock(int x, int y, int z) => Blocks.Air;
        }
        
        private static readonly FakeChunk Chunk = new();
        
        public static void PrepareBaking()
        {
            
            _spawnedModel = new GameObject("Model")
            {
                transform =
                {
                    position = new Vector3(-0.7f, -0.4f, 2.5f),
                    // -30, 45, 0, XYZ order
                    rotation = new Quaternion(-0.2391176f, 0.3696438f, -0.0990458f, 0.8923991f)
                }
            };

            _meshRenderer = _spawnedModel.AddComponent<MeshRenderer>(); 
            _modelMesh = _spawnedModel.AddComponent<MeshFilter>();
            _modelMesh = _spawnedModel.GetComponent<MeshFilter>();
            
            _material = Resources.Load<Material>("VoxelMaterial");
            _transparentMaterial = Resources.Load<Material>("TransparentVoxelMaterial");

            _camObj = new GameObject("BakeCamera")
            {
                transform =
                {
                    position = Vector3.zero,
                    rotation = Quaternion.identity
                }
            };
            _bakeCam = _camObj.AddComponent<Camera>();
            
            _bakeCam.clearFlags = CameraClearFlags.SolidColor;
            _bakeCam.backgroundColor = new Color(0, 0, 0, 0); // Completely transparent
            _bakeCam.orthographic = true;
            _bakeCam.orthographicSize = 1f; // Adjust based on model size
        }

        public static Sprite BakeToSprite(Block block)
        {
            MeshBuilder meshBuilder = new MeshBuilder();
            if (!block.IsAir) block.Render(Chunk, meshBuilder, Vector3Int.zero, Vector3.zero);
            
            Mesh renderMesh = block.Transparent ? new Mesh{
                    vertices = meshBuilder.TransparentVertices.ToArray(),
                    triangles = meshBuilder.TransparentTriangles.ToArray(),
                    uv = meshBuilder.TransparentUvs.ToArray()
            } : 
            new Mesh
            {
                vertices = meshBuilder.Vertices.ToArray(),
                triangles = meshBuilder.Triangles.ToArray(),
                uv = meshBuilder.Uvs.ToArray()
            };
            
            _meshRenderer.material = block.Transparent ? _transparentMaterial : _material;
            
            renderMesh.SetUVs(1, block.Transparent ? meshBuilder.TransparentTextureIndices.ToArray() : meshBuilder.TextureIndices.ToArray());
            renderMesh.RecalculateNormals();
            
            return BakeToSprite(renderMesh);
        }

        public static Sprite BakeToSprite(Mesh mesh)
        {
            _modelMesh.mesh = mesh;
            
            // 1. Render the model directly into a temporary RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            _bakeCam.targetTexture = rt;
            _bakeCam.Render();
        
            // 2. Extract raw pixels from the RenderTexture to a Texture2D
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            texture.Apply();
        
            // 3. Build the runtime Sprite
            Sprite generatedSprite = Sprite.Create(
                texture, 
                new Rect(0, 0, Resolution, Resolution), 
                new Vector2(0.5f, 0.5f), // Pivot in the center
                100f // Pixels Per Unit
            );
        
            // 4. Clean up memory and scene pollution leak immediately
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
        
            return generatedSprite;
        }

        public static void FinalizeBaking()
        {
            Destroy(_camObj);
            Destroy(_spawnedModel);
        }
    }
}
