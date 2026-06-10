using System;
using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Serializable wrapper for storing a Unity scene name in the Inspector.
    /// </summary>
    /// <remarks>
    /// Unity does not serialize <see cref="UnityEngine.SceneManagement.Scene"/> values as
    /// editable Inspector references, so this class stores the scene by name instead.
    /// The referenced scene must still be added to Unity's Build Settings before it can
    /// be loaded at runtime.
    /// </remarks>
    [Serializable]
    public class SceneReference
    {
        [Header("Scene")]
        [Tooltip("Name of the Unity scene to reference. The scene must be included in Build Settings before it can be loaded at runtime.")]
        [SerializeField] private string sceneName;

        /// <summary>
        /// Gets the configured scene name.
        /// </summary>
        /// <remarks>
        /// This value is intended to be passed to scene loading systems such as
        /// SceneManager or SceneLoadService. It may be empty if no scene has been
        /// assigned in the Inspector.
        /// </remarks>
        public string SceneName => sceneName;

        /// <summary>
        /// Gets whether this scene reference contains a non-empty scene name.
        /// </summary>
        /// <remarks>
        /// This only validates that the field has text. It does not verify that the scene
        /// exists in the project or that it has been added to Build Settings.
        /// </remarks>
        public bool IsValid => !string.IsNullOrWhiteSpace(sceneName);
    }
}
