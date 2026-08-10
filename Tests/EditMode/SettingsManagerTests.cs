using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SettingsManagerTests
    {
        private GameObject managerObject;
        private SettingsManager manager;
        private Action<float> sensitivityChangedHandler;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Settings Manager");
            manager = managerObject.AddComponent<SettingsManager>();
            InvokeLifecycle("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (sensitivityChangedHandler != null)
            {
                SettingsManager.OnMouseSensitivityChanged -=
                    sensitivityChangedHandler;
            }

            if (managerObject != null)
            {
                InvokeLifecycle("OnDestroy");
                UnityEngine.Object.DestroyImmediate(managerObject);
            }

            PlayerPrefs.DeleteKey("Settings_MouseSensitivity");
            PlayerPrefs.DeleteKey("Settings_VSync");
        }

        [Test]
        public void SetMouseSensitivity_UpdatesValueAndPublishesChange()
        {
            float reportedSensitivity = -1f;
            sensitivityChangedHandler =
                sensitivity => reportedSensitivity = sensitivity;
            SettingsManager.OnMouseSensitivityChanged +=
                sensitivityChangedHandler;

            manager.SetMouseSensitivity(0.75f);

            Assert.That(manager.MouseSensitivity, Is.EqualTo(0.75f));
            Assert.That(reportedSensitivity, Is.EqualTo(0.75f));
        }

        [Test]
        public void AccessibilitySetter_UpdatesValueAndPublishesSettingId()
        {
            GameSettingId? reported = null;
            void Handle(GameSettingId setting) => reported = setting;
            SettingsManager.OnSettingChanged += Handle;
            try
            {
                manager.SetReducedFlashing(true);
                Assert.That(manager.ReducedFlashingEnabled, Is.True);
                Assert.That(reported, Is.EqualTo(GameSettingId.ReducedFlashing));
            }
            finally
            {
                SettingsManager.OnSettingChanged -= Handle;
                PlayerPrefs.DeleteKey("Settings_ReducedFlashing");
            }
        }

        [Test]
        public void InteractionModeSetter_PersistsTogglePreference()
        {
            manager.SetInteractionInputMode((int)InteractionInputMode.Toggle);
            Assert.That(manager.InteractionMode, Is.EqualTo(InteractionInputMode.Toggle));
            Assert.That(PlayerPrefs.GetInt("Settings_InteractionInputMode"), Is.EqualTo(1));
            PlayerPrefs.DeleteKey("Settings_InteractionInputMode");
        }

        [Test]
        public void DestroyingManager_ClearsSingletonInstance()
        {
            Assert.That(SettingsManager.Instance, Is.SameAs(manager));

            InvokeLifecycle("OnDestroy");
            UnityEngine.Object.DestroyImmediate(managerObject);
            managerObject = null;

            Assert.That(SettingsManager.Instance, Is.Null);
        }

        [Test]
        public void Start_WithNoUiReferences_LoadsSettingsWithoutThrowing()
        {
            PlayerPrefs.SetFloat("Settings_MouseSensitivity", 0.65f);
            PlayerPrefs.SetInt(
                "Settings_VSync",
                QualitySettings.vSyncCount > 0 ? 1 : 0
            );
            typeof(SettingsManager)
                .GetField(
                    "resolutionOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                .SetValue(
                    manager,
                    Array.Empty<SettingsManager.ResolutionOption>()
                );
            LogAssert.Expect(
                LogType.Warning,
                "SettingsManager could not find any Volume in the scene."
            );
            LogAssert.Expect(
                LogType.Warning,
                "SettingsManager cannot apply brightness because " +
                "ColorAdjustments is null."
            );

            Assert.DoesNotThrow(() => InvokeLifecycle("Start"));
            Assert.That(manager.MouseSensitivity, Is.EqualTo(0.65f));
        }

        private void InvokeLifecycle(string methodName)
        {
            typeof(SettingsManager)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                .Invoke(manager, null);
        }
    }
}
