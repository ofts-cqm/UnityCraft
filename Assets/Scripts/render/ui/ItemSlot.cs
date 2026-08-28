using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using world.items;

namespace render.ui
{
    public class ItemSlot: MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI _count;
        private Image _sprite;
        private ItemStack _stack;
        private int _index;
        [CanBeNull] public InventoryMenu Parent { get; set; } = null;
        
        void Awake()
        {
            _sprite = GetComponent<Image>();
            _count = GetComponentInChildren<TextMeshProUGUI>();
        }
        
        public void Display(ItemStack stack, int index)
        {
            _sprite.sprite = stack.Item.Sprite;
            _stack = stack;
            _index = index;
            _count.SetText(stack.Stack < 2 || stack.Infinite ? "" : stack.Stack.ToString());
        }

        // Detects clicks on the UI element
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_stack.IsEmpty && InventoryMenu.HoldingItem.IsEmpty) return;
            
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (InventoryMenu.HoldingItem.CanStack(_stack))
                {
                    _stack.StackItem(InventoryMenu.HoldingItem);
                }
                else
                {
                    (_stack, InventoryMenu.HoldingItem) = (InventoryMenu.HoldingItem, _stack);
                    
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (InventoryMenu.HoldingItem.IsEmpty)
                {
                    InventoryMenu.HoldingItem = _stack.Half();
                }
                else if (InventoryMenu.HoldingItem.CanStack(_stack))
                {
                    if (_stack.IsEmpty) _stack.Item = InventoryMenu.HoldingItem.Item;
                    _stack.Increase();
                    InventoryMenu.HoldingItem.Decrease();
                }
                else
                {
                    (_stack, InventoryMenu.HoldingItem) = (InventoryMenu.HoldingItem, _stack);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Middle)
            {
                if (_stack.IsEmpty) return;
                InventoryMenu.HoldingItem = _stack;
            }
            
            InventoryMenu.UpdateHoldingItem();
            Display(_stack, _index);
            Parent?.SetStack(_stack, _index);
        }
    }
}