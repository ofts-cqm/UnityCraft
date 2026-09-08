using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using player;
using render.ui;

namespace render.screens
{
    public sealed class GameplayMenuController : MonoBehaviour
    {
        public static GameplayMenuController Instance { get; private set; }
        public bool PauseVisible => _pauseOverlay != null && _pauseOverlay.activeSelf;

        private World.World _world;
        private static readonly string[] LoadingTips =
        {
            "Use the hotbar to quickly switch between items.",
            "Explore different biomes to discover new terrain.",
            "Press shift to place vertical slab!",
            "Different to minecraft, blocks can waterlog flowing water!",
            "Use upper slab to constrain water's flowing amount.",
            "Lower slab can only block low-level flowing water.",
            "Did you see the waves?",
            "Use the pause menu to save and return to world selection.",
            "Infinitely generated terrain!",
            "You may see the same tip twice.",
            "Made by OFTS_CQM and Codex in Unity",
            "Does not use any assets or libs from Unity Store"
        };

        private GameObject _hud;
        private GameObject _loadingOverlay;
        private TextMeshProUGUI _loadingText;
        private TextMeshProUGUI _loadingTipText;
        private RectTransform _progressFill;
        private GameObject _pauseOverlay;
        private Button _resumeButton;
        private Button _quitButton;
        private Button _settingsButton;
        private SettingsMenuController _settingsMenu;
        private TextMeshProUGUI _pauseStatus;
        private Material _backgroundMaterial;
        private bool _quitting;

        public static GameplayMenuController Create(World.World world)
        {
            GameObject gameObject = new("Gameplay Menus");
            GameplayMenuController controller = gameObject.AddComponent<GameplayMenuController>();
            controller.Initialize(world);
            return controller;
        }

        private void Initialize(World.World world)
        {
            Instance = this;
            _world = world;
            _hud = GameObject.Find("Hud");
            if (_hud != null) _hud.SetActive(false);
            MenuUiFactory.EnsureEventSystem();
            BuildUi();
            SetLoadingProgress(0, 1);
        }

        private void BuildUi()
        {
            Canvas canvas = MenuUiFactory.CreateCanvas("World Loading and Pause Menus", 200);
            canvas.transform.SetParent(transform, false);
            BuildLoadingOverlay(canvas.transform);
            BuildPauseOverlay(canvas.transform);
            _settingsMenu = SettingsMenuController.Create(canvas.transform, OnSettingsClosed);
        }

        private void BuildLoadingOverlay(Transform parent)
        {
            Image root = MenuUiFactory.CreatePanel(parent, "Loading World", Color.black);
            _loadingOverlay = root.gameObject;
            MenuUiFactory.Stretch(root.rectTransform);
            _backgroundMaterial = MenuUiFactory.CreateAtlasMaterial(7);
            RawImage texture = MenuUiFactory.CreateAtlasBackground(root.transform, _backgroundMaterial, new Vector2(64, 36));
            texture.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            Image shade = MenuUiFactory.CreatePanel(root.transform, "Shade", new Color(0, 0, 0, 0.55f));
            MenuUiFactory.Stretch(shade.rectTransform);

            TextMeshProUGUI title = MenuUiFactory.CreateText(root.transform, "Title", "LOADING WORLD", 42);
            MenuUiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-400, 35), new Vector2(400, 95));
            title.fontStyle = FontStyles.Bold;

