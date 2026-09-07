using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using world.persistence;
using render.ui;

namespace render.screens
{
    public sealed class WorldSelectionController : MonoBehaviour
    {
        private sealed class WorldEntry
        {
            public readonly WorldDescriptor Descriptor;
            public readonly Button Button;

            public WorldEntry(WorldDescriptor descriptor, Button button)
            {
                Descriptor = descriptor;
                Button = button;
            }
        }

        private FileWorldStorage _storage;
        private Material _backgroundMaterial;
        private RectTransform _worldList;
        private TextMeshProUGUI _emptyMessage;
        private TextMeshProUGUI _statusText;
        private Button _playButton;
        private readonly List<WorldEntry> _entries = new();
        private WorldEntry _selected;

        private GameObject _createOverlay;
        private TMP_InputField _nameInput;
        private TMP_InputField _seedInput;
        private TextMeshProUGUI _createError;

        private GameObject _dialogOverlay;
        private TextMeshProUGUI _dialogTitle;
        private TextMeshProUGUI _dialogMessage;
        private Button _dialogPrimary;
        private TextMeshProUGUI _dialogPrimaryLabel;
        private Button _dialogSecondary;
        private Action _dialogAction;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            WorldSession.ClearSelection();
            _storage = new FileWorldStorage(Application.persistentDataPath);
            MenuUiFactory.EnsureEventSystem();
            BuildUi();
            RefreshWorlds();

            string pendingError = WorldSession.ConsumeError();
            if (!string.IsNullOrWhiteSpace(pendingError)) ShowDialog("Could Not Load World", pendingError, "OK");
        }

