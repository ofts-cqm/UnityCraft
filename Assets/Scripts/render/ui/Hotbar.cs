using System;
using player;
using UnityEngine;
using UnityEngine.InputSystem;
using World.blocks;

namespace render.ui
{
    public class Hotbar : MonoBehaviour
    {
        public RectTransform selection;
        private int SelectedIndex { get; set; }
        private Memory<Block> HotbarItems { get; set; }
        public Block HoldingBlock => HotbarItems.Span[SelectedIndex];

        public void LoadFromPlayer(Player player)
        {
            HotbarItems = player.inventory.AsMemory(0, 9);
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
                selection.anchoredPosition = new Vector2(SelectedIndex * 50, selection.anchoredPosition.y);
            };
        }
    }
}
