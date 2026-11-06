using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Settings;
using QuietStatic.Toolkit.UI.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SettingsAccessibilityTests
    {
        private GameObject managerObject;
        private SettingsManager manager;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Settings");
            manager = managerObject.AddComponent<SettingsManager>();
            Invoke(manager, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Invoke(manager, "OnDestroy");
            Object.DestroyImmediate(managerObject);
            PlayerPrefs.DeleteKey("Settings_InputBindingOverrides");
            PlayerPrefs.DeleteKey("Settings_ClosedCaptions");
            PlayerPrefs.DeleteKey("Settings_ReducedFlashing");
            PlayerPrefs.DeleteKey("Settings_ReducedCameraMotion");
        }

        [Test]
        public void SettingsMenu_InitializesReadoutsAndRefreshesWithoutChangingPreferences()
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.quietstatic.core/Runtime/UI/Prefabs/SettingsMenu.prefab");
            GameObject menu = Object.Instantiate(prefab);
            try
            {
                SettingsMenuView view = menu.GetComponent<SettingsMenuView>();
                Invoke(view, "Awake");
                UnityEngine.UI.Slider slider = menu.transform.Find("Master volume/Control")
                    .GetComponent<UnityEngine.UI.Slider>();
                TMPro.TMP_Text readout = menu.transform.Find("Master volume/Value")
                    .GetComponent<TMPro.TMP_Text>();
                int callbacks = 0;
                slider.onValueChanged.AddListener(_ => callbacks++);
                view.Refresh();
                Assert.That(slider.value, Is.EqualTo(1f));
                Assert.That(readout.text, Is.EqualTo("100%"));
                Assert.That(callbacks, Is.Zero);
                slider.value = 0.42f;
                Assert.That(manager.MasterVolume, Is.EqualTo(1f), "Editing must not apply settings.");
                Assert.That(readout.text, Is.EqualTo("42%"));
                menu.SetActive(false);
                menu.SetActive(true);
                Invoke(view, "OnEnable");
                view.Refresh();
                Assert.That(slider.value, Is.EqualTo(manager.MasterVolume));
                Assert.That(menu.transform.Find("Master volume/Value"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(menu); }
        }

        [Test]
        public void SettingsMenu_ApplyCommitsAllStagedValuesAndBackDiscardsEdits()
        {
            string[] keys = { "Settings_MasterVolume", "Settings_MusicVolume", "Settings_SfxVolume", "Settings_MouseSensitivity", "Settings_VSync", "Settings_ResolutionIndex" };
            bool[] existed = System.Array.ConvertAll(keys, PlayerPrefs.HasKey);
            float[] previous = System.Array.ConvertAll(keys, key => PlayerPrefs.GetFloat(key));
            int previousResolution = PlayerPrefs.GetInt("Settings_ResolutionIndex");
            int previousVSync = PlayerPrefs.GetInt("Settings_VSync");
            int previousQualityVSync = QualitySettings.vSyncCount;
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.quietstatic.core/Runtime/UI/Prefabs/SettingsMenu.prefab");
            GameObject menu = Object.Instantiate(prefab);
            try
            {
                SettingsMenuView view = menu.GetComponent<SettingsMenuView>();
                var resolutionObject = new GameObject("Test Resolution", typeof(RectTransform), typeof(TMPro.TMP_Dropdown));
                resolutionObject.transform.SetParent(menu.transform, false);
                var resolution = resolutionObject.GetComponent<TMPro.TMP_Dropdown>();
                Set(view, "resolution", resolution);
                Invoke(view, "Awake");
                Invoke(view, "OnEnable");
                var master = menu.transform.Find("Master volume/Control").GetComponent<UnityEngine.UI.Slider>();
                var music = menu.transform.Find("Music volume/Control").GetComponent<UnityEngine.UI.Slider>();
                var apply = menu.transform.Find("Apply Changes").GetComponent<UnityEngine.UI.Button>();
                Assert.That(apply.interactable, Is.False);
                master.value = 0.3f;
                music.value = 0.7f;
                resolution.value = 2;
                Assert.That(manager.ResolutionIndex, Is.Zero);
                Assert.That(manager.MasterVolume, Is.EqualTo(1f));
                Assert.That(manager.MusicVolume, Is.EqualTo(1f));
                Assert.That(apply.interactable, Is.True);
                apply.onClick.Invoke();
                Assert.That(manager.ResolutionIndex, Is.EqualTo(2));
                Assert.That(manager.MasterVolume, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(manager.MusicVolume, Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(PlayerPrefs.GetFloat("Settings_MusicVolume"), Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(apply.interactable, Is.False);
                master.value = 0.9f;
                view.RequestBack();
                Assert.That(manager.MasterVolume, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(master.value, Is.EqualTo(0.3f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(menu);
                for (int index = 0; index < keys.Length; index++)
                {
                    if (!existed[index]) PlayerPrefs.DeleteKey(keys[index]);
                    else if (keys[index] == "Settings_ResolutionIndex") PlayerPrefs.SetInt(keys[index], previousResolution);
                    else if (keys[index] == "Settings_VSync") PlayerPrefs.SetInt(keys[index], previousVSync);
                    else PlayerPrefs.SetFloat(keys[index], previous[index]);
                }
                PlayerPrefs.Save();
                QualitySettings.vSyncCount = previousQualityVSync;
            }
        }

        [Test]
        public void CameraLookUsesTheAppliedSensitivityMultiplier()
        {
            bool existed = PlayerPrefs.HasKey("Settings_MouseSensitivity");
            float previous = PlayerPrefs.GetFloat("Settings_MouseSensitivity");
            var cameraObject = new GameObject("Sensitivity camera");
            try
            {
                var camera = cameraObject.AddComponent<QuietStatic.Toolkit.Cameras.CameraController>();
                manager.SetMouseSensitivity(0.5f);
                var rotate = camera.GetType().GetMethod("HandleRotation", BindingFlags.NonPublic | BindingFlags.Instance);
                var yaw = camera.GetType().GetField("yaw", BindingFlags.NonPublic | BindingFlags.Instance);
                float before = (float)yaw.GetValue(camera);
                rotate.Invoke(camera, new object[] { new Vector2(10f, 0f) });
                float halfSpeed = (float)yaw.GetValue(camera) - before;
                manager.SetMouseSensitivity(1f);
                before = (float)yaw.GetValue(camera);
                rotate.Invoke(camera, new object[] { new Vector2(10f, 0f) });
                Assert.That((float)yaw.GetValue(camera) - before, Is.EqualTo(halfSpeed * 2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                if (existed) PlayerPrefs.SetFloat("Settings_MouseSensitivity", previous);
                else PlayerPrefs.DeleteKey("Settings_MouseSensitivity");
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void RebindingOverrides_RoundTripAndReset()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputAction action = asset.AddActionMap("Player").AddAction("Interact");
            action.AddBinding("<Keyboard>/space");
            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/enter");
                InputRebindControl.SaveOverrides(asset);
                action.RemoveBindingOverride(0);
                InputRebindControl.LoadOverrides(asset);
                Assert.That(action.bindings[0].overridePath, Is.EqualTo("<Keyboard>/enter"));

                InputRebindControl.ResetOverrides(asset);
                Assert.That(action.bindings[0].overridePath, Is.Null.Or.Empty);
                Assert.That(PlayerPrefs.HasKey("Settings_InputBindingOverrides"), Is.False);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void AccessibilityApplier_DisablesConfiguredMotionAndFlashingBehaviours()
        {
            GameObject target = new("Accessibility Target");
            Light flashing = target.AddComponent<Light>();
            Camera motion = target.AddComponent<Camera>();
            AccessibilitySettingsApplier applier = target.AddComponent<AccessibilitySettingsApplier>();
            try
            {
                Set(applier, "flashingEffects", new Behaviour[] { flashing });
                Set(applier, "cameraMotionEffects", new Behaviour[] { motion });
                manager.SetReducedFlashing(true);
                manager.SetReducedCameraMotion(true);
                applier.Apply();
                Assert.That(flashing.enabled, Is.False);
                Assert.That(motion.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void PauseMenuPageNavigation_IsMutuallyExclusive()
        {
            GameObject root = new("Pause");
            GameObject main = new("Main");
            GameObject settings = new("Settings");
            main.transform.SetParent(root.transform);
            settings.transform.SetParent(root.transform);
            PauseMenuView view = root.AddComponent<PauseMenuView>();
            try
            {
                Set(view, "mainPage", main);
                Set(view, "settingsPage", settings);
                view.ShowSettingsPage();
                Assert.That(main.activeSelf, Is.False);
                Assert.That(settings.activeSelf, Is.True);
                view.ShowMainPage();
                Assert.That(main.activeSelf, Is.True);
                Assert.That(settings.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private static void Invoke(object target, string method) =>
            target?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
    }
}
