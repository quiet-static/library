using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Chooses a cinematic before transitioning to its shared location scene.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Cinematic Scene Launcher")]
    public sealed class CinematicSceneLauncher : MonoBehaviour
    {
        [Header("Destination")]
        [SerializeField] private SceneReference targetScene = new();
        [Tooltip("Must match the Location ID on the destination Cinematic Scene Player.")]
        [SerializeField] private string locationId;
        [Tooltip("Stable ID from the destination player's Cinematic Database.")]
        [SerializeField] private string cinematicId;

        [Header("Channels")]
        [SerializeField] private CinematicLaunchChannel launchChannel;
        [SerializeField] private SceneFlowRequestChannel sceneFlowChannel;

        [Header("Events")]
        [SerializeField] private UnityEvent onAccepted;
        [SerializeField] private UnityEvent onRejected;

        /// <summary>Transitions using the Inspector-configured cinematic selection.</summary>
        public void LaunchConfigured() => Launch(cinematicId);

        /// <summary>Selects a cinematic ID and transitions to the configured shared scene.</summary>
        public bool Launch(string requestedCinematicId)
        {
            if (launchChannel == null || !targetScene.IsValid ||
                !launchChannel.Request(locationId, requestedCinematicId))
            {
                onRejected?.Invoke();
                return false;
            }

            var request = new SceneTransitionRequest(targetScene.SceneName);
            bool accepted = sceneFlowChannel != null
                ? sceneFlowChannel.RequestTransition(request)
                : SceneFlowManager.Instance != null && !SceneFlowManager.Instance.IsTransitioning;

            if (accepted && sceneFlowChannel == null)
                SceneFlowManager.Instance.TransitionToScene(request);

            if (!accepted)
            {
                launchChannel.Clear();
                onRejected?.Invoke();
                return false;
            }

            onAccepted?.Invoke();
            return true;
        }
    }
}
