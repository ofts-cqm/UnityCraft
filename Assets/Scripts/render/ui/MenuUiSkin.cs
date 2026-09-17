using UnityEngine;

namespace render.ui
{
    /// <summary>
    /// Shared sprite and color contract for menus built entirely at runtime.
    /// A resource asset is used so screen controllers do not duplicate visual
    /// choices and the generated sprites can remain in Assets/Textures/ui.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityCraft/Menu UI Skin", fileName = "MenuUiSkin")]
    public sealed class MenuUiSkin : ScriptableObject
    {
        [Header("Surfaces")]
        public Sprite panel;
        public Sprite modalPanel;
        public Sprite inset;
        public Sprite row;

        [Header("Buttons")]
        public Sprite buttonNormal;
        public Sprite buttonHighlighted;
        public Sprite buttonPressed;
        public Sprite buttonSelected;
        public Sprite buttonDisabled;

        [Header("Inputs")]
        public Sprite inputNormal;
        public Sprite inputFocused;

        [Header("Sliders")]
        public Sprite sliderTrack;
        public Sprite sliderFill;
        public Sprite sliderHandle;

        [Header("Text")]
        public Color titleText = new(0.835f, 0.627f, 0.306f, 1f);
        public Color bodyText = new(0.863f, 0.941f, 0.827f, 1f);
        public Color secondaryText = new(0.659f, 0.827f, 0.773f, 1f);
        public Color warningText = new(1f, 0.78f, 0.30f, 1f);
        public Color errorText = new(1f, 0.42f, 0.36f, 1f);

        public bool HasRequiredSprites => panel != null && modalPanel != null && inset != null && row != null &&
                                          buttonNormal != null && buttonHighlighted != null &&
                                          buttonPressed != null && buttonSelected != null &&
                                          buttonDisabled != null && inputNormal != null &&
                                          inputFocused != null && sliderTrack != null &&
                                          sliderFill != null && sliderHandle != null;
    }
}
