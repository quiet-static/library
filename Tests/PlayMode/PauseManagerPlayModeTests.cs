using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class PauseManagerPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new();
        private CursorLockMode originalCursorLock;
        private bool originalCursorVisible;

        [SetUp]
        public void CaptureCursorState()
        {
            originalCursorLock = Cursor.lockState;
            originalCursorVisible = Cursor.visible;
        }

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
            Cursor.lockState = originalCursorLock;
            Cursor.visible = originalCursorVisible;
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

        [UnityTest]
        public IEnumerator NarrativePause_RestoresInterruptedModeAndTimeScale()
        {
            CreateManagers("Dialogue", pauseTimeScale: true,
                out GameStateManager gameState, out PauseManager pauseManager);
            SetField(pauseManager, "additionalPausableStates", new[] { "Dialogue", "Cutscene" });
            foreach (string state in new[] { "Dialogue", "Cutscene" })
            {
                gameState.SetState(state);
                Time.timeScale = .65f;
                pauseManager.PauseGame();
                Assert.That(pauseManager.IsPaused, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                yield return null;
                pauseManager.ResumeGame();
                Assert.That(gameState.CurrentState, Is.EqualTo(state));
                Assert.That(Time.timeScale, Is.EqualTo(.65f));
            }
        }

        [UnityTest]
        public IEnumerator OverlappingPauseAndLoadRestoreTimeAfterBothRelease()
        {
            CreateManagers("Playing", pauseTimeScale: true, out _, out PauseManager pauseManager);
            Time.timeScale = 0.65f;

            pauseManager.PauseGame();
            using (TimeScaleFreeze load = TimeScaleFreeze.Acquire())
            {
                pauseManager.ResumeGame();
                Assert.That(Time.timeScale, Is.Zero);
            }
            Assert.That(Time.timeScale, Is.EqualTo(0.65f));

            using (TimeScaleFreeze load = TimeScaleFreeze.Acquire())
            {
                pauseManager.PauseGame();
            }
            Assert.That(Time.timeScale, Is.Zero);

            pauseManager.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(0.65f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForceResumeSetsNormalTimeAfterOtherFreezeReleases()
        {
            CreateManagers("Playing", pauseTimeScale: true, out _, out PauseManager pauseManager);
            Time.timeScale = 0.65f;
            pauseManager.PauseGame();

            using (TimeScaleFreeze load = TimeScaleFreeze.Acquire())
            {
                pauseManager.ForceResumeTime();
                Assert.That(Time.timeScale, Is.Zero);
            }
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClosingDialogueWhilePausedKeepsCursorAvailableUntilResume()
        {
            CreateManagers("Playing", pauseTimeScale: false, out _, out PauseManager pauseManager, manageCursor: true);
            DialogueUIManager dialogue = CreateDialogueUi();

            dialogue.ShowDialogueUI();
            pauseManager.PauseGame();
            dialogue.HideDialogueUI();
            Assert.That(Cursor.visible, Is.True);

            pauseManager.ResumeGame();
            Assert.That(Cursor.visible, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExternalStateChangeReleasesPauseOwnershipWhileDialogueRetainsItsClaim()
        {
            CreateManagers("Playing", pauseTimeScale: false, out GameStateManager gameState,
                out PauseManager pauseManager, manageCursor: true);
            DialogueUIManager dialogue = CreateDialogueUi();
            SetField(pauseManager, "pausedState", " Paused ");

            pauseManager.PauseGame();
            dialogue.ShowDialogueUI();
            gameState.SetState("Cutscene");
            Assert.That(Cursor.visible, Is.True);

            dialogue.enabled = false;
            Assert.That(Cursor.visible, Is.False);
            yield return null;
        }

        private DialogueUIManager CreateDialogueUi()
        {
            GameObject owner = Track(new GameObject("Dialogue UI"));
            owner.SetActive(false);
            var dialogue = owner.AddComponent<DialogueUIManager>();
            SetField(dialogue, "hiddenCursorLockMode", CursorLockMode.None);
            owner.SetActive(true);
            return dialogue;
        }

        private void CreateManagers(
            string startingState,
            bool pauseTimeScale,
            out GameStateManager gameState,
            out PauseManager pauseManager,
            bool manageCursor = false)
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
            SetField(pauseManager, "manageCursor", manageCursor);
            SetField(pauseManager, "gameplayCursorLockMode", CursorLockMode.None);
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
