using UnityEngine;

namespace QuietStatic.Toolkit.Utilities
{
    /// <summary>
    /// Identifies a reusable object state that can be selected from Inspector UnityEvents.
    /// </summary>
    /// <remarks>
    /// The asset is only an identity and display description. Each
    /// <see cref="ObjectStateHandler"/> decides which scene objects represent this state.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ObjectState",
        menuName = "Quiet Static Toolkit/Utilities/Object State Definition"
    )]
    public sealed class ObjectStateDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used by code, save data, or diagnostic output.")]
        [SerializeField] private string id;

        [Tooltip("Optional explanation of what this state represents.")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        /// <summary>Gets the stable identifier assigned to this state.</summary>
        public string Id => id;

        /// <summary>Gets the editor-facing description of this state.</summary>
        public string Description => description;
    }
}
