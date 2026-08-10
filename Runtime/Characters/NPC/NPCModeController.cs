using System;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Switches groups of attachable behaviours to let one NPC change roles at runtime.</summary>
    [DefaultExecutionOrder(100)]
    public class NPCModeController : MonoBehaviour
    {
        [Serializable]
        public class NPCMode
        {
            [Tooltip("Unique designer-facing name used by SetModeByName.")]
            /// <summary>Designer-facing name used for mode lookup.</summary>
            public string modeName;

            [Tooltip("Behaviors activated while this mode is current.")]
            /// <summary>Behaviors enabled by this mode.</summary>
            public NPCBehaviour[] enabledBehaviours;

            [Tooltip("Presentation or helper objects activated while this mode is current.")]
            /// <summary>GameObjects enabled by this mode.</summary>
            public GameObject[] enabledObjects;

            [Tooltip("Invoked after this mode has been fully applied.")]
            /// <summary>Callbacks invoked after this mode is applied.</summary>
            public UnityEvent onModeEntered;
        }

        [Tooltip("Behavior components whose active state may be changed by a mode.")]
        [SerializeField] private NPCBehaviour[] managedBehaviours;
        [Tooltip("Optional presentation or helper objects whose active state may be changed by a mode.")]
        [SerializeField] private GameObject[] managedObjects;
        [Tooltip("Named mode configurations available to this NPC.")]
        [SerializeField] private NPCMode[] modes;
        [Tooltip("Index of the mode applied when this component starts.")]
        [Min(0)]
        [SerializeField] private int startingMode;

        /// <summary>Gets the current mode index, or -1 before a valid mode is applied.</summary>
        public int CurrentModeIndex { get; private set; } = -1;

        /// <summary>Gets the current mode name, or an empty string when no mode is active.</summary>
        public string CurrentModeName => IsValid(CurrentModeIndex) ? modes[CurrentModeIndex].modeName : string.Empty;

        private void Start() => SetMode(startingMode);

        /// <summary>Applies a mode by array index.</summary>
        /// <param name="index">Index in the configured modes array.</param>
        public void SetMode(int index)
        {
            if (!IsValid(index))
            {
                GameLogger.Warning(
                    "SetMode",
                    this,
                    $"Invalid NPC mode index {index} on {name}."
                );
                return;
            }

            foreach (NPCBehaviour behaviour in managedBehaviours)
                if (behaviour != null) behaviour.SetBehaviourActive(false);

            foreach (GameObject item in managedObjects)
                if (item != null) item.SetActive(false);

            NPCMode mode = modes[index];
            foreach (NPCBehaviour behaviour in mode.enabledBehaviours)
                if (behaviour != null) behaviour.SetBehaviourActive(true);

            foreach (GameObject item in mode.enabledObjects)
                if (item != null) item.SetActive(true);

            CurrentModeIndex = index;
            mode.onModeEntered?.Invoke();
        }

        /// <summary>Applies the first mode whose name matches, ignoring case.</summary>
        /// <param name="modeName">Configured mode name.</param>
        public void SetModeByName(string modeName)
        {
            for (int i = 0; i < modes.Length; i++)
            {
                if (string.Equals(modes[i].modeName, modeName, StringComparison.OrdinalIgnoreCase))
                {
                    SetMode(i);
                    return;
                }
            }

            GameLogger.Warning(
                "SetModeByName",
                this,
                $"NPC mode '{modeName}' was not found on {name}."
            );
        }

        private bool IsValid(int index) => modes != null && index >= 0 && index < modes.Length;
    }
}
