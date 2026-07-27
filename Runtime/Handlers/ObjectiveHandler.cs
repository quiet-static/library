using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Reserved UnityEvent-facing bridge for objective commands.
    /// </summary>
    /// <remarks>
    /// Objective selection is currently automatic through <c>ObjectiveResolver</c>, so this
    /// component intentionally exposes no commands yet. It remains as the stable hierarchy
    /// location for future objective actions without coupling scene objects to the resolver.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Objective Handler")]
    public class ObjectiveHandler : MonoBehaviour
    {
    }
}
