using System;
using player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using world.items;

namespace render.ui
{
    public class Hotbar : MonoBehaviour
    {
        public RectTransform selection;
        public RectTransform holdingBlockTransform;
        public Image holdingBlockImage;
        public GameObject prefab;
        
        private int SelectedIndex { get; set; }
        private Memory<Item> HotbarItems { get; set; }
        public Item HoldingItem => HotbarItems.Span[SelectedIndex];
        private Image[] _sprites = new Image[9];

        private float _holdingBlockAnimationStartTime;
        private const float HoldingBlockAnimationDuration = 0.3f;

        private void RegisterSprite(int index)
        {
            GameObject go = Instantiate(prefab, transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x + index * 50, rt.anchoredPosition.y);
            _sprites[index] = go.GetComponent<Image>();
        }
        
        public void LoadFromPlayer(Player player)
        {
            HotbarItems = player.inventory.AsMemory(0, 9);
            holdingBlockImage.sprite = HotbarItems.Span[SelectedIndex].Sprite;
        }
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            InputSystem.actions.FindAction("Scroll").performed += context =>
            {
                int delta = (int)context.ReadValue<float>();
                SelectedIndex -= delta;
                if (SelectedIndex >= 9) SelectedIndex = 0;
                if (SelectedIndex < 0) SelectedIndex = 8;
                UpdateHoldingBlock();
            };
            
            for (int i = 0; i < 9; i++)
            {
                int copy = i;
                InputSystem.actions.FindAction((i + 1).ToString()).performed += _ =>
                {
                    SelectedIndex = copy;
                    UpdateHoldingBlock();
                };
                RegisterSprite(i);
            }
            
            RefreshInventory();
        }

        private void UpdateHoldingBlock()
        {
            selection.anchoredPosition = new Vector2(SelectedIndex * 50, selection.anchoredPosition.y);
            holdingBlockImage.sprite = HotbarItems.Span[SelectedIndex].Sprite;
            _holdingBlockAnimationStartTime = Time.time;
        }

        void Update()
        {
            holdingBlockTransform.anchoredPosition = new Vector2(
                500, 
                Mathf.Lerp(-500, -200, (Time.time - _holdingBlockAnimationStartTime) / HoldingBlockAnimationDuration)
            );
        }

        private void RefreshInventory()
        {
            for (int i = 0; i < 9; i++)
            {
                _sprites[i].sprite = HotbarItems.Span[i].Sprite;
            }
        }
    }
}
