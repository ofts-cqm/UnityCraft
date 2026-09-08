using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace settings
{
    public static class GameSettings
    {
        public const float DefaultSensitivity = 0.3f;
        public const int DefaultViewDistance = 8;
        public const int DefaultAutosaveInterval = 30;

        public const float MinimumSensitivity = 0.05f;
        public const float MaximumSensitivity = 1f;
        public const int MinimumViewDistance = 2;
        public const int MaximumViewDistance = 12;
        public const int MinimumAutosaveInterval = 10;
        public const int MaximumAutosaveInterval = 300;

        private const string SensitivityKey = "settings.player.sensitivity";
        private const string ViewDistanceKey = "settings.world.viewDistance";
        private const string AutosaveIntervalKey = "settings.world.autosaveInterval";
        private const string BindingOverridesKey = "settings.player.bindingOverrides";
        private static readonly HashSet<string> ExcludedBindingActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "Look",
            "Pause",
            "Scroll",
            "SprintPending"
        };

        private static bool _loaded;
        private static InputActionAsset _actions;

        public static float Sensitivity { get; private set; } = DefaultSensitivity;
        public static int ViewDistance { get; private set; } = DefaultViewDistance;
        public static int AutosaveInterval { get; private set; } = DefaultAutosaveInterval;
        public static event Action Applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => EnsureLoaded();

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _actions = InputSystem.actions;
            Sensitivity = NormalizeSensitivity(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity));
            ViewDistance = Mathf.Clamp(PlayerPrefs.GetInt(ViewDistanceKey, DefaultViewDistance),
                MinimumViewDistance, MaximumViewDistance);
            AutosaveInterval = NormalizeAutosave(PlayerPrefs.GetInt(AutosaveIntervalKey, DefaultAutosaveInterval));

            string overrides = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
            if (_actions != null && !string.IsNullOrWhiteSpace(overrides))
            {
                try
                {
                    _actions.LoadBindingOverridesFromJson(overrides);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Ignoring invalid saved input bindings: {exception.Message}");
                    _actions.RemoveAllBindingOverrides();
                    PlayerPrefs.DeleteKey(BindingOverridesKey);
                    PlayerPrefs.Save();
                }
            }
            if (_actions != null) SynchronizeDerivedBindings(_actions);

            _loaded = true;
        }

        public static InputActionAsset CreateDraftActions(bool useDefaults = false)
        {
            EnsureLoaded();
            if (_actions == null) throw new InvalidOperationException("The project input action asset is not available.");

            InputActionAsset draft = InputActionAsset.FromJson(_actions.ToJson());
            if (!useDefaults)
            {
                string overrides = _actions.SaveBindingOverridesAsJson();
                if (!string.IsNullOrWhiteSpace(overrides)) draft.LoadBindingOverridesFromJson(overrides);
            }
            SynchronizeDerivedBindings(draft);
            return draft;
        }

        public static void Apply(float sensitivity, int viewDistance, int autosaveInterval, InputActionAsset draftActions)
        {
            EnsureLoaded();
            if (_actions == null) throw new InvalidOperationException("The project input action asset is not available.");
            if (draftActions == null) throw new ArgumentNullException(nameof(draftActions));

            Sensitivity = NormalizeSensitivity(sensitivity);
            ViewDistance = Mathf.Clamp(viewDistance, MinimumViewDistance, MaximumViewDistance);
            AutosaveInterval = NormalizeAutosave(autosaveInterval);

            SynchronizeDerivedBindings(draftActions);
            string overrides = draftActions.SaveBindingOverridesAsJson();
            _actions.RemoveAllBindingOverrides();
            if (!string.IsNullOrWhiteSpace(overrides)) _actions.LoadBindingOverridesFromJson(overrides);

            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.SetInt(ViewDistanceKey, ViewDistance);
            PlayerPrefs.SetInt(AutosaveIntervalKey, AutosaveInterval);
            PlayerPrefs.SetString(BindingOverridesKey, overrides);
            PlayerPrefs.Save();
            Applied?.Invoke();
        }

        public static bool IsBindingConfigurable(string actionName) =>
            !ExcludedBindingActions.Contains(actionName);

        public static string GetBindingDisplayName(InputAction action, int bindingIndex)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            string display = action.GetBindingDisplayString(bindingIndex,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            if (!string.IsNullOrWhiteSpace(display)) return display;

            string path = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrWhiteSpace(path)) return "Unbound";
            int slash = path.LastIndexOf('/');
            string controlName = slash >= 0 ? path.Substring(slash + 1) : path;
            controlName = controlName.Trim('{', '}');
            if (string.Equals(controlName, "space", StringComparison.OrdinalIgnoreCase)) return "Space";
            return NicifyControlName(controlName);
        }

        public static void SynchronizeDerivedBindings(InputActionAsset actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            InputActionMap player = actions.FindActionMap("Player");
            if (player == null) return;
            InputAction move = player.FindAction("Move");
            InputAction sprintPending = player.FindAction("SprintPending");
            if (move == null || sprintPending == null) return;

            string forwardPath = null;
            for (int i = 0; i < move.bindings.Count; i++)
            {
                InputBinding binding = move.bindings[i];
                if (!binding.isPartOfComposite || !string.Equals(binding.name, "up", StringComparison.OrdinalIgnoreCase) ||
                    !IsKeyboardMousePath(binding.path)) continue;
                forwardPath = binding.effectivePath;
                break;
            }
            if (string.IsNullOrWhiteSpace(forwardPath)) return;

            for (int i = 0; i < sprintPending.bindings.Count; i++)
            {
                InputBinding binding = sprintPending.bindings[i];
                if (!IsKeyboardMousePath(binding.path) ||
                    string.Equals(binding.effectivePath, forwardPath, StringComparison.OrdinalIgnoreCase)) continue;
                sprintPending.ApplyBindingOverride(i, forwardPath);
            }
        }

        public static float NormalizeSensitivity(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultSensitivity;
            value = Mathf.Clamp(value, MinimumSensitivity, MaximumSensitivity);
            return Mathf.Round(value / 0.05f) * 0.05f;
        }

        public static int NormalizeAutosave(int value)
        {
            value = Mathf.Clamp(value, MinimumAutosaveInterval, MaximumAutosaveInterval);
            return Mathf.RoundToInt(value / 10f) * 10;
        }

        private static bool IsKeyboardMousePath(string path)
        {
            path ??= string.Empty;
            return path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Pointer>", StringComparison.OrdinalIgnoreCase);
        }

        private static string NicifyControlName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unbound";
            StringBuilder result = new();
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(value[i - 1])) result.Append(' ');
                result.Append(i == 0 ? char.ToUpperInvariant(current) : current);
            }
            return result.ToString();
        }
    }
}
