using NUnit.Framework;
using render.ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Editor
{
    public sealed class MenuUiSkinTests
    {
        [Test]
        public void ResourceContainsEveryRequiredPixelSprite()
        {
            MenuUiSkin skin = Resources.Load<MenuUiSkin>("MenuUiSkin");
            Assert.IsNotNull(skin);
            Assert.IsTrue(skin.HasRequiredSprites);

            foreach (Sprite sprite in new[]
                     {
                         skin.panel, skin.modalPanel, skin.inset, skin.row,
                         skin.buttonNormal, skin.buttonHighlighted, skin.buttonPressed,
                         skin.buttonSelected, skin.buttonDisabled, skin.inputNormal,
                         skin.inputFocused, skin.sliderTrack, skin.sliderFill, skin.sliderHandle
                     })
            {
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, sprite.name);
                Assert.AreEqual(100f, sprite.pixelsPerUnit, sprite.name);
                Assert.AreNotEqual(Vector4.zero, sprite.border, sprite.name);
                string path = AssetDatabase.GetAssetPath(sprite);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
            }
        }

        [Test]
        public void FactoryAppliesNineSlicedButtonStatesAndPersistentSelection()
        {
            GameObject root = new("Menu UI Test", typeof(RectTransform));
            try
            {
                Button button = MenuUiFactory.CreateButton(root.transform, "Button", "TEST", null);
                Image image = button.GetComponent<Image>();
                MenuUiSkin skin = Resources.Load<MenuUiSkin>("MenuUiSkin");

                Assert.AreEqual(Selectable.Transition.SpriteSwap, button.transition);
                Assert.AreEqual(Image.Type.Sliced, image.type);
                Assert.AreEqual(skin.buttonNormal, image.sprite);
                Assert.AreEqual(skin.buttonHighlighted, button.spriteState.highlightedSprite);
                Assert.AreEqual(skin.buttonPressed, button.spriteState.pressedSprite);
                Assert.AreEqual(skin.buttonDisabled, button.spriteState.disabledSprite);
                Assert.AreEqual(skin.bodyText, button.GetComponentInChildren<TextMeshProUGUI>().color);

                MenuUiFactory.SetButtonSelected(button, true);
                Assert.AreEqual(skin.buttonSelected, image.sprite);
                MenuUiFactory.SetButtonSelected(button, false);
                Assert.AreEqual(skin.buttonNormal, image.sprite);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FactoryAppliesInputAndSliderSkinWithoutChangingGeometry()
        {
            GameObject root = new("Menu UI Test", typeof(RectTransform));
            try
            {
                MenuUiSkin skin = Resources.Load<MenuUiSkin>("MenuUiSkin");
                TMP_InputField input = MenuUiFactory.CreateInput(root.transform, "Input", "Placeholder");
                Image inputImage = input.GetComponent<Image>();
                Assert.AreEqual(skin.inputNormal, inputImage.sprite);
                Assert.AreEqual(skin.inputFocused, input.spriteState.selectedSprite);

                Image track = MenuUiFactory.CreatePanel(root.transform, "Track", Color.black);
                Slider slider = track.gameObject.AddComponent<Slider>();
                Image fill = MenuUiFactory.CreatePanel(track.transform, "Fill", Color.white);
                Image handle = MenuUiFactory.CreatePanel(track.transform, "Handle", Color.white);
                slider.targetGraphic = handle;
                MenuUiFactory.ApplySliderSkin(slider, track, fill, handle);

                Assert.AreEqual(skin.sliderTrack, track.sprite);
                Assert.AreEqual(skin.sliderFill, fill.sprite);
                Assert.AreEqual(skin.sliderHandle, handle.sprite);
                Assert.AreEqual(Selectable.Transition.ColorTint, slider.transition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
