using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace render
{
    public class ScreenshotManager: MonoBehaviour
    {
        [Header("Capture Settings")]
        [Tooltip("The width of the 360 image. Height will automatically be half of this (2:1 aspect ratio).")]
        public int imageWidth = 4096; 
        public string fileName = "360_Capture";

        private Camera _targetCamera;

        void Start()
        {
            _targetCamera = GetComponent<Camera>();
            InputSystem.actions.FindAction("Screenshot").performed += _ => Capture360();
        }

        private void Capture360()
        {
            int imageHeight = imageWidth / 2;

            // 1. Create a temporary RenderTexture formatted as a Cubemap
            RenderTexture cubemapTex = new RenderTexture(imageWidth, imageWidth, 24)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Cube
            };

            // 2. Create a standard temporary RenderTexture for the Equirectangular output
            RenderTexture equirectTex = new RenderTexture(imageWidth, imageWidth, 24);

            // 3. Render the camera's view into the Cubemap texture
            _targetCamera.RenderToCubemap(cubemapTex);

            // 4. Convert the Cubemap texture into Equirectangular format (Mono eye)
            cubemapTex.ConvertToEquirect(equirectTex, Camera.MonoOrStereoscopicEye.Left);

            // 5. Read the pixels out of the active RenderTexture into a Texture2D
            RenderTexture.active = equirectTex;
            Texture2D finalImage = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            finalImage.ReadPixels(new Rect(0, imageHeight, imageWidth, imageHeight), 0, 0);
            finalImage.Apply();

            // 6. Clean up resources to prevent memory leaks
            RenderTexture.active = null;
            Destroy(cubemapTex);
            Destroy(equirectTex);

            // 7. Encode texture to PNG and save to the project directory
            byte[] bytes = finalImage.EncodeToPNG();
            Destroy(finalImage);

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../{fileName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png"));
            File.WriteAllBytes(path, bytes);

            Debug.Log($"📷 360 Equirectangular image saved to: {path}");
        }
    }

}