using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Scene-local convergence point that plays a reusable cinematic definition.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Cinematic Scene Player")]
    public sealed class CinematicScenePlayer : MonoBehaviour
    {
        [Serializable] public sealed class ShotBinding
        {
            [Tooltip("Shot ID referenced by a cinematic beat.")] public string id;
            [Tooltip("Camera or rig moved when this shot is selected.")] public Transform cameraTransform;
            [Tooltip("Scene marker supplying the shot position and rotation.")] public Transform pose;
        }

        [Serializable] public sealed class CharacterBinding
        {
            [Tooltip("Character ID referenced by animation commands.")] public string id;
            [Tooltip("Animator controlled for this character.")] public Animator animator;
        }

        [Serializable] public sealed class ActivityBinding
        {
            [Tooltip("Activity ID referenced by a cinematic beat.")] public string id;
            [Tooltip("Scene component invoked by the beat; implement ICinematicWaitSource to make the beat wait.")] public MonoBehaviour component;
        }

        [Serializable] public sealed class BeatEventRoute
        {
            [Tooltip("Beat ID that invokes these scene events.")] public string beatId;
            public UnityEvent onStarted;
            public UnityEvent onFinished;
        }

        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        [Header("Location and Selection")]
        [Tooltip("Logical location shared by all cinematics configured in this scene.")]
        [SerializeField] private string locationId;
        [Tooltip("Catalog containing every cinematic that may play at this location.")]
        [SerializeField] private CinematicDatabase database;
        [Tooltip("Cross-scene selection channel. A matching request overrides the default definition.")]
        [SerializeField] private CinematicLaunchChannel launchChannel;
        [Tooltip("Definition used for direct Play calls and optionally for ordinary scene loads.")]
        [SerializeField] private CinematicDefinition definition;
        [Tooltip("Play the default definition when the scene was loaded without a launch request.")]
        [SerializeField] private bool playDefaultOnStart;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Scene Bindings")]
        [SerializeField] private List<ShotBinding> shots = new();
        [SerializeField] private List<CharacterBinding> characters = new();
        [SerializeField] private List<ActivityBinding> activities = new();

        [Header("Converged Events")]
        [Tooltip("Invoked before the first beat.")] [SerializeField] private UnityEvent onStarted;
        [Tooltip("Invoked after the final beat.")] [SerializeField] private UnityEvent onFinished;
        [Tooltip("Invoked with the stable beat ID whenever a beat starts.")] [SerializeField] private StringEvent onBeatStarted;
        [Tooltip("Invoked with the stable beat ID whenever a beat finishes.")] [SerializeField] private StringEvent onBeatFinished;
        [Tooltip("Optional no-argument UnityEvent routes for specific beat IDs.")] [SerializeField] private List<BeatEventRoute> beatEvents = new();

        private Coroutine routine;
        public CinematicDefinition Definition => definition;
        public bool IsRunning { get; private set; }
        public int CurrentBeatIndex { get; private set; } = -1;
        public string DisplayName => definition != null && !string.IsNullOrWhiteSpace(definition.Id) ? definition.Id : gameObject.name;

        private void Start()
        {
            if (launchChannel != null && launchChannel.TryConsume(locationId, out string requestedId))
            {
                Play(requestedId);
                return;
            }
            if (playDefaultOnStart) Play();
        }
        private void OnDisable() => Stop();

        /// <summary>Plays the assigned definition from its first beat.</summary>
        public void Play()
        {
            if (!IsRunning && definition != null) routine = StartCoroutine(PlayRoutine());
        }

        /// <summary>Selects and plays one of the cinematics available at this location.</summary>
        public void Play(string cinematicId)
        {
            if (IsRunning || database == null) return;
            CinematicDefinition selected = database.Find(cinematicId);
            if (selected == null)
            {
                GameLogger.Warning(nameof(Play), this,
                    $"Cinematic '{cinematicId}' is not present in the database for location '{locationId}'.");
                return;
            }
            definition = selected;
            Play();
        }

        /// <summary>Stops playback without invoking the sequence-finished event.</summary>
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            IsRunning = false;
            CurrentBeatIndex = -1;
        }

        /// <summary>Plays one beat in isolation for scene iteration.</summary>
        public void PlayBeat(int index)
        {
            if (!IsRunning && definition != null && index >= 0 && index < definition.Beats.Count)
                routine = StartCoroutine(PlaySingleRoutine(index));
        }

        private IEnumerator PlayRoutine()
        {
            IsRunning = true;
            onStarted?.Invoke();
            for (int i = 0; i < definition.Beats.Count; i++)
            {
                CurrentBeatIndex = i;
                yield return RunBeat(definition.Beats[i]);
            }
            onFinished?.Invoke();
            Finish();
        }

        private IEnumerator PlaySingleRoutine(int index)
        {
            IsRunning = true;
            CurrentBeatIndex = index;
            yield return RunBeat(definition.Beats[index]);
            Finish();
        }

        private void Finish()
        {
            IsRunning = false;
            CurrentBeatIndex = -1;
            routine = null;
        }

        private IEnumerator RunBeat(CinematicDefinition.Beat beat)
        {
            if (beat == null) yield break;
            InvokeBeatEvents(beat.id, true);
            ApplyShot(beat.cameraShotId);
            ApplyAnimations(beat.characterAnimations);

            if (beat.delayBeforeActivity > 0f) yield return Wait(beat.delayBeforeActivity);
            ActivityBinding activity = Find(activities, beat.activityId, item => item.id);
            if (activity?.component is ICinematicWaitSource waitSource)
            {
                waitSource.Play();
                yield return new WaitUntil(() => activity.component == null || !waitSource.IsRunning);
            }
            else if (activity?.component != null)
            {
                activity.component.SendMessage("Play", SendMessageOptions.DontRequireReceiver);
            }
            if (beat.delayAfterActivity > 0f) yield return Wait(beat.delayAfterActivity);
            InvokeBeatEvents(beat.id, false);
        }

        private void ApplyShot(string id)
        {
            ShotBinding shot = Find(shots, id, item => item.id);
            if (shot?.cameraTransform != null && shot.pose != null)
                shot.cameraTransform.SetPositionAndRotation(shot.pose.position, shot.pose.rotation);
        }

        private void ApplyAnimations(IReadOnlyList<CinematicDefinition.CharacterAnimation> commands)
        {
            if (commands == null) return;
            for (int i = 0; i < commands.Count; i++)
            {
                CinematicDefinition.CharacterAnimation command = commands[i];
                CharacterBinding character = command == null ? null : Find(characters, command.characterId, item => item.id);
                if (character?.animator == null) continue;
                if (!string.IsNullOrWhiteSpace(command.trigger)) character.animator.SetTrigger(command.trigger);
                if (!string.IsNullOrWhiteSpace(command.stateName))
                    character.animator.Play(command.stateName, command.layer, command.normalizedStartTime);
            }
        }

        private void InvokeBeatEvents(string id, bool started)
        {
            if (started) onBeatStarted?.Invoke(id); else onBeatFinished?.Invoke(id);
            for (int i = 0; i < beatEvents.Count; i++)
            {
                BeatEventRoute route = beatEvents[i];
                if (route == null || !string.Equals(route.beatId, id, StringComparison.Ordinal)) continue;
                if (started) route.onStarted?.Invoke(); else route.onFinished?.Invoke();
            }
        }

        private object Wait(float duration)
        {
            return useUnscaledTime
                ? (object)new WaitForSecondsRealtime(duration)
                : new WaitForSeconds(duration);
        }

        private static T Find<T>(List<T> values, string id, Func<T, string> getId) where T : class
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < values.Count; i++)
                if (values[i] != null && string.Equals(getId(values[i]), id, StringComparison.Ordinal)) return values[i];
            return null;
        }
    }
}