        private void BuildUi()
        {
            Canvas canvas = MenuUiFactory.CreateCanvas("World Selection", 100);
            canvas.transform.SetParent(transform, false);
            _backgroundMaterial = MenuUiFactory.CreateAtlasMaterial(0);
            RawImage background = MenuUiFactory.CreateAtlasBackground(canvas.transform, _backgroundMaterial, new Vector2(64, 36));
            background.color = new Color(0.72f, 0.72f, 0.72f, 1f);

            Image shade = MenuUiFactory.CreatePanel(canvas.transform, "Background Shade", new Color(0, 0, 0, 0.42f));
            MenuUiFactory.Stretch(shade.rectTransform);

            TextMeshProUGUI title = MenuUiFactory.CreateText(canvas.transform, "Title", "SELECT WORLD", 46);
            MenuUiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-400, -94), new Vector2(400, -30));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 4;

            Image mainPanel = MenuUiFactory.CreatePanel(canvas.transform, "World List Panel", MenuUiFactory.PanelColor);
            MenuUiFactory.SetAnchoredRect(mainPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-470, -250), new Vector2(470, 245));

            BuildWorldList(mainPanel.transform);

            _playButton = MenuUiFactory.CreateButton(canvas.transform, "Play Selected", "PLAY SELECTED", PlaySelected);
            MenuUiFactory.SetAnchoredRect(_playButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-465, 32), new Vector2(-8, 88));
            _playButton.interactable = false;

            Button createButton = MenuUiFactory.CreateButton(canvas.transform, "Create World", "CREATE NEW WORLD", OpenCreate);
            MenuUiFactory.SetAnchoredRect(createButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(8, 32), new Vector2(465, 88));

            _statusText = MenuUiFactory.CreateText(canvas.transform, "Status", string.Empty, 18);
            MenuUiFactory.SetAnchoredRect(_statusText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-470, 4), new Vector2(470, 29));
            _statusText.color = new Color(1f, 0.85f, 0.45f, 1f);

            BuildCreateOverlay(canvas.transform);
            BuildDialogOverlay(canvas.transform);
        }

        private void BuildWorldList(Transform parent)
        {
            Image viewportImage = MenuUiFactory.CreatePanel(parent, "Viewport", new Color(0, 0, 0, 0.25f));
            RectTransform viewport = viewportImage.rectTransform;
            MenuUiFactory.Stretch(viewport, 16, 16, 16, 16);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            GameObject contentObject = new("Worlds", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            _worldList = contentObject.GetComponent<RectTransform>();
            _worldList.anchorMin = new Vector2(0, 1);
            _worldList.anchorMax = new Vector2(1, 1);
            _worldList.pivot = new Vector2(0.5f, 1);
            _worldList.anchoredPosition = Vector2.zero;
            _worldList.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _worldList;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;

            _emptyMessage = MenuUiFactory.CreateText(viewport, "Empty Message",
                "No worlds yet. Create one to begin.", 24);
            MenuUiFactory.Stretch(_emptyMessage.rectTransform, 20, 20, 20, 20);
            _emptyMessage.color = new Color(0.78f, 0.78f, 0.78f, 1f);
        }

        private void RefreshWorlds()
        {
            foreach (Transform child in _worldList) Destroy(child.gameObject);
            _entries.Clear();
            _selected = null;
            _playButton.interactable = false;
            int unreadable = 0;
            List<WorldDescriptor> descriptors = new();

            foreach (string worldId in _storage.ListWorldIds())
            {
                try { descriptors.Add(_storage.ReadWorldDescriptor(worldId)); }
                catch (Exception exception)
                {
                    unreadable++;
                    CreateUnavailableRow(worldId, exception.Message);
                }
            }

            foreach (WorldDescriptor descriptor in descriptors.OrderByDescending(SavedSortKey)) CreateWorldRow(descriptor);
            _emptyMessage.gameObject.SetActive(descriptors.Count == 0 && unreadable == 0);
            _statusText.text = unreadable == 0 ? string.Empty : $"{unreadable} save folder(s) could not be read.";
            if (_entries.Count > 0) SelectEntry(_entries[0]);
        }

        private void CreateWorldRow(WorldDescriptor descriptor)
        {
            Button button = MenuUiFactory.CreateButton(_worldList, descriptor.WorldId, string.Empty, null);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(18, 5, 18, 5);
            label.fontSize = 23;
            label.text = $"<b>{Escape(descriptor.DisplayName)}</b>\n<size=16><color=#C8C8C8>Last saved: {FormatDate(descriptor.lastSavedUtc)}    Seed: {Escape(descriptor.worldSeed)}</color></size>";
            WorldEntry entry = new(descriptor, button);
            _entries.Add(entry);
            button.onClick.AddListener(() => SelectEntry(entry));
        }

        private void CreateUnavailableRow(string worldId, string error)
        {
            Button button = MenuUiFactory.CreateButton(_worldList, worldId, $"{worldId}\n<size=16><color=#FF8888>Unavailable save</color></size>",
                () => ShowDialog("Unavailable World", error, "OK"));
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(18, 5, 18, 5);
        }

        private void SelectEntry(WorldEntry entry)
        {
            _selected = entry;
            _playButton.interactable = true;
            foreach (WorldEntry candidate in _entries)
            {
                ColorBlock colors = candidate.Button.colors;
                colors.normalColor = candidate == entry ? MenuUiFactory.SelectedColor : MenuUiFactory.ButtonColor;
                candidate.Button.colors = colors;
            }
        }

        private void PlaySelected()
        {
            if (_selected == null) return;
            try
            {
                WorldLoadAuthorization authorization = SaveVersionPolicy.Authorize(_selected.Descriptor);
                EnterWorld(authorization);
            }
            catch (NewerContentConfirmationRequiredException exception)
            {
                ShowDialog("Newer World Version",
                    $"{exception.Descriptor.DisplayName} was saved by a newer content version. Unknown blocks may be replaced if you continue.",
                    "LOAD ANYWAY", () => EnterWorld(SaveVersionPolicy.Authorize(exception.Descriptor, true)), "CANCEL");
            }
            catch (Exception exception) { ShowDialog("Could Not Load World", exception.Message, "OK"); }
        }

        private static void EnterWorld(WorldLoadAuthorization authorization)
        {
            WorldSession.Select(authorization);
            SceneManager.LoadScene(GameScenes.Gameplay);
        }

        private void BuildCreateOverlay(Transform parent)
        {
            Image shade = MenuUiFactory.CreatePanel(parent, "Create World Overlay", new Color(0, 0, 0, 0.75f));
            _createOverlay = shade.gameObject;
            MenuUiFactory.Stretch(shade.rectTransform);

            Image panel = MenuUiFactory.CreatePanel(shade.transform, "Create World Panel", MenuUiFactory.PanelColor);
            MenuUiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360, -220), new Vector2(360, 220));

            TextMeshProUGUI title = MenuUiFactory.CreateText(panel.transform, "Title", "CREATE NEW WORLD", 34);
            MenuUiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(25, -70), new Vector2(-25, -20));

            TextMeshProUGUI nameLabel = MenuUiFactory.CreateText(panel.transform, "Name Label", "World Name", 20, TextAlignmentOptions.BottomLeft);
            MenuUiFactory.SetAnchoredRect(nameLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -120), new Vector2(-45, -85));
            _nameInput = MenuUiFactory.CreateInput(panel.transform, "World Name", "My World");
            MenuUiFactory.SetAnchoredRect(_nameInput.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -176), new Vector2(-45, -124));
            _nameInput.characterLimit = 64;

            TextMeshProUGUI seedLabel = MenuUiFactory.CreateText(panel.transform, "Seed Label", "Seed (optional)", 20, TextAlignmentOptions.BottomLeft);
            MenuUiFactory.SetAnchoredRect(seedLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -226), new Vector2(-45, -191));
            _seedInput = MenuUiFactory.CreateInput(panel.transform, "World Seed", "Leave blank for a random seed");
            MenuUiFactory.SetAnchoredRect(_seedInput.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(45, -282), new Vector2(-45, -230));

            _createError = MenuUiFactory.CreateText(panel.transform, "Error", string.Empty, 17);
            MenuUiFactory.SetAnchoredRect(_createError.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(45, 91), new Vector2(-45, 120));
            _createError.color = new Color(1f, 0.45f, 0.4f, 1f);

            Button cancel = MenuUiFactory.CreateButton(panel.transform, "Cancel", "CANCEL", CloseCreate);
            MenuUiFactory.SetAnchoredRect(cancel.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(45, 28), new Vector2(-8, 82));
            Button create = MenuUiFactory.CreateButton(panel.transform, "Create", "CREATE WORLD", CreateWorld);
            MenuUiFactory.SetAnchoredRect(create.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(8, 28), new Vector2(-45, 82));
            _createOverlay.SetActive(false);
        }

        private void OpenCreate()
        {
            _nameInput.text = string.Empty;
            _seedInput.text = string.Empty;
            _createError.text = string.Empty;
            _createOverlay.SetActive(true);
            _nameInput.Select();
            _nameInput.ActivateInputField();
        }

        private void CloseCreate() => _createOverlay.SetActive(false);

        private void CreateWorld()
        {
            string displayName = _nameInput.text.Trim();
            if (displayName.Length == 0)
            {
                _createError.text = "Enter a world name.";
                return;
            }

            try
            {
                string worldId = WorldIdUtility.CreateUnique(displayName, _storage.ListWorldIds());
                WorldDescriptor descriptor = _storage.CreateWorld(worldId, displayName, _seedInput.text);
                EnterWorld(SaveVersionPolicy.Authorize(descriptor));
            }
            catch (Exception exception) { _createError.text = exception.Message; }
        }

        private void BuildDialogOverlay(Transform parent)
        {
            Image shade = MenuUiFactory.CreatePanel(parent, "Dialog Overlay", new Color(0, 0, 0, 0.78f));
            _dialogOverlay = shade.gameObject;
            MenuUiFactory.Stretch(shade.rectTransform);
            Image panel = MenuUiFactory.CreatePanel(shade.transform, "Dialog", MenuUiFactory.PanelColor);
            MenuUiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-370, -175), new Vector2(370, 175));

            _dialogTitle = MenuUiFactory.CreateText(panel.transform, "Title", string.Empty, 32);
            MenuUiFactory.SetAnchoredRect(_dialogTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(30, -66), new Vector2(-30, -18));
            _dialogMessage = MenuUiFactory.CreateText(panel.transform, "Message", string.Empty, 21);
            MenuUiFactory.SetAnchoredRect(_dialogMessage.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(45, 92), new Vector2(-45, -78));

            _dialogSecondary = MenuUiFactory.CreateButton(panel.transform, "Secondary", "CANCEL", CloseDialog);
            MenuUiFactory.SetAnchoredRect(_dialogSecondary.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(45, 25), new Vector2(-8, 78));
            _dialogPrimary = MenuUiFactory.CreateButton(panel.transform, "Primary", "OK", InvokeDialogAction);
            MenuUiFactory.SetAnchoredRect(_dialogPrimary.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(8, 25), new Vector2(-45, 78));
            _dialogPrimaryLabel = _dialogPrimary.GetComponentInChildren<TextMeshProUGUI>();
            _dialogOverlay.SetActive(false);
        }

        private void ShowDialog(string title, string message, string primary, Action action = null, string secondary = null)
        {
            _dialogTitle.text = title;
            _dialogMessage.text = message;
            _dialogPrimaryLabel.text = primary;
            _dialogAction = action;
            _dialogSecondary.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            if (!string.IsNullOrEmpty(secondary)) _dialogSecondary.GetComponentInChildren<TextMeshProUGUI>().text = secondary;
            RectTransform primaryRect = _dialogPrimary.GetComponent<RectTransform>();
            if (string.IsNullOrEmpty(secondary))
                MenuUiFactory.SetAnchoredRect(primaryRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 25), new Vector2(180, 78));
            else
                MenuUiFactory.SetAnchoredRect(primaryRect, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(8, 25), new Vector2(-45, 78));
            _dialogOverlay.SetActive(true);
        }

        private void InvokeDialogAction()
        {
            Action action = _dialogAction;
            CloseDialog();
            action?.Invoke();
        }

        private void CloseDialog()
        {
            _dialogOverlay.SetActive(false);
            _dialogAction = null;
        }

        private static DateTime SavedSortKey(WorldDescriptor descriptor)
        {
            return DateTime.TryParse(descriptor.lastSavedUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime value) ? value : DateTime.MinValue;
        }

        private static string FormatDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
                ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
                : "Unknown";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private void OnDestroy()
        {
            if (_backgroundMaterial != null) Destroy(_backgroundMaterial);
        }
    }

    public static class WorldSelectionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameScenes.WorldSelection || UnityEngine.Object.FindAnyObjectByType<WorldSelectionController>() != null) return;
            new GameObject("World Selection Controller").AddComponent<WorldSelectionController>();
        }
    }
}
