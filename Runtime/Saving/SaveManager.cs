using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>
    /// Coordinates versioned save slots using the existing flag, scene-flow, and spawn systems.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Managers/Save Manager")]
    public sealed class SaveManager : ToolkitSingleton<SaveManager>
    {
        [Serializable]
        public sealed class SlotUnityEvent : UnityEvent<int> { }

        [Serializable]
        public sealed class SaveErrorUnityEvent : UnityEvent<int, string> { }

        [Header("Storage")]
        [Tooltip("File prefix used inside Unity's persistent data directory.")]
        [SerializeField] private string filePrefix = "save_slot_";

        [Tooltip("Highest valid slot index. Slots begin at zero.")]
        [Min(0)]
        [SerializeField] private int maximumSlot = 9;

        [Header("Restoration")]
        [Tooltip("Registered target ID moved to the saved arrival spawn after loading.")]
        [SerializeField] private string playerTargetId = "Player";

        [Tooltip("Optional cross-scene request channel.")]
        [SerializeField] private SaveRequestChannel requestChannel;

        [Header("Events")]
        [Tooltip("Invoked after a slot is saved successfully. Passes the slot index.")]
        [SerializeField] private SlotUnityEvent onSaved = new SlotUnityEvent();
        [Tooltip("Invoked after a slot is restored successfully. Passes the slot index.")]
        [SerializeField] private SlotUnityEvent onLoaded = new SlotUnityEvent();
        [Tooltip("Invoked after a slot and its backup are deleted. Passes the slot index.")]
        [SerializeField] private SlotUnityEvent onDeleted = new SlotUnityEvent();
        [Tooltip("Invoked when saving fails. Passes the slot index and error message.")]
        [SerializeField] private SaveErrorUnityEvent onSaveFailed = new SaveErrorUnityEvent();
        [Tooltip("Invoked when loading fails. Passes the slot index and error message.")]
        [SerializeField] private SaveErrorUnityEvent onLoadFailed = new SaveErrorUnityEvent();

        /// <summary>Whether a save restoration is currently running.</summary>
        public bool IsLoading { get; private set; }

        private CrossSceneChannelSubscription<SaveRequestChannel>
            requestSubscription;

        private CrossSceneChannelSubscription<SaveRequestChannel>
            RequestSubscription =>
                requestSubscription ??=
                    new CrossSceneChannelSubscription<SaveRequestChannel>(
                        SubscribeToRequests,
                        UnsubscribeFromRequests);

        private void OnEnable()
        {
            RequestSubscription.Bind(requestChannel);
        }

        private void OnDisable()
        {
            RequestSubscription.Unbind();
        }

        /// <summary>Changes the cross-scene channel and updates its live subscription.</summary>
        public void SetRequestChannel(SaveRequestChannel value)
        {
            requestChannel = value;
            if (isActiveAndEnabled)
            {
                RequestSubscription.Bind(requestChannel);
            }
        }

        /// <summary>Saves the current world state to a slot.</summary>
        public bool SaveSlot(int slot, string arrivalSpawnId = "")
        {
            if (!TryValidateSlot(slot, out string error))
            {
                RaiseSaveFailed(slot, error);
                return false;
            }

            try
            {
                SaveGameData data = CaptureData(arrivalSpawnId);
                string json = JsonUtility.ToJson(data, true);
                WriteAtomically(GetSlotPath(slot), json);
                onSaved.Invoke(slot);
                return true;
            }
            catch (Exception exception)
            {
                RaiseSaveFailed(slot, exception.Message);
                return false;
            }
        }

        /// <summary>UnityEvent-friendly overload that saves without an arrival spawn.</summary>
        public void SaveSlot(int slot) => SaveSlot(slot, string.Empty);

        /// <summary>Begins restoring a save slot.</summary>
        public void LoadSlot(int slot)
        {
            if (IsLoading)
            {
                RaiseLoadFailed(slot, "A save slot is already being loaded.");
                return;
            }

            if (!TryReadSlot(
                    slot,
                    out SaveGameData data,
                    out string error,
                    out _))
            {
                RaiseLoadFailed(slot, error);
                return;
            }

            StartCoroutine(RestoreRoutine(slot, data));
        }

        /// <summary>Deletes a save slot if it exists.</summary>
        public bool DeleteSlot(int slot)
        {
            if (!TryValidateSlot(slot, out string error))
            {
                RaiseSaveFailed(slot, error);
                return false;
            }

            try
            {
                string path = GetSlotPath(slot);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                string backupPath = GetBackupPath(path);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                onDeleted.Invoke(slot);
                return true;
            }
            catch (Exception exception)
            {
                RaiseSaveFailed(slot, exception.Message);
                return false;
            }
        }

        /// <summary>Returns whether a valid slot file exists.</summary>
        public bool HasSave(int slot)
        {
            if (!TryValidateSlot(slot, out _))
            {
                return false;
            }

            string path = GetSlotPath(slot);
            return File.Exists(path) || File.Exists(GetBackupPath(path));
        }

        /// <summary>
        /// Reads lightweight slot information without changing gameplay state.
        /// </summary>
        public bool TryGetSlotMetadata(
            int slot,
            out SaveSlotMetadata metadata,
            out string error)
        {
            metadata = null;

            if (!TryReadSlot(
                    slot,
                    out SaveGameData data,
                    out error,
                    out bool recoveredFromBackup))
            {
                return false;
            }

            metadata = new SaveSlotMetadata(
                slot,
                data.version,
                data.savedAtUtc,
                data.activeScene,
                data.arrivalSpawnId,
                recoveredFromBackup);
            return true;
        }

        private void HandleSaveRequested(int slot, string arrivalSpawnId)
        {
            SaveSlot(slot, arrivalSpawnId);
        }

        private void HandleDeleteRequested(int slot)
        {
            DeleteSlot(slot);
        }

        private void SubscribeToRequests(SaveRequestChannel value)
        {
            value.CommandRequested += HandleCommand;
        }

        private void UnsubscribeFromRequests(SaveRequestChannel value)
        {
            value.CommandRequested -= HandleCommand;
        }

        private void HandleCommand(SaveCommand command)
        {
            switch (command.Type)
            {
                case SaveCommandType.Save:
                    HandleSaveRequested(
                        command.Slot,
                        command.ArrivalSpawnId);
                    break;
                case SaveCommandType.Load:
                    LoadSlot(command.Slot);
                    break;
                case SaveCommandType.Delete:
                    HandleDeleteRequested(command.Slot);
                    break;
            }
        }

        private SaveGameData CaptureData(string arrivalSpawnId)
        {
            var data = new SaveGameData
            {
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                activeScene = SceneManager.GetActiveScene().name,
                arrivalSpawnId = Normalize(arrivalSpawnId)
            };

            if (FlagManager.Instance != null)
            {
                data.activeFlags = FlagManager.Instance.ActiveFlags
                    .OrderBy(flag => flag, StringComparer.Ordinal)
                    .ToList();
            }

            foreach (ISaveParticipant participant in FindParticipants())
            {
                data.participants.Add(new SaveParticipantData(
                    participant.SaveId.Trim(),
                    participant.CaptureSaveState() ?? string.Empty));
            }

            return data;
        }

        private IEnumerator RestoreRoutine(int slot, SaveGameData data)
        {
            IsLoading = true;

            if (!string.IsNullOrWhiteSpace(data.activeScene))
            {
                if (SceneFlowManager.Instance == null)
                {
                    IsLoading = false;
                    RaiseLoadFailed(slot, "No SceneFlowManager is available.");
                    yield break;
                }

                yield return SceneFlowManager.Instance.TransitionToSceneRoutine(
                    new SceneTransitionRequest(data.activeScene));
            }

            RestoreFlags(data.activeFlags);
            RestoreParticipants(data.participants);

            if (!string.IsNullOrWhiteSpace(data.arrivalSpawnId) &&
                SpawnManager.Instance != null)
            {
                yield return null;
                SpawnManager.Instance.MoveRegisteredTargetToSpawn(
                    playerTargetId,
                    data.arrivalSpawnId);
            }

            IsLoading = false;
            onLoaded.Invoke(slot);
        }

        private static void RestoreFlags(IEnumerable<string> flags)
        {
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.RestoreFlags(flags);
            }
        }

        private static void RestoreParticipants(
            IEnumerable<SaveParticipantData> savedParticipants)
        {
            if (savedParticipants == null)
            {
                return;
            }

            Dictionary<string, ISaveParticipant> activeParticipants =
                FindParticipants().ToDictionary(
                    participant => participant.SaveId.Trim(),
                    StringComparer.Ordinal);

            foreach (SaveParticipantData saved in savedParticipants)
            {
                if (saved != null &&
                    !string.IsNullOrWhiteSpace(saved.id) &&
                    activeParticipants.TryGetValue(saved.id.Trim(), out ISaveParticipant participant))
                {
                    participant.RestoreSaveState(saved.json);
                }
            }
        }

        private static IEnumerable<ISaveParticipant> FindParticipants()
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            MonoBehaviour[] behaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is not ISaveParticipant participant ||
                    string.IsNullOrWhiteSpace(participant.SaveId))
                {
                    continue;
                }

                string id = participant.SaveId.Trim();
                if (!seenIds.Add(id))
                {
                    GameLogger.Warning(nameof(SaveManager), behaviour,
                        $"Duplicate save participant ID '{id}' was ignored.");
                    continue;
                }

                yield return participant;
            }
        }

        private bool TryReadSlot(
            int slot,
            out SaveGameData data,
            out string error,
            out bool recoveredFromBackup)
        {
            data = null;
            recoveredFromBackup = false;

            if (!TryValidateSlot(slot, out error))
            {
                return false;
            }

            string path = GetSlotPath(slot);
            string backupPath = GetBackupPath(path);
            if (!File.Exists(path) && !File.Exists(backupPath))
            {
                error = $"Save slot {slot} does not exist.";
                return false;
            }

            if (TryDeserializeFile(path, out data, out string primaryError))
            {
                error = string.Empty;
                return true;
            }

            if (TryDeserializeFile(backupPath, out data, out string backupError))
            {
                recoveredFromBackup = true;
                error = string.Empty;

                try
                {
                    File.Copy(backupPath, path, true);
                }
                catch (Exception exception)
                {
                    GameLogger.Warning(nameof(SaveManager), this,
                        $"Loaded save slot {slot} from backup but could not repair " +
                        $"the primary file: {exception.Message}");
                }

                return true;
            }

            error =
                $"Primary save could not be read ({primaryError}). " +
                $"Backup could not be read ({backupError}).";
            return false;
        }

        private static bool TryDeserializeFile(
            string path,
            out SaveGameData data,
            out string error)
        {
            data = null;

            if (!File.Exists(path))
            {
                error = "file does not exist";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<SaveGameData>(json);
                if (data == null ||
                    string.IsNullOrWhiteSpace(json) ||
                    json.Trim() == "{}")
                {
                    error = "file contains no readable save data";
                    return false;
                }

                if (data.version <= 0)
                {
                    error = $"save version {data.version} is invalid";
                    return false;
                }

                if (data.version > SaveGameData.CurrentVersion)
                {
                    error = $"save uses unsupported version {data.version}";
                    return false;
                }

                data.activeFlags ??= new List<string>();
                data.participants ??= new List<SaveParticipantData>();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool TryValidateSlot(int slot, out string error)
        {
            if (slot < 0 || slot > maximumSlot)
            {
                error = $"Slot must be between 0 and {maximumSlot}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private string GetSlotPath(int slot)
        {
            string safePrefix = string.IsNullOrWhiteSpace(filePrefix)
                ? "save_slot_"
                : filePrefix.Trim();
            return Path.Combine(
                Application.persistentDataPath,
                $"{safePrefix}{slot}.json");
        }

        private static void WriteAtomically(string path, string json)
        {
            string temporaryPath = path + ".tmp";
            string backupPath = GetBackupPath(path);
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, backupPath);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        private static string GetBackupPath(string path) => path + ".bak";

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void RaiseSaveFailed(int slot, string message)
        {
            onSaveFailed.Invoke(slot, message);
            GameLogger.Warning("SaveSlot", this, message);
        }

        private void RaiseLoadFailed(int slot, string message)
        {
            onLoadFailed.Invoke(slot, message);
            GameLogger.Warning("LoadSlot", this, message);
        }
    }
}
