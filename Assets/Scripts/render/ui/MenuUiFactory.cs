using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace render.ui
{
    public enum MenuPanelStyle
    {
        Panel,
        Modal,
        Inset,
        Row
    }

    public enum MenuTextStyle
    {
        Title,
        Body,
        Secondary,
        Warning,
        Error
    }

    public static class MenuUiFactory
    {
        private static readonly Color PanelColor = new(0.08f, 0.08f, 0.08f, 0.88f);
        private static readonly Color ButtonColor = new(0.34f, 0.34f, 0.34f, 1f);
        private static readonly Color SelectedColor = new(0.48f, 0.48f, 0.48f, 1f);
        private const string SkinResourceName = "MenuUiSkin";
        private static readonly int TerrainTextures = Shader.PropertyToID("_TerrainTextures");
        private static readonly int Atlas = Shader.PropertyToID("_Atlas");
        private static readonly int Slice = Shader.PropertyToID("_Slice");
        private static MenuUiSkin _skin;
        private static bool _reportedMissingSkin;

        private static MenuUiSkin Skin
        {
            get
            {
                if (_skin == null) _skin = Resources.Load<MenuUiSkin>(SkinResourceName);
                if (_skin == null && !_reportedMissingSkin)
                {
                    Debug.LogError("MenuUiSkin resource is missing. Runtime menus will use their plain-color fallback.");
                    _reportedMissingSkin = true;
                }
                return _skin;
            }
        }

        public static string SecondaryTextHex => Skin == null
            ? "C8C8C8"
            : ColorUtility.ToHtmlStringRGB(Skin.secondaryText);

        public static string ErrorTextHex => Skin == null
            ? "FF8888"
            : ColorUtility.ToHtmlStringRGB(Skin.errorText);

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            _ = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static Material CreateAtlasMaterial(int slice)
        {
            Material terrainMaterial = Resources.Load<Material>("VoxelMaterial");
            Shader shader = Shader.Find("UnityCraft/UI/Atlas Tile");
            if (terrainMaterial == null || shader == null) return null;
            Texture atlas = terrainMaterial.GetTexture(TerrainTextures);
            if (atlas == null) return null;

            Material material = new(shader);
            material.SetTexture(Atlas, atlas);
            material.SetFloat(Slice, slice);
            return material;
        }

        public static RawImage CreateAtlasBackground(Transform parent, Material material, Vector2 tiling)
        {
            GameObject gameObject = new("Block Texture Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            gameObject.transform.SetParent(parent, false);
            Stretch(gameObject.GetComponent<RectTransform>());
            RawImage image = gameObject.GetComponent<RawImage>();
            // Unity UI requires Graphic.mainTexture to be a regular 2D texture. The custom
            // material owns the Texture2DArray separately in its _Atlas property.
            image.texture = material == null ? Texture2D.grayTexture : Texture2D.whiteTexture;
            image.material = material;
            image.uvRect = new Rect(0, 0, tiling.x, tiling.y);
            image.raycastTarget = false;
            return image;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Image CreateThemedPanel(Transform parent, string name, MenuPanelStyle style)
        {
            Image image = CreatePanel(parent, name, Color.white);
            MenuUiSkin skin = Skin;
            if (skin == null)
            {
                image.color = style == MenuPanelStyle.Inset || style == MenuPanelStyle.Row
                    ? new Color(0.03f, 0.03f, 0.03f, 0.95f)
                    : PanelColor;
                return image;
            }

            image.sprite = style switch
            {
                MenuPanelStyle.Modal => skin.modalPanel,
                MenuPanelStyle.Inset => skin.inset,
                MenuPanelStyle.Row => skin.row,
                _ => skin.panel
            };
            image.type = Image.Type.Sliced;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            Image image = CreatePanel(parent, name, ButtonColor);
            Button button = image.gameObject.AddComponent<Button>();
            ApplyButtonSkin(button, false);
            if (onClick != null) button.onClick.AddListener(() => onClick());

            TextMeshProUGUI text = CreateText(button.transform, "Label", label, 24);
            Stretch(text.rectTransform, 8, 8, 4, 4);
            ApplyTextStyle(text, MenuTextStyle.Body);
            return button;
        }

        public static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null) return;
            ApplyButtonSkin(button, selected);
        }

        private static void ApplyButtonSkin(Button button, bool selected)
        {
            Image image = button.GetComponent<Image>();
            MenuUiSkin skin = Skin;
            if (skin != null)
            {
                image.color = Color.white;
                image.sprite = selected ? skin.buttonSelected : skin.buttonNormal;
                image.type = Image.Type.Sliced;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = skin.buttonHighlighted,
                    pressedSprite = skin.buttonPressed,
                    selectedSprite = selected ? skin.buttonSelected : skin.buttonHighlighted,
                    disabledSprite = skin.buttonDisabled
                };
                return;
            }

            image.color = Color.white;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? SelectedColor : ButtonColor;
            colors.highlightedColor = new Color(0.48f, 0.48f, 0.48f, 1f);
            colors.pressedColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.75f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        public static TMP_InputField CreateInput(Transform parent, string name, string placeholder)
        {
            Image background = CreatePanel(parent, name, new Color(0.03f, 0.03f, 0.03f, 0.95f));
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            MenuUiSkin skin = Skin;
            if (skin != null)
            {
                background.color = Color.white;
                background.sprite = skin.inputNormal;
                background.type = Image.Type.Sliced;
                input.transition = Selectable.Transition.SpriteSwap;
                input.spriteState = new SpriteState
                {
                    highlightedSprite = skin.inputFocused,
                    pressedSprite = skin.inputFocused,
                    selectedSprite = skin.inputFocused,
                    disabledSprite = skin.buttonDisabled
                };
            }

            GameObject viewportObject = new("Text Area", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(input.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport, 12, 12, 4, 4);

            TextMeshProUGUI value = CreateText(viewport, "Text", string.Empty, 23, TextAlignmentOptions.MidlineLeft);
            Stretch(value.rectTransform);
            value.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI hint = CreateText(viewport, "Placeholder", placeholder, 23, TextAlignmentOptions.MidlineLeft);
            Stretch(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;
            hint.color = new Color(0.65f, 0.65f, 0.65f, 1f);

            input.textViewport = viewport;
            input.textComponent = value;
            input.placeholder = hint;
            input.pointSize = 23;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        public static void ApplySliderSkin(Slider slider, Image track, Image fill, Image handle)
        {
            MenuUiSkin skin = Skin;
            if (skin == null) return;

            SetSlicedSprite(track, skin.sliderTrack);
            SetSlicedSprite(fill, skin.sliderFill);
            SetSlicedSprite(handle, skin.sliderHandle);
            // The handle has a unique narrow silhouette, so interaction states
            // tint that sprite instead of swapping in rectangular button art.
            slider.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.72f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.52f, 0.52f, 0.75f);
            colors.colorMultiplier = 1f;
            slider.colors = colors;
        }

        public static void ApplyTextStyle(TextMeshProUGUI text, MenuTextStyle style)
        {
            MenuUiSkin skin = Skin;
            if (skin == null) return;
            text.color = style switch
            {
                MenuTextStyle.Title => skin.titleText,
                MenuTextStyle.Secondary => skin.secondaryText,
                MenuTextStyle.Warning => skin.warningText,
                MenuTextStyle.Error => skin.errorText,
                _ => skin.bodyText
            };
        }

        private static void SetSlicedSprite(Image image, Sprite sprite)
        {
            image.color = Color.white;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        public static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
