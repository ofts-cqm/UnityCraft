using lighting;
using UnityEngine;

namespace world.lighting
{
    /// <summary>A scalar emitter for a future torch/lamp prefab. It never allocates a Unity Light.</summary>
    public sealed class VoxelLightSource : MonoBehaviour
    {
        [Range(0, 15)] public int strength = 14;
        private WorldLighting _owner;
        private int _id;
        private Vector3Int _position;
        private int _lastStrength;

        private void Update()
        {
            var current = World.World.Instance?.Lighting;
            if (current != _owner)
            {
                if (_owner != null) _owner.RemoveSource(_id);
                _owner = current;
                _id = 0;
            }
            if (_owner == null) return;
            var position = Vector3Int.FloorToInt(transform.position);
            int value = Mathf.Clamp(strength, 0, 15);
            if (_id == 0) _id = _owner.RegisterSource(position, (byte)value);
            else if (position != _position || value != _lastStrength) _owner.UpdateSource(_id, position, (byte)value);
            _position = position;
            _lastStrength = value;
        }

        private void OnDisable()
        {
            if (_owner != null) _owner.RemoveSource(_id);
            _owner = null;
            _id = 0;
        }
    }
}
