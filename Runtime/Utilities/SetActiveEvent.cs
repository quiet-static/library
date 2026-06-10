using UnityEngine;

namespace QuietStatic.Toolkit.Utilities
{
    /// <summary>
    /// Inspector-friendly helper for enabling or disabling a target GameObject.
    /// </summary>
    /// <remarks>
    /// This component is useful for UnityEvents, animation events, timeline signals,
    /// buttons, triggers, and other systems that need to toggle an object without
    /// writing a custom script for each case.
    /// </remarks>
    public class SetActiveEvent : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The GameObject that will be enabled or disabled when this event helper is called.")]
        [SerializeField] private GameObject target;

        /// <summary>
        /// Attempts to assign this GameObject as the default target when the component
        /// is first added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            target = gameObject;
        }

        /// <summary>
        /// Sets the active state of the configured target GameObject.
        /// </summary>
        /// <param name="active">
        /// True to activate the target; false to deactivate it.
        /// </param>
        /// <remarks>
        /// If no target is assigned, this method safely does nothing.
        /// </remarks>
        public void SetActive(bool active)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(active);
        }

        /// <summary>
        /// Activates the configured target GameObject.
        /// </summary>
        public void Show()
        {
            SetActive(true);
        }

        /// <summary>
        /// Deactivates the configured target GameObject.
        /// </summary>
        public void Hide()
        {
            SetActive(false);
        }
    }
}
