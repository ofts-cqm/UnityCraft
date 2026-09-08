using System.Reflection;
using NUnit.Framework;
using settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tests.Editor
{
    public sealed class SettingsTests
    {
        private const string SensitivityKey = "settings.player.sensitivity";
        private const string ViewDistanceKey = "settings.world.viewDistance";
        private const string AutosaveIntervalKey = "settings.world.autosaveInterval";
        private const string BindingOverridesKey = "settings.player.bindingOverrides";

        private float _originalSensitivity;
        private int _originalViewDistance;
        private int _originalAutosaveInterval;
        private string _originalOverrides;
        private bool _hadSensitivity;
        private bool _hadViewDistance;
        private bool _hadAutosaveInterval;
        private bool _hadOverrides;

        [SetUp]
        public void SetUp()
        {
            _hadSensitivity = PlayerPrefs.HasKey(SensitivityKey);
            _hadViewDistance = PlayerPrefs.HasKey(ViewDistanceKey);
            _hadAutosaveInterval = PlayerPrefs.HasKey(AutosaveIntervalKey);
            _hadOverrides = PlayerPrefs.HasKey(BindingOverridesKey);
            _originalSensitivity = PlayerPrefs.GetFloat(SensitivityKey);
            _originalViewDistance = PlayerPrefs.GetInt(ViewDistanceKey);
            _originalAutosaveInterval = PlayerPrefs.GetInt(AutosaveIntervalKey);
            _originalOverrides = PlayerPrefs.GetString(BindingOverridesKey);

            PlayerPrefs.DeleteKey(SensitivityKey);
            PlayerPrefs.DeleteKey(ViewDistanceKey);
            PlayerPrefs.DeleteKey(AutosaveIntervalKey);
            PlayerPrefs.DeleteKey(BindingOverridesKey);
            InputSystem.actions?.RemoveAllBindingOverrides();
            ResetSettingsState();
        }

        [TearDown]
        public void TearDown()
        {
            RestoreFloat(SensitivityKey, _hadSensitivity, _originalSensitivity);
            RestoreInt(ViewDistanceKey, _hadViewDistance, _originalViewDistance);
            RestoreInt(AutosaveIntervalKey, _hadAutosaveInterval, _originalAutosaveInterval);
            RestoreString(BindingOverridesKey, _hadOverrides, _originalOverrides);
            PlayerPrefs.Save();

            InputSystem.actions?.RemoveAllBindingOverrides();
            ResetSettingsState();
            GameSettings.EnsureLoaded();
        }

        [Test]
        public void DefaultsMatchExistingGameplayValues()
        {
            GameSettings.EnsureLoaded();
            Assert.AreEqual(0.3f, GameSettings.Sensitivity);
            Assert.AreEqual(8, GameSettings.ViewDistance);
            Assert.AreEqual(30, GameSettings.AutosaveInterval);
        }

        [Test]
        public void NumericValuesAreClampedAndRoundedToSliderSteps()
        {
            Assert.AreEqual(0.05f, GameSettings.NormalizeSensitivity(-1f));
            Assert.AreEqual(0.55f, GameSettings.NormalizeSensitivity(0.53f));
            Assert.AreEqual(1f, GameSettings.NormalizeSensitivity(4f));
            Assert.AreEqual(30, GameSettings.NormalizeAutosave(26));
            Assert.AreEqual(10, GameSettings.NormalizeAutosave(-1));
            Assert.AreEqual(300, GameSettings.NormalizeAutosave(999));
        }

        [Test]
        public void ApplyPersistsNumbersAndPlayerBindingOverride()
        {
            GameSettings.EnsureLoaded();
            InputActionAsset draft = GameSettings.CreateDraftActions();
            try
            {
                InputAction pause = draft.FindActionMap("Player", true).FindAction("Pause", true);
                pause.ApplyBindingOverride(0, "<Keyboard>/f10");
                Assert.AreEqual("<Keyboard>/escape", InputSystem.actions.FindActionMap("Player", true)
                    .FindAction("Pause", true).bindings[0].effectivePath,
                    "Editing the draft must not change live bindings before Apply.");

                GameSettings.Apply(0.53f, 99, 26, draft);

                Assert.AreEqual(0.55f, PlayerPrefs.GetFloat(SensitivityKey));
                Assert.AreEqual(12, PlayerPrefs.GetInt(ViewDistanceKey));
                Assert.AreEqual(30, PlayerPrefs.GetInt(AutosaveIntervalKey));
                Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerPrefs.GetString(BindingOverridesKey)));
                Assert.AreEqual("<Keyboard>/f10", InputSystem.actions.FindActionMap("Player", true)
                    .FindAction("Pause", true).bindings[0].effectivePath);
            }
            finally
            {
                Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void InvalidPersistedValuesFallBackOrClampSafely()
        {
            PlayerPrefs.SetFloat(SensitivityKey, float.NaN);
            PlayerPrefs.SetInt(ViewDistanceKey, -100);
            PlayerPrefs.SetInt(AutosaveIntervalKey, 999);
            PlayerPrefs.SetString(BindingOverridesKey, "not valid binding json");

            GameSettings.EnsureLoaded();

            Assert.AreEqual(GameSettings.DefaultSensitivity, GameSettings.Sensitivity);
            Assert.AreEqual(GameSettings.MinimumViewDistance, GameSettings.ViewDistance);
            Assert.AreEqual(GameSettings.MaximumAutosaveInterval, GameSettings.AutosaveInterval);
            Assert.IsFalse(PlayerPrefs.HasKey(BindingOverridesKey));
        }

        [Test]
        public void RemovedGeneratedActionsAreAbsentButHotbarActionsRemain()
        {
            InputActionMap player = InputSystem.actions.FindActionMap("Player", true);
            Assert.IsNull(player.FindAction("Crouch"));
            Assert.IsNull(player.FindAction("Previous"));
            Assert.IsNull(player.FindAction("Next"));
            for (int i = 1; i <= 9; i++) Assert.IsNotNull(player.FindAction(i.ToString()));
        }

        [Test]
        public void BindingBlacklistExcludesOnlyInternalOrFixedControls()
        {
            Assert.IsFalse(GameSettings.IsBindingConfigurable("Look"));
            Assert.IsFalse(GameSettings.IsBindingConfigurable("Pause"));
            Assert.IsFalse(GameSettings.IsBindingConfigurable("Scroll"));
            Assert.IsFalse(GameSettings.IsBindingConfigurable("SprintPending"));
            Assert.IsTrue(GameSettings.IsBindingConfigurable("Move"));
            Assert.IsTrue(GameSettings.IsBindingConfigurable("Jump"));
        }

        [Test]
        public void SprintPendingTracksPrimaryForwardBinding()
        {
            GameSettings.EnsureLoaded();
            InputActionAsset draft = GameSettings.CreateDraftActions(true);
            try
            {
                InputActionMap player = draft.FindActionMap("Player", true);
                InputAction move = player.FindAction("Move", true);
                InputAction sprintPending = player.FindAction("SprintPending", true);
                int primaryForward = -1;
                for (int i = 0; i < move.bindings.Count; i++)
                {
                    if (move.bindings[i].isPartOfComposite && move.bindings[i].name == "up" &&
                        move.bindings[i].path.StartsWith("<Keyboard>"))
                    {
                        primaryForward = i;
                        break;
                    }
                }

                Assert.GreaterOrEqual(primaryForward, 0);
                move.ApplyBindingOverride(primaryForward, "<Keyboard>/t");
                GameSettings.SynchronizeDerivedBindings(draft);
                Assert.AreEqual("<Keyboard>/t", sprintPending.bindings[0].effectivePath);
            }
            finally
            {
                Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void FullBindingDisplayNameMakesSpaceVisible()
        {
            InputAction jump = InputSystem.actions.FindActionMap("Player", true).FindAction("Jump", true);
            Assert.AreEqual("Space", GameSettings.GetBindingDisplayName(jump, 0));
        }

        private static void ResetSettingsState()
        {
            typeof(GameSettings).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
            typeof(GameSettings).GetField("_actions", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        }

        private static void RestoreFloat(string key, bool existed, float value)
        {
            if (existed) PlayerPrefs.SetFloat(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        private static void RestoreInt(string key, bool existed, int value)
        {
            if (existed) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        private static void RestoreString(string key, bool existed, string value)
        {
            if (existed) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
    }
}
