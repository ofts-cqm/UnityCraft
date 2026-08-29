using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;
using world.items;

namespace render.ui
{
    public class InventoryMenu : MonoBehaviour
    {
        public static ItemStack HoldingItem;
        [CanBeNull] public static ItemSlot HoldingItemSlot;
        private static GameObject _preFab;

        public event EventHandler<UpdateInventoryEventArg> OnUpdate;

        private Memory<ItemStack> _inventory;
        private ItemSlot[] _slots;

        public class UpdateInventoryEventArg : EventArgs
        {
            public readonly int index;
            public readonly ItemStack newStack;

            public UpdateInventoryEventArg(ItemStack stack, int index)
            {
                this.index = index;
                newStack = stack;
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

        public static void DrawHoldingItem()
        {
            HoldingItemSlot?.SetPosition(Mouse.current.position.ReadValue());
        }
    }
}