using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Saving;
using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.Spawning;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class SaveRestorationFailureTests
    {
        private readonly List<GameObject> objects = new();
        private readonly List<string> checkpoints = new();
        private Scene originalScene;
        private Scene destination;
        private Scene additionalScene;

        private sealed class ThrowingParticipant : MonoBehaviour, ISaveParticipant
        {
            public string SaveId => "test.throwing-participant";
            public bool FailDuringRollback { get; set; }
            public string CaptureSaveState() => "previous";
            public void RestoreSaveState(string json)
            {
                if (FailDuringRollback || json != "previous")
                    throw new InvalidOperationException("Expected participant failure.");
            }
        }

        private sealed class StatefulParticipant : MonoBehaviour, ISaveParticipant
        {
            public string SaveId { get; set; } = "test.stateful-participant";
            public string State { get; private set; } = "previous";
            public int RestoreCount { get; private set; }
            public Action<string> AfterRestore { get; set; }
            public string CaptureSaveState() => State;

            public void RestoreSaveState(string json)
            {
                State = json;
                RestoreCount++;
                AfterRestore?.Invoke(json);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject instance in objects)
                UnityEngine.Object.Destroy(instance);
            objects.Clear();
            foreach (string path in checkpoints)
                File.Delete(path);
            checkpoints.Clear();
            if (destination.IsValid() && destination.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
                yield return SceneManager.UnloadSceneAsync(destination);
            }
            if (additionalScene.IsValid() && additionalScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(additionalScene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReentrantLoadFromStartProducesOneLifecycleResult()
        {
            SaveManager manager = CreateManager(out string prefix);
            WriteCheckpoint(prefix, 0);
            WriteCheckpoint(prefix, 1);
            int starts = 0;
            bool wasLoadingAtStart = false;
            var completions = new List<(int slot, bool succeeded)>();
            manager.LoadStarted += () =>
            {
                if (++starts != 1) return;
                wasLoadingAtStart = manager.IsLoading;
                Assert.That(QuietStatic.Toolkit.Dialogue.DialogueEventPlayer.TriggerStartsEnabled, Is.False);
                manager.LoadSlot(1);
            };
            manager.LoadCompleted += (slot, succeeded, _) => completions.Add((slot, succeeded));

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: A save slot is already being loaded.*slot 1"));
            manager.LoadSlot(0);
            yield return null;

            Assert.That(wasLoadingAtStart, Is.True);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(new[] { (0, true) }));
            Assert.That(manager.IsLoading, Is.False);
            Assert.That(QuietStatic.Toolkit.Dialogue.DialogueEventPlayer.TriggerStartsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ThrowingLoadStartedListenerReleasesLockAndAllowsRetry()
        {
            SaveManager manager = CreateManager(out string prefix);
            WriteCheckpoint(prefix, 0);
            var completions = new List<bool>();
            Action throwingListener = () => throw new InvalidOperationException("Expected start failure.");
            manager.LoadStarted += throwingListener;
            manager.LoadCompleted += (_, succeeded, _) => completions.Add(succeeded);

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: Could not start the load: Expected start failure"));
            manager.LoadSlot(0);
            Assert.That(manager.IsLoading, Is.False);
            Assert.That(completions, Is.EqualTo(new[] { false }));

            manager.LoadStarted -= throwingListener;
            manager.LoadSlot(0);
            yield return null;
            Assert.That(completions, Is.EqualTo(new[] { false, true }));
        }

        [UnityTest]
        public IEnumerator SaveUsesItsOwnTransitionResultWhenAnotherRequestOverwritesLastResult()
        {
            originalScene = SceneManager.GetActiveScene();
            destination = SceneManager.CreateScene($"Checkpoint Destination {Guid.NewGuid():N}");
            SaveManager manager = CreateManager(out string prefix);
            WriteCheckpoint(prefix, 0, destination.name);
            SceneFlowManager flow = CreateFlowManager();
            var rejectedRequest = new SceneTransitionRequest(string.Empty);
            flow.TransitionCompleted += _ => flow.TransitionToScene(rejectedRequest);
            var completions = new List<bool>();
            manager.LoadCompleted += (_, succeeded, _) => completions.Add(succeeded);

            LogAssert.Expect(LogType.Warning, new Regex("SceneFlowManager cannot transition to an empty scene name"));
            manager.LoadSlot(0);
            for (int frame = 0; frame < 20 && manager.IsLoading; frame++)
                yield return null;

            Assert.That(completions, Is.EqualTo(new[] { true }));
            Assert.That(flow.LastTransitionResult?.Request, Is.SameAs(rejectedRequest));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(destination));
        }

        [UnityTest]
        public IEnumerator LoadDuringUnrelatedTransitionLeavesThatTransitionRunning()
        {
            originalScene = SceneManager.GetActiveScene();
            destination = SceneManager.CreateScene($"Transition Destination {Guid.NewGuid():N}");
            SaveManager manager = CreateManager(out string prefix);
            WriteCheckpoint(prefix, 0, destination.name);
            SceneFlowManager flow = CreateFlowManager();
            bool stillTransitioning = false;
            var completions = new List<bool>();
            manager.LoadCompleted += (_, succeeded, _) => completions.Add(succeeded);
            flow.TransitionStarted += _ =>
            {
                manager.LoadSlot(0);
                stillTransitioning = flow.IsTransitioning;
            };

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: Wait for the current scene transition to finish"));
            yield return flow.TransitionToSceneRoutine(new SceneTransitionRequest(destination.name, unloadOtherScenes: false));

            Assert.That(stillTransitioning, Is.True);
            Assert.That(flow.LastTransitionResult?.Succeeded, Is.True);
            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(manager.IsLoading, Is.False);
        }

        [UnityTest]
        public IEnumerator ParticipantException_ReleasesLoadingLockAndReportsOnce()
        {
            string prefix = $"restore_failure_{Guid.NewGuid():N}_";
            var managerObject = new GameObject("Save Manager Failure Test");
            var participantObject = new GameObject("Throwing Save Participant");
            SaveManager manager = managerObject.AddComponent<SaveManager>();
            participantObject.AddComponent<ThrowingParticipant>().FailDuringRollback = true;
            SetField(manager, "filePrefix", prefix);

            int loaded = 0;
            int failed = 0;
            string failureMessage = string.Empty;
            SetField(manager, "onLoaded", CreateSlotEvent(_ => loaded++));
            SetField(manager, "onLoadFailed", CreateErrorEvent((_, message) =>
            {
                failed++;
                failureMessage = message;
            }));

            string path = Path.Combine(
                Application.persistentDataPath,
                $"{prefix}0.json");
            var data = new SaveGameData
            {
                activeScene = string.Empty,
                participants = new List<SaveParticipantData>
                {
                    new("test.throwing-participant", "{}"),
                },
            };
            File.WriteAllText(path, JsonUtility.ToJson(data));
            LogAssert.Expect(
                LogType.Warning,
                new Regex(
                    "LoadSlot: Save participant 'test[.]throwing-participant' " +
                    "failed to restore: Expected participant failure[.]"));

            manager.LoadSlot(0);
            yield return null;

            Assert.That(manager.IsLoading, Is.False);
            Assert.That(loaded, Is.Zero);
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(failureMessage, Does.Contain("test.throwing-participant"));
            Assert.That(manager.LastLoadRecoveryIncomplete, Is.True);

            manager.DeleteSlot(0);
            UnityEngine.Object.Destroy(managerObject);
            UnityEngine.Object.Destroy(participantObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ParticipantFailureRestoresActiveSceneAndEarlierParticipantState()
        {
            originalScene = SceneManager.GetActiveScene();
            destination = SceneManager.CreateScene($"Failed Restore Destination {Guid.NewGuid():N}");
            additionalScene = SceneManager.CreateScene($"Existing Support {Guid.NewGuid():N}");
            SaveManager manager = CreateManager(out string prefix);
            CreateFlowManager();
            StatefulParticipant previous = CreateObject("Persistent Participant", originalScene).AddComponent<StatefulParticipant>();
            CreateObject("Failing Destination Participant", destination).AddComponent<ThrowingParticipant>();
            string checkpoint = WriteCheckpoint(prefix, 0, new SaveGameData
            {
                activeScene = destination.name,
                participants = new List<SaveParticipantData>
                {
                    new(previous.SaveId, "saved"),
                    new("test.throwing-participant", "fail")
                }
            });
            string savedContent = File.ReadAllText(checkpoint);
            var completions = new List<(bool succeeded, Scene activeScene)>();
            manager.LoadCompleted += (_, succeeded, _) => completions.Add((succeeded, SceneManager.GetActiveScene()));

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: Save participant 'test[.]throwing-participant' failed to restore"));
            manager.LoadSlot(0);
            yield return WaitForLoad(manager);

            Assert.That(completions, Is.EqualTo(new[] { (false, originalScene) }));
            Assert.That(previous.State, Is.EqualTo("previous"));
            Assert.That(previous.RestoreCount, Is.EqualTo(2));
            Assert.That(manager.LastLoadRecoveryIncomplete, Is.False);
            Assert.That(destination.isLoaded, Is.True, "A scene already loaded before the request remains loaded.");
            Assert.That(additionalScene.isLoaded, Is.True);
            Assert.That(File.ReadAllText(checkpoint), Is.EqualTo(savedContent));
        }

        [UnityTest]
        public IEnumerator MissingDestinationSpawnDoesNotUseRetiringSceneOrApplyParticipants()
        {
            originalScene = SceneManager.GetActiveScene();
            destination = SceneManager.CreateScene($"Missing Spawn Destination {Guid.NewGuid():N}");
            additionalScene = SceneManager.CreateScene($"Retiring Content {Guid.NewGuid():N}");
            SaveManager manager = CreateManager(out string prefix);
            CreateFlowManager();
            const string spawnId = "test.arrival";
            var spawnManager = CreateObject("Test Spawn Manager", originalScene).AddComponent<SpawnManager>();
            var target = CreateObject("Registered Player", originalScene);
            spawnManager.RegisterTarget("Player", target.transform);
            var retiringSpawn = CreateObject("Old Spawn", additionalScene).AddComponent<SpawnPoint>();
            SetField(retiringSpawn, "id", spawnId);
            StatefulParticipant previous = CreateObject("Persistent Participant", originalScene).AddComponent<StatefulParticipant>();
            var data = new SaveGameData
            {
                activeScene = destination.name,
                arrivalSpawnId = spawnId,
                participants = new List<SaveParticipantData> { new(previous.SaveId, "saved") }
            };
            WriteCheckpoint(prefix, 0, data);
            var completions = new List<bool>();
            manager.LoadCompleted += (_, succeeded, _) => completions.Add(succeeded);

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: Spawn restoration failed: The saved spawn 'test[.]arrival' is unavailable"));
            manager.LoadSlot(0);
            yield return WaitForLoad(manager);

            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
            Assert.That(previous.RestoreCount, Is.Zero);
            Assert.That(additionalScene.isLoaded, Is.True);

            var destinationSpawn = CreateObject("Destination Spawn", destination).AddComponent<SpawnPoint>();
            SetField(destinationSpawn, "id", spawnId);
            destinationSpawn.transform.position = new Vector3(3f, 1f, 5f);
            manager.LoadSlot(0);
            yield return WaitForLoad(manager);

            Assert.That(completions, Is.EqualTo(new[] { false, true }));
            Assert.That(previous.State, Is.EqualTo("saved"));
            Assert.That(target.transform.position, Is.EqualTo(destinationSpawn.transform.position));
            Assert.That(additionalScene.isLoaded, Is.False);
        }

        private static IEnumerator WaitForLoad(SaveManager manager)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && manager.IsLoading)
                yield return null;
            Assert.That(manager.IsLoading, Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingManagerDuringParticipantRestoreRollsBackAndAllowsRetry()
        {
            originalScene = SceneManager.GetActiveScene();
            destination = SceneManager.CreateScene($"Cancelled Destination {Guid.NewGuid():N}");
            SaveManager manager = CreateManager(out string prefix);
            CreateFlowManager();
            var first = CreateObject("First Participant", originalScene).AddComponent<StatefulParticipant>();
            var second = CreateObject("Second Participant", originalScene).AddComponent<StatefulParticipant>();
            second.SaveId = "test.second-participant";
            var completions = new List<bool>();
            manager.LoadCompleted += (_, succeeded, _) => completions.Add(succeeded);
            first.AfterRestore = state =>
            {
                if (state == "saved")
                    manager.enabled = false;
            };
            WriteCheckpoint(prefix, 0, new SaveGameData
            {
                activeScene = destination.name,
                participants = new List<SaveParticipantData>
                {
                    new(first.SaveId, "saved"),
                    new(second.SaveId, "saved")
                }
            });

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: Save restoration was cancelled"));
            manager.LoadSlot(0);
            yield return WaitForLoad(manager);

            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
            Assert.That(first.State, Is.EqualTo("previous"));
            Assert.That(first.RestoreCount, Is.EqualTo(2));
            Assert.That(second.RestoreCount, Is.Zero);
            Assert.That(manager.LastLoadRecoveryIncomplete, Is.False);

            first.AfterRestore = null;
            manager.enabled = true;
            manager.LoadSlot(0);
            yield return WaitForLoad(manager);
            Assert.That(completions, Is.EqualTo(new[] { false, true }));
            Assert.That(second.State, Is.EqualTo("saved"));
        }

        [UnityTest]
        public IEnumerator DestroyingManagerWhileLoadWaitsCompletesRollbackAndReleasesGlobalLock()
        {
            originalScene = SceneManager.GetActiveScene();
            destination = SceneManager.CreateScene($"Destroy Save Owner Destination {Guid.NewGuid():N}");
            SaveManager manager = CreateManager(out string prefix);
            SceneFlowManager flow = CreateFlowManager();
            var participant = CreateObject("Unapplied Participant", originalScene).AddComponent<StatefulParticipant>();
            WriteCheckpoint(prefix, 0, new SaveGameData
            {
                activeScene = destination.name,
                arrivalSpawnId = "wait-before-spawn",
                participants = new List<SaveParticipantData> { new(participant.SaveId, "saved") }
            });
            GameObject faderOwner = CreateObject("Transition Fader", originalScene);
            faderOwner.AddComponent<CanvasGroup>();
            var fader = faderOwner.AddComponent<ScreenFader>();
            SetField(fader, "fadeToBlackDuration", 0f);
            SetField(fader, "fadeToClearDuration", 0f);
            SetField(flow, "screenFader", fader);
            SetField(flow, "fadeDuringTransitions", true);
            SetField(flow, "blackHoldDuration", 0.05f);
            flow.TransitionStarted += _ => UnityEngine.Object.Destroy(manager.gameObject);
            var completions = new List<(bool succeeded, bool ownerDestroyed)>();
            manager.LoadCompleted += (_, succeeded, _) => completions.Add((succeeded, manager == null));

            LogAssert.Expect(LogType.Warning, new Regex("LoadSlot: Save restoration was cancelled"));
            manager.LoadSlot(0);
            yield return WaitForLoad(manager);

            Assert.That(manager == null, Is.True);
            Assert.That(completions, Is.EqualTo(new[] { (false, true) }));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
            Assert.That(participant.RestoreCount, Is.Zero);
            Assert.That(flow.IsTransitioning, Is.False);
            var replacement = CreateManager(out string replacementPrefix);
            WriteCheckpoint(replacementPrefix, 0);
            replacement.LoadCompleted += (_, succeeded, _) => completions.Add((succeeded, true));
            replacement.LoadSlot(0);
            yield return WaitForLoad(replacement);
            Assert.That(completions, Is.EqualTo(new[] { (false, true), (true, true) }));
        }

        private static SaveManager.SlotUnityEvent CreateSlotEvent(Action<int> listener)
        {
            var result = new SaveManager.SlotUnityEvent();
            result.AddListener(listener.Invoke);
            return result;
        }

        private SaveManager CreateManager(out string prefix)
        {
            prefix = $"restore_lifecycle_{Guid.NewGuid():N}_";
            var owner = new GameObject("Save Lifecycle Test");
            objects.Add(owner);
            var manager = owner.AddComponent<SaveManager>();
            SetField(manager, "filePrefix", prefix);
            return manager;
        }

        private GameObject CreateObject(string name, Scene scene)
        {
            var owner = new GameObject(name);
            objects.Add(owner);
            SceneManager.MoveGameObjectToScene(owner, scene);
            return owner;
        }

        private SceneFlowManager CreateFlowManager()
        {
            var owner = new GameObject("Save Test Scene Flow Manager");
            objects.Add(owner);
            var flow = owner.AddComponent<SceneFlowManager>();
            flow.ConfigurePersistentScenes(new[] { originalScene.name });
            SetField(flow, "fadeDuringTransitions", false);
            return flow;
        }

        private void WriteCheckpoint(string prefix, int slot, string sceneName = "") =>
            WriteCheckpoint(prefix, slot, new SaveGameData { activeScene = sceneName });

        private string WriteCheckpoint(string prefix, int slot, SaveGameData data)
        {
            string path = Path.Combine(Application.persistentDataPath, $"{prefix}{slot}.json");
            checkpoints.Add(path);
            File.WriteAllText(path, JsonUtility.ToJson(data));
            return path;
        }

        private static SaveManager.SaveErrorUnityEvent CreateErrorEvent(
            Action<int, string> listener)
        {
            var result = new SaveManager.SaveErrorUnityEvent();
            result.AddListener(listener.Invoke);
            return result;
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
