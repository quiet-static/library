using QuietStatic.Toolkit.Objectives;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// UnityEvent-facing bridge for objective lifecycle commands.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Objective Handler")]
    public class ObjectiveHandler : MonoBehaviour
    {
        [Tooltip("Optional definition used by parameterless UnityEvent methods.")]
        [SerializeField] private ObjectiveDefinition objective;

        /// <summary>Activates the definition assigned in the Inspector.</summary>
        public void ActivateObjective()
        {
            ActivateObjective(objective);
        }

        /// <summary>Activates a supplied objective definition.</summary>
        public void ActivateObjective(ObjectiveDefinition definition)
        {
            ObjectiveManager.Instance?.ActivateObjective(definition);
        }

        /// <summary>Completes the definition assigned in the Inspector.</summary>
        public void CompleteObjective()
        {
            CompleteObjective(objective);
        }

        /// <summary>Completes a supplied objective definition.</summary>
        public void CompleteObjective(ObjectiveDefinition definition)
        {
            ObjectiveManager.Instance?.CompleteObjective(definition);
        }

        /// <summary>Completes whichever objective is currently active.</summary>
        public void CompleteActiveObjective()
        {
            ObjectiveManager.Instance?.CompleteActiveObjective();
        }

        /// <summary>Clears the active objective without completing it.</summary>
        public void ClearActiveObjective()
        {
            ObjectiveManager.Instance?.ClearActiveObjective();
        }

        /// <summary>Refreshes the active objective's presentation event.</summary>
        public void RefreshActiveObjective()
        {
            ObjectiveManager.Instance?.RefreshActiveObjective();
        }
    }
}