            _loadingText = MenuUiFactory.CreateText(root.transform, "Progress Text", string.Empty, 22);
            MenuUiFactory.SetAnchoredRect(_loadingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-400, -18), new Vector2(400, 22));

            Image progressBack = MenuUiFactory.CreatePanel(root.transform, "Progress Bar", new Color(0.05f, 0.05f, 0.05f, 0.95f));
            MenuUiFactory.SetAnchoredRect(progressBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-310, -58), new Vector2(310, -34));
            Image fill = MenuUiFactory.CreatePanel(progressBack.transform, "Fill", new Color(0.42f, 0.75f, 0.3f, 1f));
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0, 1);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;

            Image tipsPanel = MenuUiFactory.CreatePanel(root.transform, "Tips", new Color(0.05f, 0.05f, 0.05f, 0.82f));
            MenuUiFactory.SetAnchoredRect(tipsPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-310, -180), new Vector2(310, -96));

            TextMeshProUGUI tipsTitle = MenuUiFactory.CreateText(tipsPanel.transform, "Title", "TIPS", 18);
            MenuUiFactory.SetAnchoredRect(tipsTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -30), new Vector2(-12, -6));
            tipsTitle.fontStyle = FontStyles.Bold;

            Button tipButton = MenuUiFactory.CreateButton(tipsPanel.transform, "Next Tip", "Click to see next tip", ShowRandomTip);
            MenuUiFactory.SetAnchoredRect(tipButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(12, 10), new Vector2(-12, -36));
            tipButton.GetComponent<Image>().color = Color.clear;
            tipButton.transition = Selectable.Transition.None;
            _loadingTipText = tipButton.GetComponentInChildren<TextMeshProUGUI>();
            _loadingTipText.fontSize = 20;
            _loadingTipText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        private void ShowRandomTip()
        {
            _loadingTipText.text = LoadingTips[Random.Range(0, LoadingTips.Length)];
        }

        private void BuildPauseOverlay(Transform parent)
        {
            Image root = MenuUiFactory.CreatePanel(parent, "Pause Menu", new Color(0, 0, 0, 0.72f));
            _pauseOverlay = root.gameObject;
            MenuUiFactory.Stretch(root.rectTransform);

            Image panel = MenuUiFactory.CreatePanel(root.transform, "Pause Panel", MenuUiFactory.PanelColor);
            MenuUiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300, -225), new Vector2(300, 225));
            TextMeshProUGUI title = MenuUiFactory.CreateText(panel.transform, "Title", "GAME PAUSED", 38);
            MenuUiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(30, -80), new Vector2(-30, -24));
            title.fontStyle = FontStyles.Bold;

            _resumeButton = MenuUiFactory.CreateButton(panel.transform, "Resume", "RESUME GAME", Resume);
            MenuUiFactory.SetAnchoredRect(_resumeButton.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -164), new Vector2(-45, -106));
            _settingsButton = MenuUiFactory.CreateButton(panel.transform, "Settings", "SETTINGS", OpenSettings);
            MenuUiFactory.SetAnchoredRect(_settingsButton.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -238), new Vector2(-45, -180));
            _quitButton = MenuUiFactory.CreateButton(panel.transform, "Save and Quit", "SAVE & QUIT TO WORLD SELECTION", BeginSaveAndQuit);
            MenuUiFactory.SetAnchoredRect(_quitButton.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -312), new Vector2(-45, -254));

            _pauseStatus = MenuUiFactory.CreateText(panel.transform, "Status", string.Empty, 18);
            MenuUiFactory.SetAnchoredRect(_pauseStatus.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(30, 34), new Vector2(-30, 78));
            _pauseStatus.color = new Color(1f, 0.85f, 0.45f, 1f);
            _pauseOverlay.SetActive(false);
        }

        public void SetLoadingProgress(int loaded, int total)
        {
            total = Mathf.Max(1, total);
            float progress = Mathf.Clamp01((float)loaded / total);
            _loadingText.text = $"Preparing chunks... {loaded} / {total}";
            _progressFill.anchorMax = new Vector2(progress, 1);
        }

        public void CompleteLoading()
        {
            _loadingOverlay.SetActive(false);
            if (_hud != null) _hud.SetActive(true);
        }

        public void ShowPause()
        {
            if (_quitting || _loadingOverlay.activeSelf) return;
            _pauseStatus.text = string.Empty;
            _pauseOverlay.SetActive(true);
            Player.PauseGame();
        }

        public void Resume()
        {
            if (_quitting || (_settingsMenu != null && _settingsMenu.IsOpen)) return;
            _pauseOverlay.SetActive(false);
            Player.ResumeGame();
        }

        private void OpenSettings()
        {
            if (_quitting) return;
            _pauseOverlay.SetActive(false);
            _settingsMenu.Open();
        }

        private void OnSettingsClosed()
        {
            if (!_quitting) _pauseOverlay.SetActive(true);
        }

        private void BeginSaveAndQuit()
        {
            if (_quitting) return;
            _quitting = true;
            _resumeButton.interactable = false;
            _settingsButton.interactable = false;
            _quitButton.interactable = false;
            _pauseStatus.text = "Saving world...";
            StartCoroutine(SaveAndQuitNextFrame());
        }

        private IEnumerator SaveAndQuitNextFrame()
        {
            yield return null;
            _world.SaveAndQuitToWorldSelection();
        }

        private void OnDestroy()
        {
            if (_backgroundMaterial != null) Destroy(_backgroundMaterial);
            if (Instance == this) Instance = null;
        }
    }
}
