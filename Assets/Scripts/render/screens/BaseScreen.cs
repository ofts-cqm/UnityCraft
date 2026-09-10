using player;
using render.ui;
using TMPro;
using UnityEngine;
using world.items;

namespace render.screens
{
    public class BaseScreen : MonoBehaviour
    {
        private ItemSlot _holdingItemSlot;
        private GameObject _hoveringName;

        void Awake()
        {
            GameObject obj = Instantiate(Resources.Load<GameObject>("Item"), transform);
            _holdingItemSlot = obj.GetComponent<ItemSlot>();
            _holdingItemSlot.OnClickBehavior = () => { };
            _hoveringName = Instantiate(Resources.Load<GameObject>("HoveringName"), transform);
            _hoveringName.SetActive(false);
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
            InventoryMenu.HoveringName = _hoveringName.GetComponentInChildren<TextMeshProUGUI>();
            InventoryMenu.HoveringNameObject = _hoveringName.GetComponent<RectTransform>();
            InventoryMenu.UpdateHoldingItem();
            Player.CurrentScreen = this;
        }

        protected void OnDisable()
        {
            if (Player.CurrentScreen == this) Player.CurrentScreen = null;
            if (GameplayMenuController.Instance == null || !GameplayMenuController.Instance.PauseVisible)
                Player.ResumeGame();
            _holdingItemSlot.transform.gameObject.SetActive(false);
            InventoryMenu.HoldingItemSlot = null;
            InventoryMenu.HoveringName = null;
            InventoryMenu.HoveringNameObject = null;
        }
    }
}
