using System;
using System.Collections.Generic;
using System.Linq;
using settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using render.ui;

namespace render.screens
{
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private sealed class BindingRow
        {
            public GameObject Root;
            public InputAction Action;
            public int BindingIndex;
            public string Label;
            public TextMeshProUGUI ButtonLabel;
        }

        private Action _onClosed;
        private Slider _sensitivitySlider;
        private Slider _viewDistanceSlider;
        private Slider _autosaveSlider;
        private TextMeshProUGUI _sensitivityValue;
        private TextMeshProUGUI _viewDistanceValue;
        private TextMeshProUGUI _autosaveValue;
        private TextMeshProUGUI _warning;
        private RectTransform _content;
        private InputActionAsset _draftActions;
        private InputActionMap _livePlayerMap;
        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private bool _livePlayerMapWasEnabled;
        private bool _ignoreEscapeUntilReleased;
        private readonly List<BindingRow> _bindingRows = new();

        public bool IsOpen => gameObject.activeSelf;
        public bool IsRebinding => _rebindOperation != null;

        public static SettingsMenuController Create(Transform parent, Action onClosed)
        {
            Image root = MenuUiFactory.CreatePanel(parent, "Settings Menu", new Color(0, 0, 0, 0.82f));
            MenuUiFactory.Stretch(root.rectTransform);
            SettingsMenuController controller = root.gameObject.AddComponent<SettingsMenuController>();
            controller._onClosed = onClosed;
            controller.BuildUi();
            root.gameObject.SetActive(false);
            return controller;
        }

        public void Open()
        {
            if (IsOpen) return;
            GameSettings.EnsureLoaded();
            _livePlayerMap = InputSystem.actions?.FindActionMap("Player");
            _livePlayerMapWasEnabled = _livePlayerMap != null && _livePlayerMap.enabled;
            if (_livePlayerMapWasEnabled) _livePlayerMap.Disable();

            ReplaceDraft(GameSettings.CreateDraftActions());
            _sensitivitySlider.SetValueWithoutNotify(GameSettings.Sensitivity);
            _viewDistanceSlider.SetValueWithoutNotify(GameSettings.ViewDistance);
            _autosaveSlider.SetValueWithoutNotify(GameSettings.AutosaveInterval);
            UpdateSliderLabels();
            RebuildBindingRows();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            CloseWithoutApplying();
        }

