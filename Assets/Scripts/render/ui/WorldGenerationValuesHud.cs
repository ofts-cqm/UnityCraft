using System.Globalization;
using settings;
using TMPro;
using UnityEngine;
using world.generation;

namespace render.ui
{
    public sealed class WorldGenerationValuesHud : MonoBehaviour
    {
        private Transform _player;
        private TextMeshProUGUI _values;
        private bool _hasSample;
        private int _sampleX;
        private int _sampleZ;

        public void Initialize(Transform player)
        {
            _player = player;
            _values = MenuUiFactory.CreateText(transform, "World Generation Values", string.Empty, 20,
                TextAlignmentOptions.TopLeft);
            MenuUiFactory.SetAnchoredRect(_values.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -136), new Vector2(500, -16));
            _values.raycastTarget = false;
            RefreshVisibility();
        }

        private void OnEnable()
        {
            GameSettings.Applied += RefreshVisibility;
            RefreshVisibility();
        }

        private void OnDisable()
        {
            GameSettings.Applied -= RefreshVisibility;
        }

        private void RefreshVisibility()
        {
            if (_values == null) return;
            _values.gameObject.SetActive(GameSettings.ShowWorldGenerationValues);
            _hasSample = false;
        }

        private void Update()
        {
            if (_player == null || _values == null || !GameSettings.ShowWorldGenerationValues) return;

            Vector3 position = _player.position;
            int worldX = Mathf.FloorToInt(position.x);
            int worldZ = Mathf.FloorToInt(position.z);
            if (_hasSample && worldX == _sampleX && worldZ == _sampleZ) return;

            ChunkGenerator.SampleClimate(worldX, worldZ, out ChunkGenerator.BiomeEnum biome,
                out float continentality, out float temperature, out float humidity);
            _values.text = "Biome: " + biome
                + "\nContinentality: " + continentality.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)
                + "\nTemperature: " + temperature.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)
                + "\nHumidity: " + (float.IsNaN(humidity)
                    ? "N/A"
                    : humidity.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture));
            _sampleX = worldX;
            _sampleZ = worldZ;
            _hasSample = true;
        }
    }
}
