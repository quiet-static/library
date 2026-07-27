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
            public string modeName;
            public NPCBehaviour[] enabledBehaviours;
            public GameObject[] enabledObjects;
            public UnityEvent onModeEntered;
        }

        [Tooltip("Behavior components whose active state may be changed by a mode.")]
        [SerializeField] private NPCBehaviour[] managedBehaviours;
        [Tooltip("Optional presentation or helper objects whose active state may be changed by a mode.")]
        [SerializeField] private GameObject[] managedObjects;
        [Tooltip("Named mode configurations available to this NPC.")]
        [SerializeField] private NPCMode[] modes;
        [Tooltip("Index of the mode applied when this component starts.")]
        [SerializeField] private int startingMode;

        public int CurrentModeIndex { get; private set; } = -1;
        public string CurrentModeName => IsValid(CurrentModeIndex) ? modes[CurrentModeIndex].modeName : string.Empty;

        private void Start() => SetMode(startingMode);

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
