# Interactions

See [`CinematicsAndReadablesSetup.md`](../../docs/Runtime/CinematicsAndReadablesSetup.md)
for the complete readable-item overlay recipe and example generator.

For complete Inspector-oriented setup recipes and reusable target prefabs, see
[`docs/Runtime/CommonComponentRecipes.md`](../../docs/Runtime/CommonComponentRecipes.md).

`Interactor` selects one target from the center of a camera, while
`Interactable` owns an object's requirements and success/failure behavior.
`InteractionHighlighter` handles presentation, and `InteractableUnlock` reacts
to progression flags.

```text
Player
`-- Interactor
    `-- Interaction Origin: PlayerCamera

Locked Door
|-- Collider
|-- Interactable
|   |-- Requirement: HasDoorKey
|   `-- On Succeeded -> Door animation
`-- InteractionHighlighter
```

Put behavior in the interactable's UnityEvents or focused handler components.
Keep the global manager out of content-object references.

Use an `InteractionUIChannel` when prompts or messages originate in replaceable
content scenes. Its persistent listener consumes a single typed command stream;
prompt, progress-meter, and message methods remain suitable for UnityEvents.

## Letters and readable items

Create a **Readable Content** asset for each letter, note, or other long-form item. Add
`ReadableInteractionTrigger` beside the item's `Interactable`, then assign the content and
the shared `InteractionUIChannel`. A successful interaction automatically publishes the
readable without the item referencing a UI scene. Use the trigger's **On Opened** event for
behavior that begins once the overlay is visible. Use **On Closed** for follow-up thoughts,
dialogue, flags, objectives, or other behavior that should occur after the player finishes
reading. These events belong to the placed trigger so they can safely reference scene handlers.

In the persistent UI scene, add `ReadableOverlayHandler` and assign the same channel, a
root `CanvasGroup`, translucent backdrop `Image`, and TextMeshPro title/body/close labels.
Connect the close button to `ReadableOverlayHandler.Close()`. The handler blocks UI
raycasts while visible and can close via Escape; its opened/closed events can be wired to
the project's input-lock handler. The backdrop opacity and all displayed text remain
Inspector-configurable.

Use `ConditionalInteractionMessage` when a success or failure UnityEvent should send
different text for different flag states. Its ordered rules evaluate `FlagRequirement`
objects and publish the first matching message through the same channel.

## Project-owned targets

Projects can keep specialized interaction policy without adding it to the
toolkit. Implement `IInteractionTarget` on the project component and the same
`Interactor` will select and invoke it. Implement `IInteractionFocusReceiver`
when the project needs to own prompt or highlight presentation. Otherwise the
Interactor uses the target's `DisplayName` and a child
`InteractionHighlighter`.

This is the intended boundary for character identities, story rules, or
project-specific event payloads. The centralized Interactor remains the sole
selection and input authority.

## Third-person setup

Put the `Interactor` on the gameplay camera and use a longer camera ray than
the player's physical reach:

1. Assign the gameplay camera and an interaction layer mask.
2. Enable **Use Active Player As Origin**.
3. Enable **Require Interaction Origin In Range** and set the physical reach.
4. Enable **Ignore Active Player Colliders** so the visible player body cannot
   block the camera ray.
5. Route the Interact input action to `HandleInteractInput()`.

The camera ray answers "what is the player aiming at?" while the origin-distance
check answers "can the active player reach it?" Trigger colliders remain valid
raycast targets because the Interactor queries colliders with
`QueryTriggerInteraction.Collide`; they no longer imply proximity-driven input.

## Hold interactions

Use `HoldInteractable` for continuous work such as sweeping or bathing. It
remains separate from the one-shot `Interactable`, but the player's
`Interactor` detects both with the same raycast and uses the same Interact
action.

1. Add a collider and `HoldInteractable` to the world object.
2. Configure its hover prompt, meter name, duration, requirements, and
   completion event.
3. Assign the optional progress root, label, and Slider on
   `InteractionUIManager`.
4. Add `HoldInteractableUnlock` to a visual and select Animator Float or Shrink.

The progress UnityEvent emits a normalized value every frame. Completion
events can be wired to a scene handler that advances the project's clock; the
content object does not need to reference a persistent time manager directly.

## Cross-scene player activities

`HoldActivitySequence` can disable **Require Collider Focus** when a locked activity
should consume held interaction input without continued aiming. In that mode it
temporarily removes the hold from `Interactor` raycast selection and sends its
prompt and progress through `InteractionUIChannel`. Assign any component implementing
`IHoldInteractInputSource`, or leave it empty to use the active `GameInputManager` as a
compatibility fallback.

The same sequence can publish an optional camera focus target and yaw/pitch ranges through
`PlayerActivityChannel`. The persistent player handler applies a limited-look region to
`CameraController` while keeping positional movement locked. `PlayerActivityHandler` may
also scale an optional progress visual and activate an `ObjectStateDefinition` on
completion. Add `HoldAudioFeedback` beside
a hold and configure an `AudioEventPlayer` when looping audio should follow held input.

New content should use `PlayerActivityContext`, `PlayerActivityChannel`,
`HoldActivitySequence`, and `PlayerActivityHandler`. The former eating-named types are
obsolete adapters retained only for source compatibility.

## Activated progress interactions

Use `ActivatedProgressInteractable` when one press should start work that
continues on its own. Assign the reusable `WorldSpaceProgressBar` prefab and,
optionally, an attachment-point Transform. The component instantiates and
controls its own meter. An existing progress-bar child can be assigned instead
when a scene needs custom placement. The player may look or walk away after
starting it; the object owns its timer and fills its attached 3D meter until
completion.
