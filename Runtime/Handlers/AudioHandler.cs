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
        [Header("Requests")]
        [Tooltip("Required channel used for all persistent audio commands.")]
        [RequiredCommandChannel]
        [SerializeField] private AudioRequestChannel requestChannel;

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
        private DialogueManager observedDialogue;

        private void OnEnable()
        {
            observedDialogue = DialogueManager.Instance;
            if (observedDialogue != null)
            {
                observedDialogue.DialogueStarted += HandleDialogueStarted;
                observedDialogue.DialogueEnded += HandleDialogueEnded;
            }
        }

        private void OnDisable()
        {
            if (observedDialogue != null)
            {
                observedDialogue.DialogueStarted -= HandleDialogueStarted;
                observedDialogue.DialogueEnded -= HandleDialogueEnded;
                observedDialogue = null;
            }
            RestorePausedAudio();
        }

        /// <summary>Starts a music clip immediately.</summary>
        public void PlayMusic(AudioClip clip)
        {
            requestChannel?.PlayMusic(clip);
        }

        /// <summary>Stops the current music immediately.</summary>
        public void StopMusic()
        {
            requestChannel?.StopMusic();
        }

        /// <summary>Transitions to a music clip using the manager's configured fade.</summary>
        public void PlayWithFade(AudioClip clip)
        {
            requestChannel?.PlayWithFade(clip);
        }

        /// <summary>Stops the current music using the manager's configured fade.</summary>
        public void StopWithFade()
        {
            requestChannel?.StopWithFade();
        }

        /// <summary>Sets normalized music volume.</summary>
        /// <param name="volume">Linear volume, normally from zero to one.</param>
        public void SetVolume(float volume)
        {
            requestChannel?.SetVolume(volume);
        }

        /// <summary>Pauses all currently playing EventSound3D instances spawned by SfxManager.</summary>
        public void PauseSpawnedSfx()
        {
            requestChannel?.PauseSpawnedSfx();
        }

        /// <summary>Resumes sounds paused by the last SfxManager pause request.</summary>
        public void ResumeSpawnedSfx()
        {
            requestChannel?.ResumeSpawnedSfx();
        }

        /// <summary>Stops and despawns all EventSound3D instances spawned by SfxManager.</summary>
        public void DespawnSpawnedSfx()
        {
            requestChannel?.DespawnSpawnedSfx();
        }

        /// <summary>Allows SfxManager to accept future EventSound3D spawn requests.</summary>
        public void EnableSfxSpawning()
        {
            requestChannel?.EnableSfxSpawning();
        }

        /// <summary>Prevents SfxManager from accepting future EventSound3D spawn requests.</summary>
        public void DisableSfxSpawning()
        {
            requestChannel?.DisableSfxSpawning();
        }

        /// <summary>Pauses background music while preserving its playback position.</summary>
        public void PauseMusic()
        {
            requestChannel?.PauseMusic();
        }

        /// <summary>Resumes background music when it is paused.</summary>
        public void ResumeMusic()
        {
            requestChannel?.ResumeMusic();
        }

        /// <summary>Assigns the persistent audio request channel.</summary>
        public void SetRequestChannel(AudioRequestChannel value) => requestChannel = value;

        private void HandleDialogueStarted(Object dialogue, Transform focusTarget)
        {
            if (!respondToDialogue)
            {
                return;
            }

            resumeSfxAfterDialogue = false;
            resumeMusicAfterDialogue = false;
            restoreSfxSpawningAfterDialogue = false;

            if (blockNewSfxDuringDialogue)
            {
                requestChannel?.DisableSfxSpawning();
                restoreSfxSpawningAfterDialogue = true;
            }

            switch (sfxOnDialogueStarted)
            {
                case SfxDialogueAction.Pause:
                    requestChannel?.PauseSpawnedSfx();
                    resumeSfxAfterDialogue = true;
                    break;
                case SfxDialogueAction.Despawn:
                    requestChannel?.DespawnSpawnedSfx();
                    break;
            }

            switch (musicOnDialogueStarted)
            {
                case MusicDialogueAction.Pause:
                    resumeMusicAfterDialogue = true;
                    requestChannel?.PauseMusic();
                    break;
                case MusicDialogueAction.Stop:
                    requestChannel?.StopMusic();
                    break;
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
                requestChannel?.ResumeSpawnedSfx();
            }

            if (resumeMusicAfterDialogue)
            {
                requestChannel?.ResumeMusic();
            }

            if (restoreSfxSpawningAfterDialogue)
            {
                requestChannel?.EnableSfxSpawning();
            }

            resumeSfxAfterDialogue = false;
            resumeMusicAfterDialogue = false;
            restoreSfxSpawningAfterDialogue = false;
        }
    }
}
