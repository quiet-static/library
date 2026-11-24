using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.Spawning;
using QuietStatic.Toolkit.State;
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
        private const string CancellationMessage = "Save restoration was cancelled because the save manager was disabled or destroyed.";

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

        /// <summary>Whether a load owns the save manager, including validation and restoration.</summary>
        public bool IsLoading => SaveRestoreOperation.IsInProgress;

        /// <summary>Optional game-owned validation. Return an explanation to reject a checkpoint.</summary>
        public Func<SaveGameData, string> ValidateCheckpoint { get; set; }
        /// <summary>Last operation failure, suitable for menu feedback.</summary>
        public string LastError { get; private set; } = string.Empty;
        /// <summary>Whether the last failed load could restore only part of the previous world.</summary>
        public bool LastLoadRecoveryIncomplete { get; private set; }
        /// <summary>Reports a load result. Requests received while a load owns the manager only log a warning.</summary>
        public event Action<int, bool, string> LoadCompleted;
        /// <summary>Raised after preflight succeeds and before destination loading begins.</summary>
        public event Action LoadStarted;

        private CrossSceneChannelSubscription<SaveRequestChannel>
            requestSubscription;

        private SaveSlotStorage SlotStorage => new(Application.persistentDataPath, filePrefix);

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
            SaveRestoreOperation.Cancel(this);
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
                SlotStorage.Write(slot, data);
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
                GameLogger.Warning("LoadSlot", this, $"A save slot is already being loaded. Ignoring request for slot {slot}.");
                return;
            }

            var operation = SaveRestoreOperation.Begin(this);
            LastLoadRecoveryIncomplete = false;
            string error;
            try
            {
                if (TryReadSlot(slot, out SaveGameData data, out error, out _))
                {
                    if (this == null || !isActiveAndEnabled)
                        throw new InvalidOperationException("The save manager must be active to restore a checkpoint.");
                    LoadStarted?.Invoke();
                    if (operation.CancellationRequested || this == null || !isActiveAndEnabled)
                        throw new InvalidOperationException(CancellationMessage);
                    operation.StartCoroutine(CompleteRestore(operation, slot, data));
                    return;
                }
            }
            catch (Exception exception) { error = $"Could not start the load: {exception.Message}"; }

            operation.Finish();
            RaiseLoadFailed(slot, error);
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
                SlotStorage.Delete(slot);
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
                data.flagValues = FlagManager.Instance.CaptureValues();
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

        private IEnumerator CompleteRestore(SaveRestoreOperation operation, int slot, SaveGameData data)
        {
            string failure = string.Empty;
            try
            {
                yield return RunGuarded(RestoreRoutine(operation, data, message => failure = message), exception =>
                {
                    LastLoadRecoveryIncomplete = true;
                    failure = $"Save restoration stopped unexpectedly: {exception.Message}";
                });
            }
            finally { operation.Finish(); }

            if (string.IsNullOrEmpty(failure))
            {
                LastError = string.Empty;
                try { onLoaded.Invoke(slot); }
                finally { LoadCompleted?.Invoke(slot, true, string.Empty); }
            }
            else
            {
                RaiseLoadFailed(slot, failure);
            }
        }

        private IEnumerator RestoreRoutine(SaveRestoreOperation operation, SaveGameData data, Action<string> reportResult)
        {
            string failure = string.Empty;
            bool CanContinue()
            {
                if (!string.IsNullOrEmpty(failure)) return false;
                if (!operation.CancellationRequested) return true;
                failure = CancellationMessage;
                return false;
            }
            Scene previousScene = SceneManager.GetActiveScene();
            string previousGameState = GameStateManager.Instance?.CurrentState;
            List<string> previousFlags = FlagManager.Instance?.ActiveFlags.ToList();
            var previousValues = FlagManager.Instance?.CaptureValues();
            var acquiredScenes = new List<Scene>();
            var retiringScenes = new HashSet<Scene>();
            var attemptedParticipants = new List<ISaveParticipant>();
            Dictionary<ISaveParticipant, (string payload, string error)> previousParticipants = null;
            bool attemptedFlagRestore = false;
            SceneFlowManager flow = SceneFlowManager.Instance;
            bool needsScene = !string.IsNullOrWhiteSpace(data.activeScene);
            Scene[] previousScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Where(scene => scene.isLoaded).ToArray();
            // Destination Awake/OnEnable/Start must observe the saved story, not the old session.
            if (needsScene && flow == null)
                failure = "No SceneFlowManager is available.";
            else if (needsScene && flow.IsTransitioning)
                failure = "Wait for the current scene transition to finish before loading.";
            else
            {
                try { previousParticipants = CaptureParticipantsForRollback(data.participants); }
                catch (Exception exception) { failure = "Could not capture participant state: " + exception.Message; }
                if (CanContinue())
                {
                    attemptedFlagRestore = true;
                    try { RestoreFlags(data.activeFlags, data.flagValues); }
                    catch (Exception exception) { failure = "Flag restoration failed: " + exception.Message; }
                }
            }

            if (CanContinue() && needsScene)
            {
                var request = new SceneTransitionRequest(data.activeScene, unloadOtherScenes: false);
                yield return RestoreScene(flow, request, acquiredScenes, error => failure = error);
                if (string.IsNullOrEmpty(failure))
                {
                    Scene active = SceneManager.GetActiveScene();
                    retiringScenes.UnionWith(previousScenes.Where(scene => scene != active &&
                        !flow.PersistentSceneNames.Contains(scene.name)));
                }
            }

            if (CanContinue())
            {
                try
                {
                    RestoreFlags(data.activeFlags, data.flagValues);
                }
                catch (Exception exception)
                {
                    failure = $"Flag restoration failed: {exception.Message}";
                }
            }

            SpawnPoint arrival = null;
            bool needsSpawn = !string.IsNullOrWhiteSpace(data.arrivalSpawnId);
            if (CanContinue() && needsSpawn)
            {
                // Let destination components register before validating the checkpoint's arrival point.
                yield return null;
                if (CanContinue())
                {
                    try
                    {
                        arrival = SpawnManager.Instance?.ResolveSpawnPoint(data.arrivalSpawnId, retiringScenes);
                        if (arrival == null)
                            throw new InvalidOperationException($"The saved spawn '{data.arrivalSpawnId}' is unavailable.");
                    }
                    catch (Exception exception) { failure = $"Spawn restoration failed: {exception.Message}"; }
                }
            }

            if (CanContinue())
            {
                try { TryRestoreParticipants(data.participants, retiringScenes, attemptedParticipants, operation, out failure); }
                catch (Exception exception) { failure = "Participant restoration failed: " + exception.Message; }
            }

            if (CanContinue() && needsSpawn)
            {
                try
                {
                    if (arrival == null || SpawnManager.Instance == null ||
                        !SpawnManager.Instance.TryGetRegisteredTarget(playerTargetId, out Transform target) ||
                        retiringScenes.Contains(target.gameObject.scene))
                        throw new InvalidOperationException("The saved spawn or its registered target is unavailable.");
                    SpawnPlacementUtility.MoveSafely(target, arrival.transform);
                }
                catch (Exception exception)
                {
                    failure = $"Spawn restoration failed: {exception.Message}";
                }
            }

            if (CanContinue())
            {
                // Restoration has committed. Keep unloading any retired content if one callback fails.
                if (flow != null)
                    foreach (Scene scene in retiringScenes)
                        yield return RunGuarded(flow.UnloadSceneIfLoadedRoutine(scene),
                            exception => GameLogger.Warning("LoadSlot", this,
                                $"Checkpoint restored, but scene cleanup failed: {exception.Message}"));
            }
            else if (attemptedFlagRestore)
            {
                string recoveryIssues = string.Empty;
                try { RestoreFlags(previousFlags, previousValues); }
                catch (Exception exception) { recoveryIssues += " Previous flags could not be restored: " + exception.Message; }

                if (SceneManager.GetActiveScene() != previousScene)
                {
                    try
                    {
                        if (!previousScene.IsValid() || !previousScene.isLoaded || !SceneManager.SetActiveScene(previousScene))
                            recoveryIssues += " The previous scene is no longer available.";
                    }
                    catch (Exception exception) { recoveryIssues += " The previous scene could not be activated: " + exception.Message; }
                }

                if (SceneFlowManager.Instance?.IsTransitioning != true)
                {
                    foreach (Scene scene in acquiredScenes)
                    {
                        if (scene == SceneManager.GetActiveScene()) continue;
                        yield return RunGuarded(UnloadAcquiredScene(flow, scene),
                            exception => recoveryIssues += $" A newly loaded scene could not be unloaded: {exception.Message}");
                        if (scene.IsValid() && scene.isLoaded)
                            recoveryIssues += $" Newly loaded scene '{scene.name}' is still loaded.";
                    }
                }
                else if (acquiredScenes.Count > 0)
                    recoveryIssues += " Newly loaded scenes could not be cleaned up while scene flow was busy.";

                recoveryIssues += RestoreParticipantSnapshots(attemptedParticipants, previousParticipants);
                if (!string.IsNullOrWhiteSpace(previousGameState))
                {
                    try { GameStateManager.Instance?.SetState(previousGameState); }
                    catch (Exception exception) { recoveryIssues += " The previous game state could not be restored: " + exception.Message; }
                }
                LastLoadRecoveryIncomplete = recoveryIssues.Length > 0;
                failure += recoveryIssues;
            }
            reportResult(failure);
        }

        private static IEnumerator UnloadAcquiredScene(SceneFlowManager flow, Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            if (flow != null) yield return flow.UnloadSceneIfLoadedRoutine(scene);
            else yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static IEnumerator RestoreScene(SceneFlowManager flow, SceneTransitionRequest request,
            ICollection<Scene> acquiredScenes, Action<string> fail)
        {
            Exception transitionException = null;
            SceneTransitionResult? result = null;
            void ObserveResult(SceneTransitionResult candidate)
            {
                if (ReferenceEquals(candidate.Request, request))
                    result = candidate;
            }

            flow.TransitionFinished += ObserveResult;
            try
            {
                yield return RunGuarded(flow.TransitionToSceneRoutine(request, acquiredScenes), exception => transitionException = exception);
            }
            finally { flow.TransitionFinished -= ObserveResult; }

            if (transitionException != null)
            {
                flow.RecoverInterruptedTransition(request);
                fail($"Scene transition threw an exception: {transitionException.Message}");
            }
            else if (result.HasValue && !result.Value.Succeeded)
            {
                fail(result.Value.Message);
            }
            else if (!result.HasValue ||
                     !string.Equals(SceneManager.GetActiveScene().name, request.TargetSceneName, StringComparison.Ordinal))
            {
                fail($"Scene '{request.TargetSceneName}' did not become active.");
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

        private static void RestoreFlags(IEnumerable<string> flags, IEnumerable<QuietStatic.Toolkit.Flags.FlagValueEntry> values)
        {
            if (FlagManager.Instance != null)
            {
                FlagManager.Instance.RestoreValues(flags, values);
            }
        }

        private static bool TryRestoreParticipants(
            IEnumerable<SaveParticipantData> savedParticipants,
            ISet<Scene> retiringScenes,
            ICollection<ISaveParticipant> attemptedParticipants,
            SaveRestoreOperation operation,
            out string error)
        {
            error = string.Empty;
            if (savedParticipants == null)
            {
                return true;
            }

            Dictionary<string, ISaveParticipant> activeParticipants =
                FindParticipants(retiringScenes).ToDictionary(
                    participant => participant.SaveId.Trim(),
                    StringComparer.Ordinal);

            var plan = new List<(ISaveParticipant participant, SaveParticipantData saved)>();
            foreach (SaveParticipantData saved in savedParticipants)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) ||
                    !activeParticipants.TryGetValue(saved.id.Trim(), out ISaveParticipant participant))
                {
                    error = $"Save participant '{saved?.id}' is unavailable in the restored scene.";
                    return false;
                }
                plan.Add((participant, saved));
            }

            foreach (var (participant, saved) in plan)
            {
                if (operation.CancellationRequested)
                {
                    error = CancellationMessage;
                    return false;
                }
                attemptedParticipants.Add(participant);
                try { participant.RestoreSaveState(saved.json); }
                catch (Exception exception)
                {
                    error = $"Save participant '{saved.id.Trim()}' failed to restore: " +
                            exception.Message;
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<ISaveParticipant, (string payload, string error)> CaptureParticipantsForRollback(
            IEnumerable<SaveParticipantData> savedParticipants)
        {
            var snapshots = new Dictionary<ISaveParticipant, (string payload, string error)>();
            var ids = new HashSet<string>(savedParticipants.Select(saved => saved.id.Trim()), StringComparer.Ordinal);
            if (ids.Count == 0) return snapshots;

            foreach (ISaveParticipant participant in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<ISaveParticipant>())
            {
                if (!ids.Contains(Normalize(participant.SaveId))) continue;
                try { snapshots.Add(participant, (participant.CaptureSaveState() ?? string.Empty, null)); }
                catch (Exception exception)
                {
                    // A project may permit loading from a menu where saving is unavailable.
                    snapshots.Add(participant, (null, exception.Message));
                }
            }
            return snapshots;
        }

        private static string RestoreParticipantSnapshots(List<ISaveParticipant> attempted,
            IReadOnlyDictionary<ISaveParticipant, (string payload, string error)> snapshots)
        {
            string error = string.Empty;
            for (int index = attempted.Count - 1; index >= 0; index--)
            {
                ISaveParticipant participant = attempted[index];
                if (participant is not MonoBehaviour owner || owner == null) continue;
                if (!snapshots.TryGetValue(participant, out var snapshot))
                {
                    error += $" Participant '{participant.SaveId}' has no previous state to restore.";
                    continue;
                }
                if (snapshot.error != null)
                {
                    error += $" Previous state for participant '{participant.SaveId}' was unavailable: {snapshot.error}";
                    continue;
                }

                try { participant.RestoreSaveState(snapshot.payload); }
                catch (Exception exception)
                {
                    error += $" Previous state for participant '{participant.SaveId}' could not be restored: {exception.Message}";
                }
            }
            return error;
        }

        private static IEnumerable<ISaveParticipant> FindParticipants(ISet<Scene> excludedScenes = null)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            MonoBehaviour[] behaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (excludedScenes?.Contains(behaviour.gameObject.scene) == true ||
                    behaviour is not ISaveParticipant participant ||
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

        private bool TryReadSlot(int slot, out SaveGameData data, out string error, out bool recoveredFromBackup)
        {
            data = null;
            recoveredFromBackup = false;
            if (!TryValidateSlot(slot, out error)) return false;

            bool found = SlotStorage.TryRead(slot, ValidateSavedCheckpoint, out data, out error, out recoveredFromBackup,
                out string warning);
            if (warning != null) GameLogger.Warning(nameof(SaveManager), this, warning);
            return found;
        }

        private string ValidateSavedCheckpoint(SaveGameData data) =>
            excludedActiveScenes?.Contains(data.activeScene) == true
                ? "The saved scene cannot be resumed."
                : ValidateCheckpoint?.Invoke(data);

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
            try { onLoadFailed.Invoke(slot, message); }
            finally
            {
                LoadCompleted?.Invoke(slot, false, message);
                GameLogger.Warning("LoadSlot", this, message);
            }
        }
    }
}
