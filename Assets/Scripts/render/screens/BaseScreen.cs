using player;
using render.ui;
using UnityEngine;
using world.items;

namespace render.screens
{
    public class BaseScreen : MonoBehaviour
    {
        private ItemSlot _holdingItemSlot;

        void Awake()
        {
            GameObject obj = Instantiate(Resources.Load<GameObject>("Item"), transform);
            _holdingItemSlot = obj.GetComponent<ItemSlot>();
            _holdingItemSlot.OnClickBehavior = () => { };
        }
        
        private void Update()
        {
            InventoryMenu.DrawHoldingItem();
        }

        public void OpenMenu()
        {
            gameObject.SetActive(true);
        }

        public void CloseMenu()
        {
            gameObject.SetActive(false);
        }
        
        protected void OnEnable()
        {
            if (Player.CurrentScreen != null) Player.CurrentScreen.CloseMenu();
            Player.PauseGame();
            _holdingItemSlot.transform.gameObject.SetActive(true);
            
            InventoryMenu.HoldingItem = ItemStack.EmptyStack();
            InventoryMenu.HoldingItemSlot = _holdingItemSlot;
            InventoryMenu.UpdateHoldingItem();
            Player.CurrentScreen = this;
        }

        protected void OnDisable()
        {
            Player.CurrentScreen = null;
            Player.ResumeGame();
            _holdingItemSlot.transform.gameObject.SetActive(false);
            InventoryMenu.HoldingItemSlot = null;
        }
    }
}