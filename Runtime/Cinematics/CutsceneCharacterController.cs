using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Manages temporary or scene-placed characters during cinematic cutscenes.
    /// </summary>
    /// <remarks>
    /// This component is designed to be driven by cutscene step events, such as UnityEvents
    /// from a sequence runner. Each step can request that a character is spawned, moved,
    /// shown, hidden, rotated toward a target, or told to play an animation.
    ///
    /// Characters are referenced by a string id instead of by direct scene references in
    /// every event. This keeps cutscene setup flexible while still allowing reusable
    /// character prefabs and pre-placed scene instances.
    /// </remarks>
    public class CutsceneCharacterController : MonoBehaviour
    {
        /// <summary>
        /// Defines one character that can be controlled by this cutscene character controller.
        /// </summary>
        /// <remarks>
        /// Each entry maps a unique character id to either a prefab, an already existing
        /// scene instance, or both. Runtime actions then use this id to find or spawn the
        /// correct character.
        /// </remarks>
        [Serializable]
        public class CutsceneCharacterEntry
        {
            [Header("Identity")]
            [Tooltip("Unique id used to reference this character in cutscene actions. Example: Emily, Jock, Creeper.")]
            /// <summary>
            /// Unique id used by character step actions to resolve this character.
            /// </summary>
            public string characterId;

            [Header("Prefab / Existing Instance")]
            [Tooltip("Prefab spawned if no existing instance is assigned or found for this character id.")]
            /// <summary>
            /// Prefab instantiated when this character needs to appear and no existing instance is available.
            /// </summary>
            public GameObject characterPrefab;

            [Tooltip("Optional existing character instance already placed in the scene.")]
            /// <summary>
            /// Optional pre-placed scene instance that should be controlled instead of spawning a new prefab.
            /// </summary>
            public GameObject existingInstance;

            [Header("Runtime")]
            [Tooltip("If true, this character is allowed to remain spawned and reusable between cutscene steps.")]
            /// <summary>
            /// Indicates whether the spawned instance is intended to persist between cutscene steps.
            /// </summary>
            /// <remarks>
            /// This field documents the intended lifetime of the character for Inspector setup.
            /// The current controller keeps spawned instances cached until explicitly despawned.
            /// </remarks>
            public bool keepSpawnedInstance = true;
        }

        /// <summary>
        /// Describes one cutscene action to apply to a character.
        /// </summary>
        /// <remarks>
        /// A single action can spawn or find a character, move it to a marker, set its
        /// active state, rotate it toward a target, and play an animation trigger or state.
        /// These actions are commonly grouped per cutscene step.
        /// </remarks>
        [Serializable]
        public class CharacterStepAction
        {
            [Header("Character")]
            [Tooltip("Character id from the Characters list. The id must match a Cutscene Character Entry.")]
            /// <summary>
            /// Id of the character entry this action should affect.
            /// </summary>
            public string characterId;

            [Header("Spawn / Position")]
            [Tooltip("If true, the controller will spawn the character prefab when no existing instance is found.")]
            /// <summary>
            /// Whether a missing character should be spawned from its configured prefab.
            /// </summary>
            public bool spawnIfMissing = true;

            [Tooltip("Optional transform used as the character's cutscene placement marker.")]
            /// <summary>
            /// Optional transform used as the position and/or rotation source for this action.
            /// </summary>
            public Transform spawnPoint;

            [Tooltip("If true, the character position is set to the spawn point position.")]
            /// <summary>
            /// Whether the character should be moved to <see cref="spawnPoint" />.
            /// </summary>
            public bool setPosition = true;

            [Tooltip("If true, the character rotation is set to the spawn point rotation before any face-target behavior is applied.")]
            /// <summary>
            /// Whether the character should copy the rotation of <see cref="spawnPoint" />.
            /// </summary>
            public bool setRotation = true;

            [Header("Visibility")]
            [Tooltip("If true, this action will explicitly change the character GameObject's active state.")]
            /// <summary>
            /// Whether this action should set the character's active state.
            /// </summary>
            public bool setActive = true;

            [Tooltip("The active state to apply when Set Active is enabled.")]
            /// <summary>
            /// Active state applied to the character when <see cref="setActive" /> is true.
            /// </summary>
            public bool activeState = true;

            [Header("Facing")]
            [Tooltip("If true, the character rotates to look at Target To Face after position/rotation placement.")]
            /// <summary>
            /// Whether the character should rotate toward a target transform.
            /// </summary>
            public bool faceTarget = false;

            [Tooltip("Transform the character should look at when Face Target is enabled.")]
            /// <summary>
            /// Target transform the character should face.
            /// </summary>
            public Transform targetToFace;

            [Tooltip("If true, only rotates around the Y axis so the character stays upright.")]
            /// <summary>
            /// Whether facing should ignore vertical difference and only rotate around the Y axis.
            /// </summary>
            public bool yawOnly = true;

            [Tooltip("If true, rotates over time instead of snapping instantly.")]
            /// <summary>
            /// Whether the character should turn smoothly toward the target.
            /// </summary>
            public bool smoothFaceTarget = true;

            [Tooltip("How long the smooth facing turn should take, in seconds.")]
            [Min(0f)]
            /// <summary>
            /// Duration of the smooth facing turn in seconds.
            /// </summary>
            public float faceDuration = 0.35f;

            [Header("Animation")]
            [Tooltip("Optional Animator to use instead of searching for one on the character or its children.")]
            /// <summary>
            /// Optional animator override used for this action.
            /// </summary>
            public Animator animatorOverride;

            [Tooltip("Animator trigger to fire. Leave blank if unused.")]
            /// <summary>
            /// Optional Animator trigger parameter to fire.
            /// </summary>
            public string animatorTrigger;

            [Tooltip("Animator state name to play directly. Leave blank if unused.")]
            /// <summary>
            /// Optional Animator state name to play directly.
            /// </summary>
            public string animatorStateName;

            [Tooltip("Animator layer used when playing a state directly.")]
            /// <summary>
            /// Animator layer index used by direct state playback.
            /// </summary>
            public int animatorLayer = 0;

            [Tooltip("Normalized start time for direct state playback. 0 starts at the beginning of the state.")]
            /// <summary>
            /// Normalized start time used by direct state playback.
            /// </summary>
            public float normalizedStartTime = 0f;
        }

        [Header("Characters")]
        [Tooltip("Characters that can be controlled by cutscene step actions. Each entry should have a unique Character Id.")]
        /// <summary>
        /// Configured character entries available to this controller.
        /// </summary>
        [SerializeField] private List<CutsceneCharacterEntry> characters = new List<CutsceneCharacterEntry>();

        /// <summary>
        /// Runtime lookup of character ids to currently resolved character instances.
        /// </summary>
        private readonly Dictionary<string, GameObject> spawnedCharacters = new Dictionary<string, GameObject>();

        /// <summary>
        /// Runtime lookup of character ids to active smooth-facing coroutines.
        /// </summary>
        /// <remarks>
        /// This allows a new facing action for the same character to cancel the previous turn
        /// instead of fighting with another coroutine.
        /// </remarks>
        private readonly Dictionary<string, Coroutine> activeFacingRoutines = new Dictionary<string, Coroutine>();

        /// <summary>
        /// Caches scene-placed character instances before cutscene actions begin.
        /// </summary>
        private void Awake()
        {
            CacheExistingInstances();
        }

        /// <summary>
        /// Applies several character actions in order.
        /// </summary>
        /// <param name="actions">The action list to apply. Null lists are ignored.</param>
        /// <remarks>
        /// This is useful when another script wants to trigger a whole cutscene step worth
        /// of character placement and animation behavior at once.
        /// </remarks>
        public void ApplyActions(List<CharacterStepAction> actions)
        {
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                ApplyAction(actions[i]);
            }
        }

        /// <summary>
        /// Applies one character action.
        /// </summary>
        /// <param name="action">The action to apply. Null actions are ignored.</param>
        /// <remarks>
        /// This method can be called from code. UnityEvents cannot easily pass this custom
        /// action type directly, so Inspector-driven workflows can use a separate trigger
        /// component that calls this method with preconfigured actions.
        /// </remarks>
        public void ApplyAction(CharacterStepAction action)
        {
            if (action == null)
            {
                return;
            }

            GameObject character = GetOrSpawnCharacter(action.characterId, action.spawnIfMissing);

            if (character == null)
            {
                GameLogger.Warning(
                    "ApplyAction",
                    this,
                    $"Could not resolve cutscene character with id '{action.characterId}'."
                );
                return;
            }

            ApplyVisibility(character, action);
            ApplyPlacement(character, action);
            ApplyFacing(character, action);
            PlayAnimation(character, action);
        }

        /// <summary>
        /// Returns an already spawned or scene-placed character by id.
        /// </summary>
        /// <param name="characterId">The configured character id to search for.</param>
        /// <returns>The resolved character GameObject, or null if no instance exists.</returns>
        public GameObject GetCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            if (spawnedCharacters.TryGetValue(characterId, out GameObject character))
            {
                return character;
            }

            CutsceneCharacterEntry entry = FindEntry(characterId);

            if (entry != null && entry.existingInstance != null)
            {
                spawnedCharacters[characterId] = entry.existingInstance;
                return entry.existingInstance;
            }

            return null;
        }

        /// <summary>
        /// Returns an existing character instance or spawns one from its configured prefab.
        /// </summary>
        /// <param name="characterId">The configured character id to resolve.</param>
        /// <param name="spawnIfMissing">Whether a prefab should be spawned when no existing instance is found.</param>
        /// <returns>The resolved or newly spawned character GameObject, or null if it cannot be resolved.</returns>
        public GameObject GetOrSpawnCharacter(string characterId, bool spawnIfMissing = true)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            GameObject existingCharacter = GetCharacter(characterId);

            if (existingCharacter != null)
            {
                return existingCharacter;
            }

            if (!spawnIfMissing)
            {
                return null;
            }

            CutsceneCharacterEntry entry = FindEntry(characterId);

            if (entry == null)
            {
                GameLogger.Warning(
                    "GetOrSpawnCharacter",
                    this,
                    $"No cutscene character entry exists for id '{characterId}'."
                );
                return null;
            }

            if (entry.characterPrefab == null)
            {
                GameLogger.Warning(
                    "GetOrSpawnCharacter",
                    this,
                    $"Cutscene character '{characterId}' has no prefab assigned."
                );
                return null;
            }

            GameObject spawned = Instantiate(entry.characterPrefab);
            spawned.name = $"{entry.characterPrefab.name}_{characterId}_Cutscene";

            spawnedCharacters[characterId] = spawned;

            return spawned;
        }

        /// <summary>
        /// Deactivates a resolved character by id.
        /// </summary>
        /// <param name="characterId">The configured character id to hide.</param>
        public void HideCharacter(string characterId)
        {
            GameObject character = GetCharacter(characterId);

            if (character != null)
            {
                character.SetActive(false);
            }
        }

        /// <summary>
        /// Activates a resolved character by id.
        /// </summary>
        /// <param name="characterId">The configured character id to show.</param>
        public void ShowCharacter(string characterId)
        {
            GameObject character = GetCharacter(characterId);

            if (character != null)
            {
                character.SetActive(true);
            }
        }

        /// <summary>
        /// Destroys and forgets a spawned or cached character by id.
        /// </summary>
        /// <param name="characterId">The configured character id to despawn.</param>
        /// <remarks>
        /// This removes the instance from the runtime lookup and destroys the GameObject if
        /// it still exists. Use this when a temporary cutscene-only character should be cleaned up.
        /// </remarks>
        public void DespawnCharacter(string characterId)
        {
            if (!spawnedCharacters.TryGetValue(characterId, out GameObject character))
            {
                return;
            }

            spawnedCharacters.Remove(characterId);

            if (character != null)
            {
                Destroy(character);
            }
        }

        /// <summary>
        /// Adds configured existing scene instances to the runtime character lookup.
        /// </summary>
        private void CacheExistingInstances()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                CutsceneCharacterEntry entry = characters[i];

                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.characterId))
                {
                    continue;
                }

                if (entry.existingInstance == null)
                {
                    continue;
                }

                spawnedCharacters[entry.characterId] = entry.existingInstance;
            }
        }

        /// <summary>
        /// Finds a configured character entry by id.
        /// </summary>
        /// <param name="characterId">The character id to search for.</param>
        /// <returns>The matching entry, or null when no entry matches.</returns>
        private CutsceneCharacterEntry FindEntry(string characterId)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                CutsceneCharacterEntry entry = characters[i];

                if (entry == null)
                {
                    continue;
                }

                if (entry.characterId == characterId)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Applies the active state requested by a character action.
        /// </summary>
        /// <param name="character">The character GameObject to update.</param>
        /// <param name="action">The action containing visibility settings.</param>
        private void ApplyVisibility(GameObject character, CharacterStepAction action)
        {
            if (!action.setActive)
            {
                return;
            }

            character.SetActive(action.activeState);
        }

        /// <summary>
        /// Applies spawn point position and rotation settings from a character action.
        /// </summary>
        /// <param name="character">The character GameObject to move or rotate.</param>
        /// <param name="action">The action containing placement settings.</param>
        private void ApplyPlacement(GameObject character, CharacterStepAction action)
        {
            if (action.spawnPoint == null)
            {
                return;
            }

            if (action.setPosition)
            {
                character.transform.position = action.spawnPoint.position;
            }

            if (action.setRotation)
            {
                character.transform.rotation = action.spawnPoint.rotation;
            }
        }

        /// <summary>
        /// Applies instant or smooth facing behavior from a character action.
        /// </summary>
        /// <param name="character">The character GameObject to rotate.</param>
        /// <param name="action">The action containing facing settings.</param>
        private void ApplyFacing(GameObject character, CharacterStepAction action)
        {
            if (!action.faceTarget || action.targetToFace == null)
            {
                return;
            }

            if (action.smoothFaceTarget && action.faceDuration > 0f)
            {
                SmoothFaceTarget(
                    action.characterId,
                    character.transform,
                    action.targetToFace,
                    action.yawOnly,
                    action.faceDuration
                );
            }
            else
            {
                FaceTarget(character.transform, action.targetToFace, action.yawOnly);
            }
        }

        /// <summary>
        /// Instantly rotates a character transform toward a target.
        /// </summary>
        /// <param name="characterTransform">The character transform to rotate.</param>
        /// <param name="target">The target transform to face.</param>
        /// <param name="yawOnly">Whether to rotate only around the Y axis.</param>
        private void FaceTarget(Transform characterTransform, Transform target, bool yawOnly)
        {
            characterTransform.rotation = GetLookRotation(characterTransform, target, yawOnly);
        }

        /// <summary>
        /// Plays any animation trigger or state requested by the character action.
        /// </summary>
        /// <param name="character">The character GameObject whose Animator should be used.</param>
        /// <param name="action">The action containing animation settings.</param>
        private void PlayAnimation(GameObject character, CharacterStepAction action)
        {
            Animator animator = action.animatorOverride;

            if (animator == null)
            {
                animator = character.GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(action.animatorTrigger))
            {
                animator.SetTrigger(action.animatorTrigger);
            }

            if (!string.IsNullOrWhiteSpace(action.animatorStateName))
            {
                animator.Play(
                    action.animatorStateName,
                    action.animatorLayer,
                    action.normalizedStartTime
                );
            }
        }

        /// <summary>
        /// Starts a smooth turn toward a target, replacing any active turn for the same character id.
        /// </summary>
        /// <param name="characterId">The character id used to track and replace active facing routines.</param>
        /// <param name="characterTransform">The character transform to rotate.</param>
        /// <param name="target">The target transform to face.</param>
        /// <param name="yawOnly">Whether to rotate only around the Y axis.</param>
        /// <param name="duration">How long the turn should take, in seconds.</param>
        private void SmoothFaceTarget(
            string characterId,
            Transform characterTransform,
            Transform target,
            bool yawOnly,
            float duration
        )
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            if (activeFacingRoutines.TryGetValue(characterId, out Coroutine activeRoutine))
            {
                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                }

                activeFacingRoutines.Remove(characterId);
            }

            Coroutine routine = StartCoroutine(SmoothFaceTargetRoutine(
                characterId,
                characterTransform,
                target,
                yawOnly,
                duration
            ));

            activeFacingRoutines[characterId] = routine;
        }

        /// <summary>
        /// Coroutine that rotates a character toward a target over time.
        /// </summary>
        /// <param name="characterId">The character id associated with this turn routine.</param>
        /// <param name="characterTransform">The character transform to rotate.</param>
        /// <param name="target">The target transform to face.</param>
        /// <param name="yawOnly">Whether to rotate only around the Y axis.</param>
        /// <param name="duration">How long the turn should take, in seconds.</param>
        /// <returns>An IEnumerator used by Unity's coroutine system.</returns>
        private IEnumerator SmoothFaceTargetRoutine(
            string characterId,
            Transform characterTransform,
            Transform target,
            bool yawOnly,
            float duration
        )
        {
            if (characterTransform == null || target == null)
            {
                yield break;
            }

            Quaternion startRotation = characterTransform.rotation;
            Quaternion targetRotation = GetLookRotation(characterTransform, target, yawOnly);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (characterTransform == null || target == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                characterTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                yield return null;
            }

            characterTransform.rotation = targetRotation;

            if (activeFacingRoutines.ContainsKey(characterId))
            {
                activeFacingRoutines.Remove(characterId);
            }
        }

        /// <summary>
        /// Calculates the rotation needed for a character to face a target.
        /// </summary>
        /// <param name="characterTransform">The character transform that will face the target.</param>
        /// <param name="target">The target transform to look toward.</param>
        /// <param name="yawOnly">Whether to ignore vertical difference and keep the character upright.</param>
        /// <returns>The desired look rotation, or the current rotation if the direction is too small.</returns>
        private Quaternion GetLookRotation(Transform characterTransform, Transform target, bool yawOnly)
        {
            Vector3 direction = target.position - characterTransform.position;

            if (yawOnly)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return characterTransform.rotation;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