        private void BuildUi()
        {
            Image panel = MenuUiFactory.CreatePanel(transform, "Settings Panel", MenuUiFactory.PanelColor);
            MenuUiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-520, -330), new Vector2(520, 330));

            TextMeshProUGUI title = MenuUiFactory.CreateText(panel.transform, "Title", "SETTINGS", 38);
            MenuUiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(30, -68), new Vector2(-30, -18));
            title.fontStyle = FontStyles.Bold;

            Image viewportImage = MenuUiFactory.CreatePanel(panel.transform, "Viewport", new Color(0, 0, 0, 0.22f));
            RectTransform viewport = viewportImage.rectTransform;
            MenuUiFactory.SetAnchoredRect(viewport, Vector2.zero, Vector2.one,
                new Vector2(28, 112), new Vector2(-28, -82));
            viewportImage.gameObject.AddComponent<RectMask2D>();

            GameObject contentObject = new("Settings Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            _content = contentObject.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;

            BuildNumericRows();

            _warning = MenuUiFactory.CreateText(panel.transform, "Binding Warning", string.Empty, 16,
                TextAlignmentOptions.MidlineLeft);
            MenuUiFactory.SetAnchoredRect(_warning.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(30, 80), new Vector2(-30, 108));
            _warning.color = new Color(1f, 0.65f, 0.2f, 1f);
            _warning.textWrappingMode = TextWrappingModes.Normal;

            Button reset = MenuUiFactory.CreateButton(panel.transform, "Reset", "RESET DEFAULTS", ResetDefaults);
            MenuUiFactory.SetAnchoredRect(reset.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.34f, 0),
                new Vector2(30, 20), new Vector2(-8, 72));
            Button cancel = MenuUiFactory.CreateButton(panel.transform, "Cancel", "CANCEL", Cancel);
            MenuUiFactory.SetAnchoredRect(cancel.GetComponent<RectTransform>(), new Vector2(0.34f, 0), new Vector2(0.67f, 0),
                new Vector2(8, 20), new Vector2(-8, 72));
            Button apply = MenuUiFactory.CreateButton(panel.transform, "Apply", "APPLY", Apply);
            MenuUiFactory.SetAnchoredRect(apply.GetComponent<RectTransform>(), new Vector2(0.67f, 0), new Vector2(1, 0),
                new Vector2(8, 20), new Vector2(-30, 72));
        }

        private void BuildNumericRows()
        {
            _sensitivitySlider = CreateSliderRow("Mouse Sensitivity", GameSettings.MinimumSensitivity,
                GameSettings.MaximumSensitivity, false, out _sensitivityValue);
            _viewDistanceSlider = CreateSliderRow("View Distance (chunks)", GameSettings.MinimumViewDistance,
                GameSettings.MaximumViewDistance, true, out _viewDistanceValue);
            _autosaveSlider = CreateSliderRow("Autosave Interval (seconds)", GameSettings.MinimumAutosaveInterval,
                GameSettings.MaximumAutosaveInterval, true, out _autosaveValue);

            _sensitivitySlider.onValueChanged.AddListener(value =>
            {
                float normalized = GameSettings.NormalizeSensitivity(value);
                _sensitivitySlider.SetValueWithoutNotify(normalized);
                UpdateSliderLabels();
            });
            _viewDistanceSlider.onValueChanged.AddListener(_ => UpdateSliderLabels());
            _autosaveSlider.onValueChanged.AddListener(value =>
            {
                int normalized = GameSettings.NormalizeAutosave(Mathf.RoundToInt(value));
                _autosaveSlider.SetValueWithoutNotify(normalized);
                UpdateSliderLabels();
            });

            TextMeshProUGUI controls = MenuUiFactory.CreateText(_content, "Controls Heading", "KEY BINDINGS", 24,
                TextAlignmentOptions.MidlineLeft);
            controls.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            controls.fontStyle = FontStyles.Bold;
        }

        private Slider CreateSliderRow(string labelText, float minimum, float maximum, bool wholeNumbers,
            out TextMeshProUGUI valueLabel)
        {
            Image row = MenuUiFactory.CreatePanel(_content, labelText, new Color(0.13f, 0.13f, 0.13f, 0.96f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            TextMeshProUGUI label = MenuUiFactory.CreateText(row.transform, "Label", labelText, 20,
                TextAlignmentOptions.MidlineLeft);
            MenuUiFactory.SetAnchoredRect(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(16, 0), new Vector2(-570, 0));

            Image track = MenuUiFactory.CreatePanel(row.transform, "Slider", new Color(0.04f, 0.04f, 0.04f, 1f));
            MenuUiFactory.SetAnchoredRect(track.rectTransform, new Vector2(0.43f, 0.5f), new Vector2(0.86f, 0.5f),
                new Vector2(0, -8), new Vector2(0, 8));
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = wholeNumbers;

            Image fill = MenuUiFactory.CreatePanel(track.transform, "Fill", new Color(0.42f, 0.75f, 0.3f, 1f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4, 4);
            fillRect.offsetMax = new Vector2(-4, -4);
            Image handle = MenuUiFactory.CreatePanel(track.transform, "Handle", new Color(0.86f, 0.86f, 0.86f, 1f));
            RectTransform handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(18, 30);
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;

            valueLabel = MenuUiFactory.CreateText(row.transform, "Value", string.Empty, 20);
            MenuUiFactory.SetAnchoredRect(valueLabel.rectTransform, new Vector2(0.87f, 0), Vector2.one,
                Vector2.zero, new Vector2(-12, 0));
            return slider;
        }

        private void RebuildBindingRows()
        {
            foreach (BindingRow row in _bindingRows)
                if (row.Root != null) Destroy(row.Root);
            _bindingRows.Clear();

            InputActionMap playerMap = _draftActions.FindActionMap("Player", true);
            Dictionary<string, int> totals = new(StringComparer.OrdinalIgnoreCase);
            foreach (InputAction action in playerMap.actions)
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!GameSettings.IsBindingConfigurable(action.name) || binding.isComposite ||
                    !IsKeyboardMouseBinding(binding)) continue;
                string key = BindingLabelBase(action, binding);
                totals[key] = totals.GetValueOrDefault(key) + 1;
            }

            Dictionary<string, int> occurrences = new(StringComparer.OrdinalIgnoreCase);
            foreach (InputAction action in playerMap.actions)
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!GameSettings.IsBindingConfigurable(action.name) || binding.isComposite ||
                    !IsKeyboardMouseBinding(binding)) continue;

                string baseLabel = BindingLabelBase(action, binding);
                occurrences[baseLabel] = occurrences.GetValueOrDefault(baseLabel) + 1;
                string label = totals[baseLabel] > 1
                    ? $"{baseLabel} — {(occurrences[baseLabel] == 1 ? "Primary" : "Alternate " + occurrences[baseLabel])}"
                    : baseLabel;
                CreateBindingRow(action, i, label);
            }
            UpdateBindingDisplays();
        }

        private void CreateBindingRow(InputAction action, int bindingIndex, string labelText)
        {
            Image row = MenuUiFactory.CreatePanel(_content, labelText, new Color(0.13f, 0.13f, 0.13f, 0.96f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
            TextMeshProUGUI label = MenuUiFactory.CreateText(row.transform, "Action", labelText, 19,
                TextAlignmentOptions.MidlineLeft);
            MenuUiFactory.SetAnchoredRect(label.rectTransform, Vector2.zero, new Vector2(0.55f, 1),
                new Vector2(16, 0), new Vector2(-8, 0));
            Button button = MenuUiFactory.CreateButton(row.transform, "Binding", string.Empty, null);
            MenuUiFactory.SetAnchoredRect(button.GetComponent<RectTransform>(), new Vector2(0.55f, 0), Vector2.one,
                new Vector2(8, 5), new Vector2(-8, -5));
            TextMeshProUGUI buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
            buttonLabel.fontSize = 18;
            BindingRow bindingRow = new()
            {
                Root = row.gameObject,
                Action = action,
                BindingIndex = bindingIndex,
                Label = labelText,
                ButtonLabel = buttonLabel
            };
            _bindingRows.Add(bindingRow);
            button.onClick.AddListener(() => StartRebind(bindingRow));
        }

        private void StartRebind(BindingRow row)
        {
            if (_rebindOperation != null) return;
            row.ButtonLabel.text = "PRESS A KEY OR MOUSE CONTROL...";
            _rebindOperation = row.Action.PerformInteractiveRebinding(row.BindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishRebind(true))
                .OnComplete(_ => FinishRebind(false));
            _rebindOperation.Start();
        }

        private void FinishRebind(bool cancelled)
        {
            _rebindOperation?.Dispose();
            _rebindOperation = null;
            _ignoreEscapeUntilReleased = cancelled;
            GameSettings.SynchronizeDerivedBindings(_draftActions);
            UpdateBindingDisplays();
        }

        private void UpdateBindingDisplays()
        {
            foreach (BindingRow row in _bindingRows)
                row.ButtonLabel.text = BindingDisplay(row);
            UpdateConflictWarning();
        }

        private void UpdateConflictWarning()
        {
            var conflicts = _bindingRows
                .GroupBy(row => row.Action.bindings[row.BindingIndex].effectivePath, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => $"{BindingDisplay(group.First())}: " +
                                 string.Join(", ", group.Select(row => row.Label)))
                .ToArray();
            _warning.text = conflicts.Length == 0
                ? string.Empty
                : "WARNING — DUPLICATE BINDINGS: " + string.Join("    |    ", conflicts);
        }

        private void ResetDefaults()
        {
            CancelActiveRebind();
            ReplaceDraft(GameSettings.CreateDraftActions(true));
            _sensitivitySlider.SetValueWithoutNotify(GameSettings.DefaultSensitivity);
            _viewDistanceSlider.SetValueWithoutNotify(GameSettings.DefaultViewDistance);
            _autosaveSlider.SetValueWithoutNotify(GameSettings.DefaultAutosaveInterval);
            UpdateSliderLabels();
            RebuildBindingRows();
        }

        private void Apply()
        {
            CancelActiveRebind();
            GameSettings.Apply(_sensitivitySlider.value, Mathf.RoundToInt(_viewDistanceSlider.value),
                Mathf.RoundToInt(_autosaveSlider.value), _draftActions);
            CloseWithoutApplying();
        }

        private void CloseWithoutApplying()
        {
            CancelActiveRebind();
            ReplaceDraft(null);
            gameObject.SetActive(false);
            if (_livePlayerMapWasEnabled && _livePlayerMap != null) _livePlayerMap.Enable();
            _livePlayerMap = null;
            _onClosed?.Invoke();
        }

        private void ReplaceDraft(InputActionAsset replacement)
        {
            if (_draftActions != null) Destroy(_draftActions);
            _draftActions = replacement;
        }

        private void CancelActiveRebind()
        {
            if (_rebindOperation == null) return;
            InputActionRebindingExtensions.RebindingOperation operation = _rebindOperation;
            _rebindOperation = null;
            operation.Cancel();
            operation.Dispose();
        }

        private void UpdateSliderLabels()
        {
            _sensitivityValue.text = GameSettings.NormalizeSensitivity(_sensitivitySlider.value).ToString("0.00");
            _viewDistanceValue.text = Mathf.RoundToInt(_viewDistanceSlider.value).ToString();
            _autosaveValue.text = $"{GameSettings.NormalizeAutosave(Mathf.RoundToInt(_autosaveSlider.value))} s";
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.escapeKey.isPressed) _ignoreEscapeUntilReleased = false;
            if (_rebindOperation == null && !_ignoreEscapeUntilReleased && Keyboard.current.escapeKey.wasPressedThisFrame)
                Cancel();
        }

        private void OnDestroy()
        {
            CancelActiveRebind();
            if (_draftActions != null) Destroy(_draftActions);
            if (_livePlayerMapWasEnabled && _livePlayerMap != null) _livePlayerMap.Enable();
        }

        private static bool IsKeyboardMouseBinding(InputBinding binding)
        {
            string path = binding.path ?? string.Empty;
            return path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Pointer>", StringComparison.OrdinalIgnoreCase);
        }

        private static string BindingLabelBase(InputAction action, InputBinding binding)
        {
            string actionName = action.name.All(char.IsDigit) ? $"Hotbar {action.name}" : action.name;
            if (action.name == "SprintPending") actionName = "Sprint (double-tap forward)";
            if (!binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.name)) return actionName;
            string part = char.ToUpperInvariant(binding.name[0]) + binding.name.Substring(1);
            return $"{actionName} {part}";
        }

        private static string BindingDisplay(BindingRow row)
        {
            return GameSettings.GetBindingDisplayName(row.Action, row.BindingIndex);
        }
    }
}
