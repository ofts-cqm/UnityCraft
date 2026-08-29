using System;
using player;
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
            protected static InventoryTab ActiveTab;
            public static int SiblingIndex;
            public static InventoryScreen Screen;
            
            protected readonly Transform Transform;
            protected readonly ItemStack[] ItemStacks;
            private readonly string _name;
            protected readonly Sprite Active;
            private readonly Sprite _inactive;
            protected readonly Image Background;

            public InventoryTab(GameObject gameObject, Sprite activeSprite, Sprite inactiveSprite, Item icon, ItemStack[] items, string name, bool active = false)
            {
                Transform = gameObject.transform.parent;
                _name = name;
                ItemStacks = items;
                Active = activeSprite;
                _inactive = inactiveSprite;
                
                Background = gameObject.transform.parent.GetComponent<Image>();
                ItemSlot image = gameObject.GetComponent<ItemSlot>();
                image.Display(new ItemStack(icon, 1), -1);
                image.OnClickBehavior = Activate;
                
                if (active)
                {
                    ActiveTab = this;
                    Screen.titleText.SetText($"<color=#5C5C5C>{_name}</color>");
                    Background.sprite = Active;
                    Transform.SetSiblingIndex(SiblingIndex + 1);
                }
            }

            protected virtual void Activate()
            {
                if (ActiveTab == this) return;
                ActiveTab.Deactivate();
                ActiveTab = this;
                
                Screen.titleText.SetText($"<color=#5C5C5C>{_name}</color>");
                Background.sprite = Active;
                Transform.SetSiblingIndex(SiblingIndex + 1);
                Screen.inventoryRenderer.UpdateInventory(ItemStacks.AsMemory(0, 45));
            }

            public virtual void Deactivate()
            {
                Transform.SetSiblingIndex(SiblingIndex - 1);
                Background.sprite = _inactive;
            }
        }

        private class BackpackTab : InventoryTab
        {
            public BackpackTab(GameObject gameObject, Sprite activeSprite, Sprite inactiveSprite) 
                : base(gameObject, activeSprite, inactiveSprite,Items.Barrel, Player.Instance.inventory[9..36], "Natural Blocks")
            {
                
            }
            
            protected override void Activate()
            {
                if (ActiveTab == this) return;
                ActiveTab.Deactivate();
                ActiveTab = this;
                
                Screen.titleText.SetText($"");
                Background.sprite = Active;
                Transform.SetSiblingIndex(SiblingIndex + 1);
                
                Screen.backpackRenderer.gameObject.SetActive(true);
                Screen.backpackRenderer.UpdateInventory(ItemStacks.AsMemory(0, 27));
                Screen.inventoryRenderer.gameObject.SetActive(false);
                Screen.inventoryTexture.sprite = Screen.creativeBackpack;
                Screen.sliderTexture.enabled = false;
            }

            public override void Deactivate()
            {
                Screen.backpackRenderer.gameObject.SetActive(false);
                Screen.inventoryRenderer.gameObject.SetActive(true);
                Screen.inventoryTexture.sprite = Screen.creativeInventory;
                Screen.sliderTexture.enabled = true;
                base.Deactivate();
            }
        }
        
        public TextMeshProUGUI titleText;
        public Image inventoryTexture;
        public Image sliderTexture;
        public GameObject natureTab;
        public GameObject buildingTab;
        public GameObject backpackTab;

        public Sprite topLeftActive;
        public Sprite topMiddleActive;
        public Sprite topLeftInactive;
        public Sprite topMiddleInactive;
        public Sprite bottomRightActive;
        public Sprite bottomRightInactive;
        public Sprite creativeInventory;
        public Sprite creativeBackpack;
        
        public InventoryMenu inventoryRenderer;
        public InventoryMenu hotbarRenderer;
        public InventoryMenu backpackRenderer;
        public static BaseScreen Instance;
        
        void Start()
        {
            Instance = GetComponent<BaseScreen>();
            InventoryTab.Screen = this;
            InventoryTab.SiblingIndex = inventoryTexture.transform.GetSiblingIndex();
            
            inventoryRenderer.InitializeInventory(Items.NatureBlockList.ToArray().AsMemory());
            hotbarRenderer.InitializeInventory(Hotbar.Instance.HotbarItems);
            backpackRenderer.InitializeInventory(Player.Instance.inventory.AsMemory(9, 27));
            hotbarRenderer.OnUpdate += Hotbar.Instance.InventoryUpdateCallBack;
            
            _ = new InventoryTab(natureTab, topLeftActive, topLeftInactive, Items.GrassBlock, Items.NatureBlockList.ToArray(), "Natural Blocks", true);
            _ = new InventoryTab(buildingTab, topMiddleActive, topMiddleInactive, Items.OakLog, Items.BuildingBlockList.ToArray(), "Building Blocks");
            _ = new BackpackTab(backpackTab, bottomRightActive, bottomRightInactive);
        }
    }
}
