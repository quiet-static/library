using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Inspector-friendly wrapper for running one or more cutscene character actions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UnityEvents cannot easily pass complex custom action data directly. This component
    /// solves that by storing a list of <see cref="CutsceneCharacterController.CharacterStepAction"/>
    /// entries in the Inspector, then applying them when <see cref="Run"/> is called.
    /// </para>
    /// <para>
    /// Typical usage is to place this component on an empty GameObject inside a cutscene
    /// scene, configure the desired character actions, then call <see cref="Run"/> from a
    /// cutscene step event.
    /// </para>
    /// </remarks>
    public class CutsceneCharacterStepTrigger : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Controller that owns the reusable character list and applies the configured cutscene actions.")]
        [SerializeField] private CutsceneCharacterController characterController;

        [Header("Actions")]
        [Tooltip("Character actions to apply when this trigger runs. Actions are processed in list order.")]
        [SerializeField]
        private List<CutsceneCharacterController.CharacterStepAction> actions =
            new List<CutsceneCharacterController.CharacterStepAction>();

        /// <summary>
        /// Attempts to auto-fill the character controller reference when the component is
        /// added or reset in the Unity Inspector.
        /// </summary>
        /// <remarks>
        /// This first checks the current GameObject, then searches the parent hierarchy.
        /// The reference can still be assigned manually if the controller lives elsewhere.
        /// </remarks>
        private void Reset()
        {
            characterController = GetComponent<CutsceneCharacterController>();

            if (characterController == null)
            {
                characterController = GetComponentInParent<CutsceneCharacterController>();
            }
        }

        /// <summary>
        /// Applies all configured character actions through the assigned character controller.
        /// </summary>
        /// <remarks>
        /// This method is designed to be called from UnityEvents, such as cutscene step
        /// start events or timeline-style sequence events. If no controller is assigned,
        /// the method logs a warning and exits safely.
        /// </remarks>
        public void Run()
        {
            if (characterController == null)
            {
                Debug.LogWarning("No CutsceneCharacterController assigned.", this);
                return;
            }

            characterController.ApplyActions(actions);
        }
    }
}
