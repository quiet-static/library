using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Defines destination-scene behavior for incoming transition conditions.
    /// </summary>
    /// <remarks>
    /// Place one definition in a content scene. After that scene becomes active
    /// and old content is unloaded, <see cref="SceneFlowManager"/> invokes the
    /// first response whose transient condition and persistent flag requirement
    /// both match. Requests without a condition preserve legacy behavior and do
    /// not invoke this definition. Entry actions run before the transition fades
    /// clear.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu(
        "Quiet Static Toolkit/Scene Flow/Scene Transition Definition")]
    public sealed class SceneTransitionDefinition : MonoBehaviour
    {
        /// <summary>
        /// One ordered transition condition and its destination-owned action.
        /// </summary>
        [Serializable]
        public sealed class Response
        {
            [Tooltip("Optional authoring label describing this entry behavior.")]
            [SerializeField] private string label;

            [Tooltip("Incoming transition condition to match. Blank responses are ignored.")]
            [SerializeField] private string conditionId;

            [Tooltip("Optional persistent gameplay flags required in addition to the transition condition.")]
            [SerializeField] private FlagRequirement requirement = new();

            [Tooltip("Invoked when this is the first response whose condition and flag requirement match.")]
            [SerializeField] private UnityEvent onEntered;

            /// <summary>Gets the optional authoring label.</summary>
            public string Label => label ?? string.Empty;

            /// <summary>Gets the normalized transient condition ID.</summary>
            public string ConditionId => Normalize(conditionId);

            /// <summary>Gets the optional persistent flag requirement.</summary>
            public FlagRequirement Requirement => requirement;

            /// <summary>
            /// Returns whether this response matches the incoming condition and
            /// current persistent flag state.
            /// </summary>
            internal bool Matches(string incomingConditionId)
            {
                return string.Equals(
                           ConditionId,
                           incomingConditionId,
                           StringComparison.Ordinal) &&
                       (requirement == null || requirement.IsMet());
            }

            /// <summary>Invokes the configured destination-owned action.</summary>
            internal void Invoke()
            {
                onEntered?.Invoke();
            }
        }

        [Header("Authoring")]
        [Tooltip("Optional map used by the Inspector to select inbound connection IDs. Runtime matching does not require this reference.")]
        [SerializeField] private SceneFlowMap sceneFlowMap;

        [Header("Entry Events")]
        [Tooltip("Invoked for every conditioned transition into this scene, before a matching response.")]
        [SerializeField] private UnityEvent onEntered;

        [Header("Conditional Responses")]
        [Tooltip("Ordered entry behaviors. Only the first matching condition and flag requirement is invoked.")]
        [SerializeField] private Response[] responses;

        /// <summary>Gets the ordered destination responses.</summary>
        public IReadOnlyList<Response> Responses =>
            responses ?? Array.Empty<Response>();

        /// <summary>
        /// Applies one incoming transition condition to this scene definition.
        /// </summary>
        /// <param name="conditionId">
        /// Transient condition carried by the accepted transition request.
        /// </param>
        /// <returns>
        /// True when one conditional response was invoked; otherwise, false.
        /// For a nonempty condition, the general entry event is invoked regardless
        /// of the return value. Empty conditions are a complete no-op.
        /// </returns>
        public bool Apply(string conditionId)
        {
            string normalizedConditionId = Normalize(conditionId);
            if (string.IsNullOrEmpty(normalizedConditionId))
            {
                return false;
            }

            onEntered?.Invoke();

            if (responses != null)
            {
                foreach (Response response in responses)
                {
                    if (response == null ||
                        !response.Matches(normalizedConditionId))
                    {
                        continue;
                    }

                    response.Invoke();
                    return true;
                }
            }

            if (responses != null &&
                responses.Length > 0)
            {
                GameLogger.Warning(
                    nameof(Apply),
                    this,
                    $"{nameof(SceneTransitionDefinition)} in scene '{gameObject.scene.name}' " +
                    $"has no eligible response for condition '{normalizedConditionId}'.");
            }

            return false;
        }

        /// <summary>
        /// Finds the first transition definition belonging to a loaded scene,
        /// including one on an inactive descendant.
        /// </summary>
        public static SceneTransitionDefinition FindInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            SceneTransitionDefinition result = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SceneTransitionDefinition[] definitions =
                    root.GetComponentsInChildren<SceneTransitionDefinition>(true);
                foreach (SceneTransitionDefinition definition in definitions)
                {
                    if (result == null)
                    {
                        result = definition;
                        continue;
                    }

                    GameLogger.Warning(
                        nameof(FindInScene),
                        result,
                        $"Scene '{scene.name}' contains more than one " +
                        $"{nameof(SceneTransitionDefinition)}. Only '{result.name}' will be used.");
                    return result;
                }
            }

            return result;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
