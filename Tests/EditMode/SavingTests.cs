using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Saving;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SavingTests
    {
        [Test]
        public void SaveGameData_RoundTripsThroughJson()
        {
            var source = new SaveGameData
            {
                savedAtUtc = "2026-07-31T00:00:00.0000000Z",
                activeScene = "House",
                arrivalSpawnId = "FrontDoor",
                activeFlags = new List<string> { "DoorOpen", "MetSam" },
                participants = new List<SaveParticipantData>
                {
                    new SaveParticipantData("clock", "{\"hour\":3}")
                }
            };

            string json = JsonUtility.ToJson(source);
            SaveGameData restored = JsonUtility.FromJson<SaveGameData>(json);

            Assert.That(restored.version, Is.EqualTo(SaveGameData.CurrentVersion));
            Assert.That(restored.activeScene, Is.EqualTo("House"));
            Assert.That(restored.arrivalSpawnId, Is.EqualTo("FrontDoor"));
            Assert.That(restored.activeFlags, Is.EqualTo(source.activeFlags));
            Assert.That(restored.participants[0].id, Is.EqualTo("clock"));
            Assert.That(restored.participants[0].json, Is.EqualTo("{\"hour\":3}"));
        }

        [Test]
        public void SaveRequestChannel_ForwardsAllSlotRequests()
        {
            SaveRequestChannel channel =
                ScriptableObject.CreateInstance<SaveRequestChannel>();
            int savedSlot = -1;
            int loadedSlot = -1;
            int deletedSlot = -1;
            string spawnId = null;

            channel.SaveRequested += (slot, spawn) =>
            {
                savedSlot = slot;
                spawnId = spawn;
            };
            channel.LoadRequested += slot => loadedSlot = slot;
            channel.DeleteRequested += slot => deletedSlot = slot;

            channel.RequestSave(2, "Kitchen");
            channel.RequestLoad(3);
            channel.RequestDelete(4);

            Assert.That(savedSlot, Is.EqualTo(2));
            Assert.That(spawnId, Is.EqualTo("Kitchen"));
            Assert.That(loadedSlot, Is.EqualTo(3));
            Assert.That(deletedSlot, Is.EqualTo(4));

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void SlotMetadata_ParsesRoundTripTimestamp()
        {
            var metadata = new SaveSlotMetadata(
                2,
                1,
                "2026-07-31T06:00:00.0000000Z",
                "House",
                "FrontDoor",
                false);

            Assert.That(metadata.TryGetSavedAt(out System.DateTime savedAt), Is.True);
            Assert.That(savedAt.Kind, Is.EqualTo(System.DateTimeKind.Utc));
            Assert.That(metadata.ActiveScene, Is.EqualTo("House"));
        }

        [Test]
        public void Metadata_RecoversCorruptPrimaryFromBackup()
        {
            string prefix = $"test_save_{System.Guid.NewGuid():N}_";
            var managerObject = new GameObject("Save Manager");
            SaveManager manager = managerObject.AddComponent<SaveManager>();
            SetPrivateField(manager, "filePrefix", prefix);

            try
            {
                Assert.That(manager.SaveSlot(0, "FirstSpawn"), Is.True);
                Assert.That(manager.SaveSlot(0, "SecondSpawn"), Is.True);

                string path = Path.Combine(
                    Application.persistentDataPath,
                    $"{prefix}0.json");
                File.WriteAllText(path, "{not valid json");

                bool found = manager.TryGetSlotMetadata(
                    0,
                    out SaveSlotMetadata metadata,
                    out string error);

                Assert.That(found, Is.True, error);
                Assert.That(metadata.RecoveredFromBackup, Is.True);
                Assert.That(metadata.ArrivalSpawnId, Is.EqualTo("FirstSpawn"));
            }
            finally
            {
                manager.DeleteSlot(0);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void ObjectStateParticipant_RestoresStateByStableId()
        {
            var root = new GameObject("Persistent Object");
            var openVisual = new GameObject("Open Visual");
            openVisual.transform.SetParent(root.transform);
            ObjectStateDefinition open =
                ScriptableObject.CreateInstance<ObjectStateDefinition>();
            open.name = "Open";
            SetPrivateField(open, "id", "door.open");

            ObjectStateHandler handler = root.AddComponent<ObjectStateHandler>();
            SetPrivateField(
                handler,
                "states",
                new[] { new ObjectStateHandler.StateBinding(open, openVisual) });
            ObjectStateSaveParticipant participant =
                root.AddComponent<ObjectStateSaveParticipant>();
            SetPrivateField(participant, "saveId", "house.front-door");
            SetPrivateField(participant, "stateHandler", handler);

            handler.ActivateState(open);
            string json = participant.CaptureSaveState();
            handler.ClearState();
            participant.RestoreSaveState(json);

            Assert.That(participant.SaveId, Is.EqualTo("house.front-door"));
            Assert.That(handler.CurrentState, Is.SameAs(open));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(open);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
