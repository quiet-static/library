using System;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic
{
    /// <summary>
    /// Coordinates the lifecycle of a dialogue session.
    /// </summary>
    /// <remarks>
    /// This manager does not control dialogue UI, dialogue trees, game states,
    /// player input, cameras, or scene loading directly.
    ///
    /// It simply tracks whether dialogue is active and raises events so other
    /// systems can react however they need.
    /// </remarks>
    public class DialogueManager : ToolkitSingleton<DialogueManager>
    {
        /// <summary>
        /// Raised when a dialogue session begins.
        /// The first parameter is the dialogue source object, if one was supplied.
        /// The second parameter is the optional world-space focus target.
        /// </summary>
        public static event Action<UnityEngine.Object, Transform> OnDialogueStarted;

        /// <summary>
        /// Raised when the active dialogue session ends.
        /// The parameter is the dialogue source object that was active.
        /// </summary>
        public static event Action<UnityEngine.Object> OnDialogueEnded;

        /// <summary>
        /// Raised whenever dialogue activity changes.
        /// The first parameter is the dialogue source object.
        /// The second parameter is true when dialogue begins and false when it ends.
        /// </summary>
        public static event Action<UnityEngine.Object, bool> OnDialogueStateChanged;

        [Serializable]
        public class DialogueStartedEvent : UnityEvent<UnityEngine.Object, Transform>
        {
        }

        [Serializable]
        public class DialogueEndedEvent : UnityEvent<UnityEngine.Object>
        {
        }

        [Header("Unity Events")]
        [Tooltip("Invoked when dialogue begins. Intended for scene-local behavior.")]
        [SerializeField] private DialogueStartedEvent onDialogueStarted;

        [Tooltip("Invoked when dialogue ends. Intended for scene-local behavior.")]
        [SerializeField] private DialogueEndedEvent onDialogueEnded;

        /// <summary>
        /// Gets whether a dialogue session is currently active.
        /// </summary>
        public bool IsDialogueActive { get; private set; }

        /// <summary>
        /// Gets the object that started the active dialogue session.
        /// This may be a DialogueTree, ScriptableObject, MonoBehaviour, or other dialogue asset.
        /// </summary>
        public UnityEngine.Object CurrentDialogue { get; private set; }

        /// <summary>
        /// Gets the optional focus target associated with the active dialogue.
        /// </summary>
        public Transform CurrentFocusTarget { get; private set; }

        /// <summary>
        /// Gets the world-space speaker or target currently associated with dialogue.
        /// </summary>
        public Transform CurrentSpeaker { get; private set; }

        /// <summary>
        /// Starts a new dialogue session.
        /// </summary>
        /// <param name="dialogue">
        /// Dialogue source or asset associated with this session.
        /// </param>
        /// <param name="focusTarget">
        /// Optional transform the camera or other systems should focus toward.
        /// </param>
        /// <param name="speaker">
        /// Optional transform representing the speaking character or object.
        /// </param>
        /// <returns>
        /// True if dialogue started; false if another dialogue session is already active.
        /// </returns>
        public bool StartDialogue(
            UnityEngine.Object dialogue,
            Transform focusTarget = null,
            Transform speaker = null
        )
        {
            if (IsDialogueActive)
            {
                GameLogger.Warning(
                    "StartDialogue",
                    this,
                    $"{nameof(DialogueManager)} cannot start dialogue because another dialogue session is already active."
                );
                return false;
            }

            IsDialogueActive = true;
            CurrentDialogue = dialogue;
            CurrentFocusTarget = focusTarget;
            CurrentSpeaker = speaker;

            OnDialogueStateChanged?.Invoke(CurrentDialogue, true);
            OnDialogueStarted?.Invoke(CurrentDialogue, CurrentFocusTarget);
            onDialogueStarted?.Invoke(CurrentDialogue, CurrentFocusTarget);

            return true;
        }

        /// <summary>
        /// Ends the active dialogue session.
        /// </summary>
        /// <returns>
        /// True if dialogue ended; false if no dialogue session was active.
        /// </returns>
        public bool StopDialogue()
        {
            if (!IsDialogueActive)
            {
                return false;
            }

            UnityEngine.Object endedDialogue = CurrentDialogue;

            // Clear state before events so listeners may safely begin a new dialogue.
            IsDialogueActive = false;
            CurrentDialogue = null;
            CurrentFocusTarget = null;
            CurrentSpeaker = null;

            OnDialogueStateChanged?.Invoke(endedDialogue, false);
            OnDialogueEnded?.Invoke(endedDialogue);
            onDialogueEnded?.Invoke(endedDialogue);

            return true;
        }

        /// <summary>
        /// Updates the active dialogue's focus target.
        /// </summary>
        /// <param name="focusTarget">New target for camera or UI focus behavior.</param>
        public void SetFocusTarget(Transform focusTarget)
        {
            CurrentFocusTarget = focusTarget;
        }

        /// <summary>
        /// Updates the active dialogue's speaker transform.
        /// </summary>
        /// <param name="speaker">New transform representing the active speaker.</param>
        public void SetSpeaker(Transform speaker)
        {
            CurrentSpeaker = speaker;
        }
    }
}