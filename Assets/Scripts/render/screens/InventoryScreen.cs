using UnityEngine;
using UnityEngine.UI;
using world.items;

namespace render.screens
{
    public class InventoryScreen : MonoBehaviour
    {
        private class InventoryTab
        {
            private readonly Transform _transform;
            
            private static InventoryTab _activeTab;

            public InventoryTab(GameObject gameObject, Item block, bool active = false)
            {
                _transform = gameObject.transform;
                if (active) _activeTab = this;
                
                Image image = gameObject.GetComponentInChildren<Image>();
                image.sprite = block.Sprite;
            }

            private void Activate()
            {
                _activeTab.Deactivate();
                _activeTab = this;
            }

            private void Deactivate()
            {
                _activeTab = null;
            }
        }
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
