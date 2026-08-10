using QuietStatic.Toolkit.Dialogue;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Exposes audio-manager requests to scene objects and optionally coordinates
    /// spawned sound effects and music around dialogue sessions.
    /// </summary>
    /// <remarks>
    /// Place this on a persistent handler object. Scene buttons, triggers, and timelines can
    /// reference these methods without holding a direct reference to the music manager.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Audio Handler")]
    [DisallowMultipleComponent]
    public class AudioHandler : MonoBehaviour
    {
        public enum SfxDialogueAction
        {
            KeepPlaying,
            Pause,
            Despawn
        }

        public enum MusicDialogueAction
        {
            KeepPlaying,
            Pause,
            Stop
        }

        [Header("Dialogue Response")]
        [Tooltip("When enabled, this handler responds to every dialogue session.")]
        [SerializeField] private bool respondToDialogue = true;

        [Tooltip("What should happen to EventSound3D instances spawned by SfxManager when dialogue starts.")]
        [SerializeField] private SfxDialogueAction sfxOnDialogueStarted = SfxDialogueAction.Pause;

        [Tooltip("When enabled, SfxManager rejects new EventSound3D spawn requests until dialogue ends.")]
        [SerializeField] private bool blockNewSfxDuringDialogue = true;

        [Tooltip("What should happen to music when dialogue starts.")]
        [SerializeField] private MusicDialogueAction musicOnDialogueStarted = MusicDialogueAction.KeepPlaying;

        [Header("Events")]
        [Tooltip("Invoked after this handler processes a dialogue-started audio request.")]
        [SerializeField] private UnityEvent onDialogueAudioStarted;

        [Tooltip("Invoked after this handler processes a dialogue-ended audio request.")]
        [SerializeField] private UnityEvent onDialogueAudioEnded;

        private bool resumeSfxAfterDialogue;
        private bool resumeMusicAfterDialogue;
        private bool restoreSfxSpawningAfterDialogue;

        private void OnEnable()
        {
            DialogueManager.OnDialogueStarted += HandleDialogueStarted;
            DialogueManager.OnDialogueEnded += HandleDialogueEnded;
        }

        private void OnDisable()
        {
            DialogueManager.OnDialogueStarted -= HandleDialogueStarted;
            DialogueManager.OnDialogueEnded -= HandleDialogueEnded;
            RestorePausedAudio();
        }

        /// <summary>Starts a music clip immediately.</summary>
        public void PlayMusic(AudioClip clip)
        {
            MusicManager.Instance?.PlayMusic(clip);
        }

        /// <summary>Stops the current music immediately.</summary>
        public void StopMusic()
        {
            MusicManager.Instance?.StopMusic();
        }

        /// <summary>Transitions to a music clip using the manager's configured fade.</summary>
        public void PlayWithFade(AudioClip clip)
        {
            MusicManager.Instance?.PlayMusicWithFade(clip);
        }

        /// <summary>Stops the current music using the manager's configured fade.</summary>
        public void StopWithFade()
        {
            MusicManager.Instance?.StopMusicWithFade();
        }

        /// <summary>Sets normalized music volume.</summary>
        /// <param name="volume">Linear volume, normally from zero to one.</param>
        public void SetVolume(float volume)
        {
            MusicManager.Instance?.SetVolume(volume);
        }

        /// <summary>Pauses all currently playing EventSound3D instances spawned by SfxManager.</summary>
        public void PauseSpawnedSfx()
        {
            SfxManager.Instance?.PauseSpawnedSounds();
        }

        /// <summary>Resumes sounds paused by the last SfxManager pause request.</summary>
        public void ResumeSpawnedSfx()
        {
            SfxManager.Instance?.ResumeSpawnedSounds();
        }

        /// <summary>Stops and despawns all EventSound3D instances spawned by SfxManager.</summary>
        public void DespawnSpawnedSfx()
        {
            SfxManager.Instance?.DespawnSpawnedSounds();
        }

        /// <summary>Allows SfxManager to accept future EventSound3D spawn requests.</summary>
        public void EnableSfxSpawning()
        {
            SfxManager.Instance?.EnableSpawning();
        }

        /// <summary>Prevents SfxManager from accepting future EventSound3D spawn requests.</summary>
        public void DisableSfxSpawning()
        {
            SfxManager.Instance?.DisableSpawning();
        }

        /// <summary>Pauses background music while preserving its playback position.</summary>
        public void PauseMusic()
        {
            MusicManager.Instance?.PauseMusic();
        }

        /// <summary>Resumes background music when it is paused.</summary>
        public void ResumeMusic()
        {
            MusicManager.Instance?.ResumeMusic();
        }

        private void HandleDialogueStarted(Object dialogue, Transform focusTarget)
        {
            if (!respondToDialogue)
            {
                return;
            }

            resumeSfxAfterDialogue = false;
            resumeMusicAfterDialogue = false;
            restoreSfxSpawningAfterDialogue = false;

            if (SfxManager.Instance != null)
            {
                if (blockNewSfxDuringDialogue && SfxManager.Instance.IsSpawningEnabled)
                {
                    SfxManager.Instance.DisableSpawning();
                    restoreSfxSpawningAfterDialogue = true;
                }

                switch (sfxOnDialogueStarted)
                {
                    case SfxDialogueAction.Pause:
                        SfxManager.Instance.PauseSpawnedSounds();
                        resumeSfxAfterDialogue = true;
                        break;
                    case SfxDialogueAction.Despawn:
                        SfxManager.Instance.DespawnSpawnedSounds();
                        break;
                }
            }

            if (MusicManager.Instance != null)
            {
                switch (musicOnDialogueStarted)
                {
                    case MusicDialogueAction.Pause:
                        resumeMusicAfterDialogue = MusicManager.Instance.IsPlaying;
                        MusicManager.Instance.PauseMusic();
                        break;
                    case MusicDialogueAction.Stop:
                        MusicManager.Instance.StopMusic();
                        break;
                }
            }

            onDialogueAudioStarted?.Invoke();
        }

        private void HandleDialogueEnded(Object dialogue)
        {
            if (!respondToDialogue)
            {
                return;
            }

            RestorePausedAudio();
            onDialogueAudioEnded?.Invoke();
        }

        private void RestorePausedAudio()
        {
            if (resumeSfxAfterDialogue)
            {
                SfxManager.Instance?.ResumeSpawnedSounds();
            }

            if (resumeMusicAfterDialogue)
            {
                MusicManager.Instance?.ResumeMusic();
            }

            if (restoreSfxSpawningAfterDialogue)
            {
                SfxManager.Instance?.EnableSpawning();
            }

            resumeSfxAfterDialogue = false;
            resumeMusicAfterDialogue = false;
            restoreSfxSpawningAfterDialogue = false;
        }
    }
}
