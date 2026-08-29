using System;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using world.items;

namespace render.ui
{
    public class ItemSlot: MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI _count;
        private Image _sprite;
        private ItemStack _stack;
        private RectTransform _rectTransform;
        private int _index;
        private InputAction _shift;
        [CanBeNull] public InventoryMenu Parent { get; set; }
        [CanBeNull] public Action OnClickBehavior { get; set; }
        
        void Awake()
        {
            _sprite = GetComponent<Image>();
            _count = GetComponentInChildren<TextMeshProUGUI>();
            _rectTransform = GetComponent<RectTransform>();
            _rectTransform.pivot = new Vector2(0, 1);
            _shift = InputSystem.actions.FindAction("Shift");
        }
        
        public void Display(ItemStack stack, int index)
        {
            if (!_sprite) Awake();
            _sprite.sprite = stack.Item.Sprite;
            _stack = stack;
            _index = index;
            _count.SetText(stack.Stack < 2 || stack.Infinite ? "" : stack.Stack.ToString());
        }

        private static readonly Vector2 Offset = new(20, 20);
        public void SetPosition(Vector2 position)
        {
            _rectTransform.anchoredPosition = position + Offset;
        }

        // Detects clicks on the UI element
        public void OnPointerClick(PointerEventData eventData)
        {
            if (OnClickBehavior != null)
            {
                OnClickBehavior();
                return;
            }
            if (_stack.IsEmpty && InventoryMenu.HoldingItem.IsEmpty) return;
            
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                if (_stack.IsEmpty) return;
                InventoryMenu.HoldingItem = _stack.Max();
            } else if (_stack.Infinite)
            {
                if (_stack.CanStack(InventoryMenu.HoldingItem))
                {
                    if (eventData.button == PointerEventData.InputButton.Left) InventoryMenu.HoldingItem.Increase();
                    else InventoryMenu.HoldingItem.Decrease();
                }
                else
                {
                    if (InventoryMenu.HoldingItem.IsEmpty)
                    {
                        InventoryMenu.HoldingItem = _shift.IsPressed() ? _stack.Max() : _stack.Copy();
                    }
                    else InventoryMenu.HoldingItem = ItemStack.EmptyStack();
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (InventoryMenu.HoldingItem.CanStack(_stack))
                {
                    InventoryMenu.HoldingItem = _stack.StackItem(InventoryMenu.HoldingItem);
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
            
            InventoryMenu.UpdateHoldingItem();
            Display(_stack, _index);
            Parent?.SetStack(_stack, _index);
        }
    }
}