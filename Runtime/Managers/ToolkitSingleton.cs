using UnityEngine;

namespace QuietStatic.Toolkit.Core
{
    /// <summary>
    /// Generic MonoBehaviour-based singleton base class for scene-level or persistent managers.
    /// </summary>
    /// <typeparam name="T">
    /// The concrete MonoBehaviour type that should expose a static singleton instance.
    /// This is normally the derived class itself, such as <c>GameStateManager</c>.
    /// </typeparam>
    /// <remarks>
    /// This class provides a simple reusable singleton pattern for manager-style components.
    /// It stores the first active instance in <see cref="Instance"/>. Scene composition owns
    /// manager lifetime; persistent managers belong in retained support scenes.
    ///
    /// Duplicate handling is configurable. When duplicates are allowed, a later instance will
    /// not replace the existing <see cref="Instance"/>, but it also will not be destroyed.
    /// </remarks>
    public abstract class ToolkitSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Gets the active singleton instance for this component type.
        /// </summary>
        /// <remarks>
        /// This value is assigned during <see cref="Awake"/> on the first valid instance.
        /// It is cleared in <see cref="OnDestroy"/> when the current singleton instance is destroyed.
        /// </remarks>
        public static T Instance { get; private set; }

        [Header("Singleton Settings")]
        [Tooltip("If true, duplicate instances destroy themselves when another singleton instance already exists.")]
        [SerializeField] private bool destroyDuplicates = true;

        /// <summary>
        /// Initializes the singleton instance and applies duplicate-handling rules.
        /// </summary>
        /// <remarks>
        /// Derived classes that override this method should call <c>base.Awake()</c> before
        /// running their own initialization, especially if their setup depends on this object
        /// being the active singleton instance.
        /// </remarks>
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (destroyDuplicates)
                {
                    enabled = false;
                    Destroy(gameObject);
                }

                return;
            }

            Instance = this as T;

        }

        /// <summary>
        /// Clears the singleton reference when the active singleton instance is destroyed.
        /// </summary>
        /// <remarks>
        /// This prevents stale references from remaining after scene changes, play mode exits,
        /// or manual destruction of the singleton GameObject.
        /// </remarks>
        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
