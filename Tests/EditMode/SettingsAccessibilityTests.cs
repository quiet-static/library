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
