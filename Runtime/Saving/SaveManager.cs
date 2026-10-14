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

        [Tooltip("Active scene names that cannot be restored independently, such as temporary playable memories. Saving in these scenes leaves existing slots untouched.")]
        [SerializeField] private string[] excludedActiveScenes = Array.Empty<string>();

        [Header("Restoration")]
        [Tooltip("Registered target ID moved to the saved arrival spawn after loading.")]
        [SerializeField] private string playerTargetId = "Player";

        [Tooltip("Required receiver channel for cross-scene save, load, and delete commands. Direct SaveManager method calls do not use this channel.")]
        [RequiredCommandChannel(isReceiver: true)]
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

        /// <summary>Optional game-owned validation. Return an explanation to reject a checkpoint.</summary>
        public Func<SaveGameData, string> ValidateCheckpoint { get; set; }
        /// <summary>Last operation failure, suitable for menu feedback.</summary>
        public string LastError { get; private set; } = string.Empty;
        public event Action<int, bool, string> LoadCompleted;
        /// <summary>Raised after preflight succeeds and before destination loading begins.</summary>
        public event Action LoadStarted;

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

            if (excludedActiveScenes != null && excludedActiveScenes.Contains(SceneManager.GetActiveScene().name))
            {
                RaiseSaveFailed(slot, "Saving is unavailable during this temporary scene. Finish the sequence before saving.");
                return false;
            }

            try
            {
                if (IsLoading || SceneFlowManager.Instance?.IsTransitioning == true)
                    throw new InvalidOperationException("Wait for loading to finish before saving.");
                SaveGameData data = CaptureData(arrivalSpawnId);
                error = ValidateCheckpoint?.Invoke(data);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                string json = JsonUtility.ToJson(data, true);
                WriteAtomically(GetSlotPath(slot), json);
                LastError = string.Empty;
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

            LoadStarted?.Invoke();
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
            return TryGetSlotMetadata(slot, out _, out _);
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
            bool succeeded = SaveSlot(slot, arrivalSpawnId);
            requestChannel?.ReportSaveCompleted(slot, succeeded);
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
            string failure = string.Empty;
            string previousScene = SceneManager.GetActiveScene().name;
            List<string> previousFlags = FlagManager.Instance?.ActiveFlags.ToList();
            try
            {
                // Destination Awake/OnEnable/Start must observe the saved story, not the old session.
                try { RestoreFlags(data.activeFlags); }
                catch (Exception exception) { failure = "Flag restoration failed: " + exception.Message; }
                if (string.IsNullOrEmpty(failure) && !string.IsNullOrWhiteSpace(data.activeScene))
                {
                    SceneFlowManager flow = SceneFlowManager.Instance;
                    if (flow == null)
                    {
                        failure = "No SceneFlowManager is available.";
                    }
                    else
                    {
                        Exception transitionException = null;
                        yield return RunGuarded(
                            flow.TransitionToSceneRoutine(
                                new SceneTransitionRequest(data.activeScene,
                                    additionalScenesToKeep: new[] { previousScene })),
                            exception => transitionException = exception);

                        if (transitionException != null)
                        {
                            failure = $"Scene transition threw an exception: " +
                                      transitionException.Message;
                        }
                        else if (!flow.LastTransitionResult.HasValue ||
                                 !flow.LastTransitionResult.Value.Succeeded ||
                                 !string.Equals(
                                     flow.LastTransitionResult.Value.Destination,
                                     data.activeScene,
                                     StringComparison.Ordinal) ||
                                 !string.Equals(
                                     SceneManager.GetActiveScene().name,
                                     data.activeScene,
                                     StringComparison.Ordinal))
                        {
                            failure = flow.LastTransitionResult?.Message ??
                                      $"Scene '{data.activeScene}' did not become active.";
                        }
                    }
                }

                if (string.IsNullOrEmpty(failure))
                {
                    try
                    {
                        RestoreFlags(data.activeFlags);
                    }
                    catch (Exception exception)
                    {
                        failure = $"Flag restoration failed: {exception.Message}";
                    }
                }

                if (string.IsNullOrEmpty(failure))
                {
                    try { TryRestoreParticipants(data.participants, out failure); }
                    catch (Exception exception) { failure = "Participant restoration failed: " + exception.Message; }
                }

                if (string.IsNullOrEmpty(failure) &&
                    !string.IsNullOrWhiteSpace(data.arrivalSpawnId))
                {
                    yield return null;
                    try
                    {
                        if (SpawnManager.Instance == null || !SpawnManager.Instance.MoveRegisteredTargetToSpawn(
                            playerTargetId,
                            data.arrivalSpawnId))
                            throw new InvalidOperationException("The saved spawn is unavailable.");
                    }
                    catch (Exception exception)
                    {
                        failure = $"Spawn restoration failed: {exception.Message}";
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }

            if (string.IsNullOrEmpty(failure))
            {
                // Keep the source menu available until every participant has restored.
                SceneFlowManager flow = SceneFlowManager.Instance;
                if (flow != null && previousScene != data.activeScene &&
                    !flow.PersistentSceneNames.Contains(previousScene) &&
                    !string.IsNullOrWhiteSpace(data.activeScene))
                    flow.UnloadScene(previousScene);
                LastError = string.Empty;
                onLoaded.Invoke(slot);
                LoadCompleted?.Invoke(slot, true, string.Empty);
            }
            else
            {
                SceneFlowManager.Instance?.RecoverInterruptedTransition();
                try { RestoreFlags(previousFlags); }
                catch (Exception exception) { failure += " Previous flags could not be restored: " + exception.Message; }
                RaiseLoadFailed(slot, failure);
            }
        }

        /// <summary>Runs nested restoration/recovery iterators and reports exceptions to the caller.</summary>
        public static IEnumerator RunGuarded(
            IEnumerator routine,
            Action<Exception> onException)
        {
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = routine.MoveNext();
                    if (moved)
                    {
                        current = routine.Current;
                    }
                }
                catch (Exception exception)
                {
                    onException?.Invoke(exception);
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    Exception nestedFailure = null;
                    yield return RunGuarded(nested, exception => nestedFailure = exception);
                    if (nestedFailure != null)
                    {
                        (routine as IDisposable)?.Dispose();
                        onException?.Invoke(nestedFailure);
                        yield break;
                    }
                }
                else yield return current;
            }
        }

        private static void RestoreFlags(IEnumerable<string> flags)
        {
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.RestoreFlags(flags);
            }
        }

        private static bool TryRestoreParticipants(
            IEnumerable<SaveParticipantData> savedParticipants,
            out string error)
        {
            error = string.Empty;
            if (savedParticipants == null)
            {
                return true;
            }

            Dictionary<string, ISaveParticipant> activeParticipants =
                FindParticipants().ToDictionary(
                    participant => participant.SaveId.Trim(),
                    StringComparer.Ordinal);

            foreach (SaveParticipantData saved in savedParticipants)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) ||
                    !activeParticipants.ContainsKey(saved.id.Trim()))
                {
                    error = $"Save participant '{saved?.id}' is unavailable in the restored scene.";
                    return false;
                }
                if (saved != null &&
                    !string.IsNullOrWhiteSpace(saved.id) &&
                    activeParticipants.TryGetValue(saved.id.Trim(), out ISaveParticipant participant))
                {
                    try
                    {
                        participant.RestoreSaveState(saved.json);
                    }
                    catch (Exception exception)
                    {
                        error = $"Save participant '{saved.id.Trim()}' failed to restore: " +
                                exception.Message;
                        return false;
                    }
                }
            }

            return true;
        }

        private static IEnumerable<ISaveParticipant> FindParticipants()
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            MonoBehaviour[] behaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

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
                    throw new InvalidOperationException($"Duplicate save participant ID '{id}'. The checkpoint cannot be restored reliably.");
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

        private bool TryDeserializeFile(
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
                var participantIds = new HashSet<string>(StringComparer.Ordinal);
                if (data.participants.Any(p => p == null || string.IsNullOrWhiteSpace(p.id) ||
                    !participantIds.Add(p.id.Trim())))
                {
                    error = "Save participant IDs are missing or duplicated.";
                    return false;
                }
                if (excludedActiveScenes != null && excludedActiveScenes.Contains(data.activeScene))
                {
                    error = "The saved scene cannot be resumed.";
                    return false;
                }
                error = ValidateCheckpoint?.Invoke(data);
                if (!string.IsNullOrEmpty(error)) return false;
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
            LastError = message;
            onSaveFailed.Invoke(slot, message);
            GameLogger.Warning("SaveSlot", this, message);
        }

        private void RaiseLoadFailed(int slot, string message)
        {
            LastError = message;
            onLoadFailed.Invoke(slot, message);
            LoadCompleted?.Invoke(slot, false, message);
            GameLogger.Warning("LoadSlot", this, message);
        }
    }
}
