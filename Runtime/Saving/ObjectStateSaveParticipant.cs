using System;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>
    /// Opts one ObjectStateHandler into save-game capture and restoration.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Saving/Object State Save Participant")]
    [RequireComponent(typeof(ObjectStateHandler))]
    public sealed class ObjectStateSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        private sealed class StatePayload
        {
            /// <summary>Stable ID of the state active when this payload was captured.</summary>
            public string stateId;
        }

        [Tooltip("Stable, globally unique ID for this scene object.")]
        [SerializeField] private string saveId;

        [Tooltip("Object state handler whose current state is persisted.")]
        [SerializeField] private ObjectStateHandler stateHandler;

        /// <inheritdoc />
        public string SaveId => saveId;

        private void Reset()
        {
            stateHandler = GetComponent<ObjectStateHandler>();
        }

        /// <inheritdoc />
        public string CaptureSaveState()
        {
            ObjectStateDefinition currentState =
                stateHandler != null ? stateHandler.CurrentState : null;

            return JsonUtility.ToJson(new StatePayload
            {
                stateId = currentState != null ? currentState.Id : string.Empty
            });
        }

        /// <inheritdoc />
        public void RestoreSaveState(string json)
        {
            if (stateHandler == null)
            {
                return;
            }

            StatePayload payload;
            try
            {
                payload = JsonUtility.FromJson<StatePayload>(json);
            }
            catch (Exception exception)
            {
                GameLogger.Warning(
                    nameof(RestoreSaveState),
                    this,
                    $"Could not read object state save payload: {exception.Message}");
                return;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.stateId))
            {
                stateHandler.ClearState();
                return;
            }

            if (!stateHandler.TryActivateStateById(payload.stateId))
            {
                GameLogger.Warning(
                    nameof(RestoreSaveState),
                    this,
                    $"No configured object state uses ID '{payload.stateId}'.");
            }
        }
    }
}
