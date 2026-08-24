using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.Saving;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class SaveRestorationFailureTests
    {
        private sealed class ThrowingParticipant : MonoBehaviour, ISaveParticipant
        {
            public string SaveId => "test.throwing-participant";
            public string CaptureSaveState() => "{}";
            public void RestoreSaveState(string json) =>
                throw new InvalidOperationException("Expected participant failure.");
        }

        [UnityTest]
        public IEnumerator ParticipantException_ReleasesLoadingLockAndReportsOnce()
        {
            string prefix = $"restore_failure_{Guid.NewGuid():N}_";
            var managerObject = new GameObject("Save Manager Failure Test");
            var participantObject = new GameObject("Throwing Save Participant");
            SaveManager manager = managerObject.AddComponent<SaveManager>();
            participantObject.AddComponent<ThrowingParticipant>();
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

            manager.DeleteSlot(0);
            UnityEngine.Object.Destroy(managerObject);
            UnityEngine.Object.Destroy(participantObject);
            yield return null;
        }

        private static SaveManager.SlotUnityEvent CreateSlotEvent(Action<int> listener)
        {
            var result = new SaveManager.SlotUnityEvent();
            result.AddListener(listener.Invoke);
            return result;
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
