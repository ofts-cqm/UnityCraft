using System;
using JetBrains.Annotations;
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
        public Memory<ItemStack> HotbarItems { get; private set; }
        public ItemStack HoldingItem => HotbarItems.Span[SelectedIndex];
        private readonly ItemSlot[] _sprites = new ItemSlot[9];

        private float _holdingBlockAnimationStartTime;
        private const float HoldingBlockAnimationDuration = 0.3f;
        
        public static Hotbar Instance { get; private set; }

        private void RegisterSprite(int index)
        {
            GameObject go = Instantiate(prefab, transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x + index * 50, rt.anchoredPosition.y);
            _sprites[index] = go.GetComponent<ItemSlot>();
        }
        
        public void LoadFromPlayer(Player player)
        {
            HotbarItems = player.inventory.AsMemory(0, 9);
            holdingBlockImage.sprite = HotbarItems.Span[SelectedIndex].Item.Sprite;
            Instance = this;
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
            holdingBlockImage.sprite = HotbarItems.Span[SelectedIndex].Item.Sprite;
            _holdingBlockAnimationStartTime = Time.time;
        }

        void Update()
        {
            holdingBlockTransform.anchoredPosition = new Vector2(
                500, 
                Mathf.Lerp(-500, -200, (Time.time - _holdingBlockAnimationStartTime) / HoldingBlockAnimationDuration)
            );
        }

        public void InventoryUpdateCallBack([CanBeNull] object sender, InventoryMenu.UpdateInventoryEventArg arg)
        {
            if (arg.index == -1) RefreshInventory();
            else _sprites[arg.index].Display(arg.newStack, arg.index);
        }

        private void RefreshInventory()
        {
            for (int i = 0; i < 9; i++) _sprites[i].Display(HotbarItems.Span[i], i);
        }
    }
}
