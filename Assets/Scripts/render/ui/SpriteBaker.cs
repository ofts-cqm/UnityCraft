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

        private const float CameraDistance = 3.0f;
        private const int Resolution = 512;

        private class FakeChunk : IBlockProvider
        {
            public Block GetBlock(Vector3Int position) => Blocks.Air;

            public Block GetBlock(int x, int y, int z) => Blocks.Air;
        }
        
        private static readonly FakeChunk Chunk = new();
        private static readonly Vector3Int ModelPosition = new(-2, -2, -2);
        
        public static void PrepareBaking()
        {
            _spawnedModel = new GameObject("Model")
            {
                transform =
                {
                    position = new Vector3(-2, -2, -2),
                    rotation = Quaternion.identity
                }
            };
            
            _spawnedModel.AddComponent<MeshRenderer>().material = Resources.Load<Material>("VoxelMaterial");
            _modelMesh = _spawnedModel.AddComponent<MeshFilter>();
            
            _camObj = new GameObject("BakeCamera");
            _bakeCam = _camObj.AddComponent<Camera>();
            
            _bakeCam.transform.position = _spawnedModel.transform.position + new Vector3(0, 0, -CameraDistance);
            _bakeCam.transform.LookAt(_spawnedModel.transform.position);
            _bakeCam.clearFlags = CameraClearFlags.SolidColor;
            _bakeCam.backgroundColor = new Color(0, 0, 0, 0); // Completely transparent
            _bakeCam.orthographic = true;
            _bakeCam.orthographicSize = 1.5f; // Adjust based on model size
        }

        public static Sprite BakeToSprite(Block block)
        {
            MeshBuilder meshBuilder = new MeshBuilder();
            block.Render(Chunk, meshBuilder, ModelPosition, ModelPosition);
            
            Mesh renderMesh = new Mesh
            {
                vertices = meshBuilder.Vertices.ToArray(),
                triangles = meshBuilder.Triangles.ToArray(),
                uv = meshBuilder.Uvs.ToArray()
            };
            
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
