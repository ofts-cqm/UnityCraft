using System;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using world.items;

namespace render.ui
{
    public class InventoryMenu : MonoBehaviour
    {
        public static ItemStack HoldingItem;
        [CanBeNull] public static ItemSlot HoldingItemSlot;
        [CanBeNull] public static TextMeshProUGUI HoveringName;
        [CanBeNull] public static RectTransform HoveringNameObject;
        private static GameObject _preFab;

        public event EventHandler<UpdateInventoryEventArg> OnUpdate;

        private Memory<ItemStack> _inventory;
        private ItemSlot[] _slots;

        public class UpdateInventoryEventArg : EventArgs
        {
            public readonly int Index;
            public readonly ItemStack NewStack;

            public UpdateInventoryEventArg(ItemStack stack, int index)
            {
                Index = index;
                NewStack = stack;
            }
        }
        
        public void InitializeInventory(Memory<ItemStack> stack)
        {
            _inventory = stack;
            _slots = new ItemSlot[stack.Length];
            Span<ItemStack> stackSpan = stack.Span;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = AddSlot(stackSpan[i], i);
        }

        private ItemSlot AddSlot(ItemStack stack, int index)
        {
            if (!_preFab) _preFab = Resources.Load<GameObject>("Item");
            GameObject obj = Instantiate(_preFab, transform);
            RectTransform rt = obj.GetComponent<RectTransform>();
            // ReSharper disable once PossibleLossOfFraction
            rt.position = new Vector2(rt.position.x + index % 9 * 45, rt.position.y - index / 9 * 45);
            ItemSlot slot = obj.GetComponent<ItemSlot>();
            slot.Parent = this;
            slot.Display(stack, index);
            return slot;
        }

        public void UpdateInventory(Memory<ItemStack> stack)
        {
            _inventory = stack;
            Span<ItemStack> stackSpan = stack.Span;
            for (int i = 0; i < _slots.Length; i++) _slots[i].Display(stackSpan[i], i);
            OnUpdate?.Invoke(this, new UpdateInventoryEventArg(ItemStack.EmptyStack(), -1));
        }

        public void SetStack(ItemStack stack, int index)
        {
            _inventory.Span[index] = stack;
            OnUpdate?.Invoke(this, new UpdateInventoryEventArg(stack, index));
        }

        public static void UpdateHoldingItem()
        {
            HoldingItemSlot?.Display(HoldingItem, -1);
        }

        private static readonly Vector2 NameOffset = new(10, 50);
        private static string _currentText = "";
        
        public static void DrawHoldingItem()
        {
            Vector2 position = Mouse.current.position.ReadValue();
            position.y -= 30;
            HoldingItemSlot?.SetPosition(position);
            if (HoveringNameObject != null) HoveringNameObject.anchoredPosition = position + NameOffset;
            
            if (_currentText == ItemSlot.HoveredStack?.Name) return;
            
            if (ItemSlot.HoveredStack == null || ItemSlot.HoveredStack == Items.Air)
            {
                _currentText = "";
                HoveringNameObject?.gameObject.SetActive(false);
                return;
            }

            if (_currentText == "") HoveringNameObject?.gameObject.SetActive(true);
            _currentText = ItemSlot.HoveredStack.Name;
            HoveringName?.SetText(_currentText);
        }
    }
}