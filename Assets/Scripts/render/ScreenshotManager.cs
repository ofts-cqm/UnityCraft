using System.IO;
using player;
using render.screens;
using UnityEngine;
using UnityEngine.InputSystem;

namespace render
{
    public class ScreenshotManager: MonoBehaviour
    {
        [Header("Capture Settings")]
        [Tooltip("The width of the 360 image. Height will automatically be half of this (2:1 aspect ratio).")]
        public int imageWidth = 4096; 
        public string fileName = "Screenshot";

        private Camera _targetCamera;

        void Start()
        {
            _targetCamera = GetComponent<Camera>();
        }

        public void Screenshot()
        {
            Keyboard keyboard = Keyboard.current;
            bool shiftPressed = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            string fileNameWithTime = $"{fileName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../{fileNameWithTime}"));

            if (shiftPressed) Capture360(path);
            else ScreenCapture.CaptureScreenshot(path);
            
            Player.PauseGame();
            GameplayMenuController.Dialog.ShowDialog("Screenshot captured", $"Screenshot saved as {fileNameWithTime}", "Open File", () => Application.OpenURL("file://" + path), "Cancel");
        }

        private void Capture360(string path)
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

            File.WriteAllBytes(path, bytes);
        }
    }
}