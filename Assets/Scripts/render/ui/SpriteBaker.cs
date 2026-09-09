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
        private static RenderTexture _rt;
        private static Mesh _bakeMesh;
        private static readonly MeshBuilder Builder = new();
        
        private static Material _material;
        private static Material _transparentMaterial;

        private const int Resolution = 1024;

        private class FakeChunk : IBlockProvider
        {
            public BlockState GetBlock(Vector3Int position) => Blocks.Air.AsState(position);

            public BlockState GetBlock(int x, int y, int z) => Blocks.Air.AsState(x, y, z);
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
            _bakeMesh = new Mesh { name = "Inventory Sprite Bake Mesh" };
            _bakeMesh.MarkDynamic();
            
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
            
            _rt = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            _bakeCam.targetTexture = _rt;
            RenderTexture.active = _rt;
        }

        public static Sprite BakeToSprite(Block block)
        {
            Builder.Clear();
            if (!block.IsAir) block.Render(block.AsState(Vector3Int.zero), Chunk, Builder, Vector3Int.zero, Vector3.zero);
            
            MeshBuilder.TexturedMeshHolder meshHolder = block.Transparent ? Builder.TransparentMesh : Builder.OpaqueMesh;
            meshHolder.UploadTo(_bakeMesh);
            
            _meshRenderer.sharedMaterial = block.Transparent ? _transparentMaterial : _material;
            return BakeToSprite(_bakeMesh);
        }

        public static Sprite BakeToSprite(Mesh mesh)
        {
            _modelMesh.sharedMesh = mesh;
            _bakeCam.Render();
            
            Texture2D texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            texture.Apply();
            _modelMesh.sharedMesh = null;

            return Sprite.Create(
                texture, 
                new Rect(0, 0, Resolution, Resolution), 
                new Vector2(0.5f, 0.5f), // Pivot in the center
                100f // Pixels Per Unit
            );
        }

        public static void FinalizeBaking()
        {
            RenderTexture.active = null;
            if (_rt != null) RenderTexture.ReleaseTemporary(_rt);

            if (_modelMesh != null) _modelMesh.sharedMesh = null;
            if (_bakeMesh != null) Destroy(_bakeMesh);
            _bakeMesh = null;
            
            Destroy(_camObj);
            Destroy(_spawnedModel);
        }
    }
}
