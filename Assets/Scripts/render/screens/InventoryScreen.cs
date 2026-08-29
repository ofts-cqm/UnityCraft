using System;
using render.ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using world.items;

namespace render.screens
{
    public class InventoryScreen : MonoBehaviour
    {
        private class InventoryTab
        {
            private static InventoryTab _activeTab;
            public static int SiblingIndex;
            public static InventoryScreen Screen;
            
            private readonly Transform _transform;
            private readonly ItemStack[] _itemStacks;
            private readonly string _name;
            private readonly Sprite _active;
            private readonly Sprite _inactive;
            private readonly Image _background;

            public InventoryTab(GameObject gameObject, Sprite activeSprite, Sprite inactiveSprite, Item icon, ItemStack[] items, string name, bool active = false)
            {
                _transform = gameObject.transform.parent;
                _name = name;
                _itemStacks = items;
                _active = activeSprite;
                _inactive = inactiveSprite;
                
                _background = gameObject.transform.parent.GetComponent<Image>();
                ItemSlot image = gameObject.GetComponent<ItemSlot>();
                image.Display(new ItemStack(icon, 1), -1);
                image.OnClickBehavior = Activate;
                
                if (active)
                {
                    _activeTab = this;
                    Screen.titleText.SetText($"<color=#5C5C5C>{_name}</color>");
                    _background.sprite = _active;
                    _transform.SetSiblingIndex(SiblingIndex + 1);
                }
            }

            private void Activate()
            {
                if (_activeTab == this) return;
                _activeTab.Deactivate();
                _activeTab = this;
                
                Screen.titleText.SetText($"<color=#5C5C5C>{_name}</color>");
                _background.sprite = _active;
                _transform.SetSiblingIndex(SiblingIndex + 1);
                Screen.inventoryRenderer.UpdateInventory(_itemStacks.AsMemory(0, 45));
            }

            private void Deactivate()
            {
                _transform.SetSiblingIndex(SiblingIndex - 1);
                _background.sprite = _inactive;
            }
        }
        
        public TextMeshProUGUI titleText;
        public Image inventoryTexture;
        public GameObject natureTab;
        public GameObject buildingTab;

        public Sprite topLeftActive;
        public Sprite topMiddleActive;
        public Sprite topLeftInactive;
        public Sprite topMiddleInactive;
        
        public InventoryMenu inventoryRenderer;
        public InventoryMenu hotbarRenderer;
        public static BaseScreen Instance;
        
        void Start()
        {
            Instance = GetComponent<BaseScreen>();
            InventoryTab.Screen = this;
            InventoryTab.SiblingIndex = inventoryTexture.transform.GetSiblingIndex();
            
            inventoryRenderer.InitializeInventory(Items.NatureBlockList.ToArray().AsMemory());
            hotbarRenderer.InitializeInventory(Hotbar.Instance.HotbarItems);
            hotbarRenderer.OnUpdate += Hotbar.Instance.InventoryUpdateCallBack;
            
            _ = new InventoryTab(natureTab, topLeftActive, topLeftInactive, Items.GrassBlock, Items.NatureBlockList.ToArray(), "Natural Blocks", true);
            _ = new InventoryTab(buildingTab, topMiddleActive, topMiddleInactive, Items.OakLog, Items.BuildingBlockList.ToArray(), "Building Blocks");
        }
    }
}
