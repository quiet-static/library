using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>
    /// Defines the ordered inputs used by an input-sequence minigame.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InputSequence",
        menuName = "Quiet Static/Minigames/Input Sequence")]
    public sealed class InputSequenceDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Step
        {
            [Tooltip("Input System action the player must perform for this step.")]
            [SerializeField] private InputActionReference action;

            [Tooltip("Optional text shown instead of the action's current binding display name.")]
            [SerializeField] private string displayName;

            /// <summary>The Input System action required by this step.</summary>
            public InputAction Action => action != null ? action.action : null;

            /// <summary>Text suitable for displaying this step to the player.</summary>
            public string DisplayName
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        return displayName;
                    }

                    InputAction inputAction = Action;
                    if (inputAction == null)
                    {
                        return "?";
                    }

                    string bindingName = inputAction.GetBindingDisplayString();
                    return string.IsNullOrWhiteSpace(bindingName) ? inputAction.name : bindingName;
                }
            }
        }

        [Tooltip("Inputs the player must perform, in order.")]
        [SerializeField] private List<Step> steps = new List<Step>();

        /// <summary>The ordered, read-only sequence of required inputs.</summary>
        public IReadOnlyList<Step> Steps => steps;

        /// <summary>Number of steps in this sequence.</summary>
        public int Count => steps.Count;
    }
}
