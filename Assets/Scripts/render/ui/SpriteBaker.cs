using UnityEngine;

namespace render.ui
{
    public class SpriteBaker : MonoBehaviour
    {
        public static Sprite BakeToSprite(GameObject modelPrefab, int resolution = 512, float cameraDistance = 3.0f)
        {
            // 1. Set up an isolated, temporary instance of the 3D model
            GameObject spawnedModel = Instantiate(modelPrefab, new Vector3(0, -100, 0), Quaternion.identity);
        
            // 2. Set up a runtime camera dedicated to shooting the sprite
            GameObject camObj = new GameObject("BakeCamera");
            Camera bakeCam = camObj.AddComponent<Camera>();
        
            // Configure camera for transparency and flat lighting look
            bakeCam.transform.position = spawnedModel.transform.position + new Vector3(0, 0, -cameraDistance);
            bakeCam.transform.LookAt(spawnedModel.transform.position);
            bakeCam.clearFlags = CameraClearFlags.SolidColor;
            bakeCam.backgroundColor = new Color(0, 0, 0, 0); // Completely transparent
            bakeCam.orthographic = true;
            bakeCam.orthographicSize = 1.5f; // Adjust based on model size
        
            // 3. Render the model directly into a temporary RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            bakeCam.targetTexture = rt;
        
            // Force the camera to draw manually right now
            bakeCam.Render();
        
            // 4. Extract raw pixels from the RenderTexture to a Texture2D
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            texture.Apply();
        
            // 5. Build the runtime Sprite
            Sprite generatedSprite = Sprite.Create(
                texture, 
                new Rect(0, 0, resolution, resolution), 
                new Vector2(0.5f, 0.5f), // Pivot in the center
                100f // Pixels Per Unit
            );
        
            // Clean up memory and scene pollution leak immediately
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(camObj);
            Destroy(spawnedModel);
        
            return generatedSprite;
        }
    }
}
