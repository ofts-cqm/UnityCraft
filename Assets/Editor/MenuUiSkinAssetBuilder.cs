using System.Collections.Generic;
using render.ui;
using UnityEditor;
using UnityEngine;

namespace UnityCraft.Editor
{
    /// <summary>
    /// Imports the deterministic menu PNGs and updates their runtime skin.
    /// Keeping this builder in the project makes art regeneration repeatable
    /// without hand-editing texture import settings or serialized GUIDs.
    /// </summary>
    public static class MenuUiSkinAssetBuilder
    {
        private const string TextureRoot = "Assets/Textures/ui/menu";
        private const string ResourcePath = "Assets/Resources/MenuUiSkin.asset";

        private static readonly Dictionary<string, Vector4> Borders = new()
        {
            ["panel"] = new Vector4(4, 4, 4, 4),
            ["modal_panel"] = new Vector4(4, 4, 4, 4),
            ["inset"] = new Vector4(4, 4, 4, 4),
            ["row"] = new Vector4(4, 4, 4, 4),
            ["button_normal"] = new Vector4(4, 4, 4, 4),
            ["button_highlighted"] = new Vector4(4, 4, 4, 4),
            ["button_pressed"] = new Vector4(4, 4, 4, 4),
            ["button_selected"] = new Vector4(4, 4, 4, 4),
            ["button_disabled"] = new Vector4(4, 4, 4, 4),
            ["input_normal"] = new Vector4(4, 4, 4, 4),
            ["input_focused"] = new Vector4(4, 4, 4, 4),
            ["slider_track"] = new Vector4(3, 3, 3, 3),
            ["slider_fill"] = new Vector4(3, 3, 3, 3),
            ["slider_handle"] = new Vector4(3, 4, 3, 4),
        };

        [MenuItem("UnityCraft/Rebuild Menu UI Skin")]
        public static void Build()
        {
            foreach ((string name, Vector4 border) in Borders)
                ConfigureSprite($"{TextureRoot}/{name}.png", border);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            MenuUiSkin skin = AssetDatabase.LoadAssetAtPath<MenuUiSkin>(ResourcePath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<MenuUiSkin>();
                AssetDatabase.CreateAsset(skin, ResourcePath);
            }

            skin.panel = Load("panel");
            skin.modalPanel = Load("modal_panel");
            skin.inset = Load("inset");
            skin.row = Load("row");
            skin.buttonNormal = Load("button_normal");
            skin.buttonHighlighted = Load("button_highlighted");
            skin.buttonPressed = Load("button_pressed");
            skin.buttonSelected = Load("button_selected");
            skin.buttonDisabled = Load("button_disabled");
            skin.inputNormal = Load("input_normal");
            skin.inputFocused = Load("input_focused");
            skin.sliderTrack = Load("slider_track");
            skin.sliderFill = Load("slider_fill");
            skin.sliderHandle = Load("slider_handle");
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();

            if (!skin.HasRequiredSprites)
                throw new UnityException("Menu UI skin was built with one or more missing sprites.");
        }

        private static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{TextureRoot}/{name}.png");
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new UnityException($"Menu UI texture is missing: {path}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // Match the Canvas referencePixelsPerUnit so one source pixel is
            // one reference-resolution UI pixel and the 4 px border stays crisp.
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
