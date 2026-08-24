using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class PauseManagerPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;

            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.Destroy(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseAndResume_ChangeStateAndTimeScale()
        {
            CreateManagers("Playing", pauseTimeScale: true,
                out GameStateManager gameState,
                out PauseManager pauseManager);

            pauseManager.PauseGame();

            Assert.That(pauseManager.IsPaused, Is.True);
            Assert.That(gameState.CurrentState, Is.EqualTo("Paused"));
            Assert.That(Time.timeScale, Is.Zero);

            pauseManager.PauseGame();
            Assert.That(gameState.CurrentState, Is.EqualTo("Paused"));

            pauseManager.ResumeGame();

            Assert.That(pauseManager.IsPaused, Is.False);
            Assert.That(gameState.CurrentState, Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseGame_IgnoresStateThatDoesNotAllowPausing()
        {
            Time.timeScale = 0.75f;
            CreateManagers("Title", pauseTimeScale: true,
                out GameStateManager gameState,
                out PauseManager pauseManager);

            pauseManager.PauseGame();

            Assert.That(pauseManager.IsPaused, Is.False);
            Assert.That(gameState.CurrentState, Is.EqualTo("Title"));
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledTimeScaleManagement_PreservesExistingScale()
        {
            Time.timeScale = 0.5f;
            CreateManagers("Playing", pauseTimeScale: false,
                out GameStateManager gameState,
                out PauseManager pauseManager);

            pauseManager.PauseGame();
            Assert.That(gameState.CurrentState, Is.EqualTo("Paused"));
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));

            pauseManager.ResumeGame();
            Assert.That(gameState.CurrentState, Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            yield return null;
        }

        private void CreateManagers(
            string startingState,
            bool pauseTimeScale,
            out GameStateManager gameState,
            out PauseManager pauseManager)
        {
            GameObject gameStateObject = Track(new GameObject("Game State Manager"));
            gameStateObject.SetActive(false);
            gameState = gameStateObject.AddComponent<GameStateManager>();
            SetField(gameState, "startingState", startingState);
            gameStateObject.SetActive(true);

            GameObject pauseObject = Track(new GameObject("Pause Manager"));
            pauseObject.SetActive(false);
            pauseManager = pauseObject.AddComponent<PauseManager>();
            SetField(pauseManager, "gameStateManager", gameState);
            SetField(pauseManager, "usePauseScene", false);
            SetField(pauseManager, "pauseTimeScale", pauseTimeScale);
            SetField(pauseManager, "manageCursor", false);
            pauseObject.SetActive(true);
        }

        private GameObject Track(GameObject value)
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
