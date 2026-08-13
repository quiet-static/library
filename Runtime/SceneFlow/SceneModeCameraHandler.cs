using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>Enables a camera and its listener only for one declared scene mode.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Quiet Static Toolkit/Scene Flow/Scene Mode Camera Handler")]
    public sealed class SceneModeCameraHandler : MonoBehaviour
    {
        [Tooltip("Scene mode in which this camera should render.")]
        [SerializeField] private SceneMode activeMode = SceneMode.Play;

        [Tooltip("Camera controlled by this handler. Uses the local Camera when empty.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Optional listener toggled together with the camera.")]
        [SerializeField] private AudioListener targetAudioListener;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            SceneModeManager.OnSceneModeChanged += HandleSceneModeChanged;
            Refresh();
        }

        private void OnDisable()
        {
            SceneModeManager.OnSceneModeChanged -= HandleSceneModeChanged;
        }

        /// <summary>Refreshes component activation from the currently declared scene mode.</summary>
        public void Refresh()
        {
            SceneMode mode = SceneModeManager.CurrentMode;

            // This supports direct scene testing when the persistent manager is not loaded.
            if (mode == SceneMode.Unspecified)
            {
                SceneModeDefinition localDefinition =
                    SceneModeManager.FindDefinition(gameObject.scene);
                if (localDefinition != null)
                {
                    mode = localDefinition.Mode;
                }
            }

            SetCameraActive(mode == activeMode);
        }

        private void HandleSceneModeChanged(SceneMode mode)
        {
            SetCameraActive(mode == activeMode);
        }

        private void SetCameraActive(bool shouldEnable)
        {
            ResolveDependencies();

            if (targetCamera != null)
            {
                targetCamera.enabled = shouldEnable;
            }

            if (targetAudioListener != null)
            {
                targetAudioListener.enabled = shouldEnable;
            }
        }

        private void ResolveDependencies()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetAudioListener == null)
            {
                targetAudioListener = GetComponent<AudioListener>();
            }
        }
    }
}
