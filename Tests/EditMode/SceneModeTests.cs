using System;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SceneModeTests
    {
        private GameObject root;
        private GameObject gameStateObject;
        private GameStateManager previousGameStateInstance;
        private Action<SceneMode> modeChangedHandler;

        [SetUp]
        public void SetUp()
        {
            ResetSceneModeStatics();
            previousGameStateInstance = GameStateManager.Instance;
            SetGameStateManagerInstance(null);
            root = new GameObject("Scene Root");
        }

        [TearDown]
        public void TearDown()
        {
            SceneModeManager.OnSceneModeChanged -= modeChangedHandler;
            modeChangedHandler = null;
            UnityEngine.Object.DestroyImmediate(root);
            if (gameStateObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameStateObject);
            }
            SetGameStateManagerInstance(previousGameStateInstance);
            previousGameStateInstance = null;
            ResetSceneModeStatics();
        }

        [Test]
        public void FindDefinition_FindsInactiveDescendantInLoadedScene()
        {
            GameObject child = new GameObject("Inactive Definition");
            child.transform.SetParent(root.transform);
            SceneModeDefinition definition = child.AddComponent<SceneModeDefinition>();
            child.SetActive(false);

            Assert.That(
                SceneModeManager.FindDefinition(root.scene),
                Is.SameAs(definition));
        }

        [Test]
        public void FindDefinition_RejectsInvalidScene()
        {
            Assert.That(SceneModeManager.FindDefinition(default), Is.Null);
        }

        [Test]
        public void CameraHandler_UsesLocalDefinitionWhenManagerModeIsUnspecified()
        {
            SceneModeDefinition definition = root.AddComponent<SceneModeDefinition>();
            SetField(definition, "mode", SceneMode.Cutscene);
            Camera camera = root.AddComponent<Camera>();
            AudioListener listener = root.AddComponent<AudioListener>();
            SceneModeCameraHandler handler = root.AddComponent<SceneModeCameraHandler>();
            SetField(handler, "activeMode", SceneMode.Cutscene);

            handler.Refresh();

            Assert.That(camera.enabled, Is.True);
            Assert.That(listener.enabled, Is.True);

            SetField(handler, "activeMode", SceneMode.Play);
            handler.Refresh();

            Assert.That(camera.enabled, Is.False);
            Assert.That(listener.enabled, Is.False);
        }

        [Test]
        public void CameraHandler_RespondsToModeChangesAndStopsAfterDisable()
        {
            Camera camera = root.AddComponent<Camera>();
            AudioListener listener = root.AddComponent<AudioListener>();
            SceneModeCameraHandler handler = root.AddComponent<SceneModeCameraHandler>();
            SetField(handler, "activeMode", SceneMode.Cutscene);
            MethodInfo apply = typeof(SceneModeManager).GetMethod(
                "ApplySceneMode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);

            SceneModeDefinition definition = root.AddComponent<SceneModeDefinition>();
            SetField(definition, "mode", SceneMode.Cutscene);
            apply.Invoke(null, new object[] { root.scene });

            Assert.That(camera.enabled, Is.True);
            Assert.That(listener.enabled, Is.True);

            handler.enabled = false;
            SetField(definition, "mode", SceneMode.Play);
            apply.Invoke(null, new object[] { root.scene });

            Assert.That(camera.enabled, Is.True);
            Assert.That(listener.enabled, Is.True);
        }

        [Test]
        public void ApplyingSameModePublishesOnceButAppliesLatestGameState()
        {
            gameStateObject = new GameObject("Game State Manager");
            GameStateManager gameStateManager =
                gameStateObject.AddComponent<GameStateManager>();
            SetGameStateManagerInstance(gameStateManager);
            SceneModeDefinition definition = root.AddComponent<SceneModeDefinition>();
            SetField(definition, "mode", SceneMode.Play);
            SetField(definition, "initialGameState", "Exploring");
            int notificationCount = 0;
            modeChangedHandler = _ => notificationCount++;
            SceneModeManager.OnSceneModeChanged += modeChangedHandler;
            MethodInfo apply = typeof(SceneModeManager).GetMethod(
                "ApplySceneMode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);

            apply.Invoke(null, new object[] { root.scene });
            SetField(definition, "initialGameState", "Combat");
            apply.Invoke(null, new object[] { root.scene });

            Assert.That(SceneModeManager.CurrentMode, Is.EqualTo(SceneMode.Play));
            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(gameStateManager.CurrentState, Is.EqualTo("Combat"));
        }

        private static void ResetSceneModeStatics()
        {
            MethodInfo reset = typeof(SceneModeManager).GetMethod(
                "ResetStatics",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private static void SetGameStateManagerInstance(GameStateManager value)
        {
            FieldInfo field = typeof(GameStateManager).BaseType?.GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing singleton backing field.");
            field.SetValue(null, value);
        }
    }
}
