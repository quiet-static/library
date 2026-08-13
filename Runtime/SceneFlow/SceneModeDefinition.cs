using QuietStatic.Toolkit.State;
using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>Declares the presentation mode and initial game state of a content scene.</summary>
    /// <remarks>
    /// Place one definition in each active content scene. Persistent systems use it to select
    /// cameras and establish the default input mode without relying on scene-name conventions.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Scene Flow/Scene Mode Definition")]
    public sealed class SceneModeDefinition : MonoBehaviour
    {
        [Tooltip("Primary presentation and control mode used while this scene is active.")]
        [SerializeField] private SceneMode mode = SceneMode.Play;

        [Tooltip("Game state applied when this scene becomes active. Playing and Cutscene are the usual values.")]
        [GameStateId]
        [SerializeField] private string initialGameState = "Playing";

        /// <summary>Gets the declared scene mode.</summary>
        public SceneMode Mode => mode;

        /// <summary>Gets the game state applied when this scene becomes active.</summary>
        public string InitialGameState => initialGameState;
    }
}
