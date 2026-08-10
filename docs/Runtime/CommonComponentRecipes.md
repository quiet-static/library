# Common Component Recipes

This page describes repeatable scene setups for the most common Quiet Static Toolkit
behaviors. Start with the recipe closest to the desired player experience, then connect
project-specific animation, audio, state, or story behavior through the exposed UnityEvents.

The ready-made interaction targets are in `Runtime/Interactions/Prefabs`. They contain
behavior and a trigger collider, but deliberately contain no project art. Add a visual child,
resize the collider, and configure the fields described by each recipe.

## Shared foundation

All raycast interactions require these persistent pieces:

1. Add `GameplayManagers` to the persistent System scene. Assign a `FlagDatabase` to its
   `FlagManager` when interactions use requirements or set progression flags.
2. Add `interaction_ui` to the persistent UI scene. Its `InteractionUIManager` supplies the
   normal prompt and hold-progress meter.
3. Add an `Interactor` to the player or gameplay camera.
4. Assign its interaction camera, range, and layer mask.
5. Route the gameplay Interact action to `Interactor.HandleInteractInput`.
6. Put an enabled Collider on every target. Trigger colliders are supported.

The Interactor owns input and target selection. An interactable owns the rules for one
target. Keep shared managers out of content-scene object references; use UnityEvents,
handlers, or ScriptableObject channels at scene boundaries.

All interaction target prefabs include `InteractionHighlighter`. Its renderer list is empty
by design, so it discovers renderers added beneath a project Prefab Variant when it awakens.

## Choose the interaction type

| Desired behavior | Component or prefab |
| --- | --- |
| One press immediately performs an action | `BasicInteractable.prefab` / `Interactable` |
| Send different feedback for different flag states | `ConditionalMessageInteractable.prefab` |
| Hold Interact until the action completes | `HoldInteractable.prefab` / `HoldInteractable` |
| Press once, then wait while work continues | `ActivatedProgressInteractable.prefab` / `ActivatedProgressInteractable` |
| Press once to start dialogue | `DialogueInteractable.prefab` |
| Play one or more 3D sounds on interaction | `AudioInteractable.prefab` |
| Trigger or toggle an Animator | `AnimatedInteractable.prefab` |
| Trigger animation and spatial audio together | `AnimatedAudioInteractable.prefab` |
| One interaction unlocks another | `InteractableUnlock` or `HoldInteractableUnlock` |
| A world object drives behavior in a persistent scene | A typed ScriptableObject channel plus scene-local listeners |

Do not combine interaction types unless the object intentionally has stages. When staged
types share one GameObject, keep later stages disabled until the earlier stage enables them.

## Recipe: basic one-shot interaction

Use this for doors, switches, pickups, inspection messages, animation triggers, and other
actions that happen as soon as Interact is pressed.

1. Drag `Runtime/Interactions/Prefabs/BasicInteractable.prefab` into the content scene.
2. Rename it and resize its Box Collider around the visible object.
3. Set **Display Name** to the verb or object name shown by the interaction UI.
4. Wire **On Interaction Succeeded** to a focused scene component, Animator, AudioSource,
   `SetActiveEvent`, or another UnityEvent-friendly handler.
5. Enable **Disable After Success** for pickups and other one-use actions.
6. Add an `InteractionHighlighter` when the visual should highlight under the crosshair.

For reusable art prefabs, place `Interactable` on the stable root containing the collider.
The collider hit is resolved upward to the nearest interaction-bearing transform.

## Recipe: interaction gated by flags

Use flags for stable story facts such as `HasKey`, `PowerRestored`, or `OpenedTV`.

1. Add the flag IDs to the project's `FlagDatabase` first.
2. On the `Interactable`, set **Requirement Mode**:
   - **All** requires every listed flag.
   - **Any** requires at least one listed flag.
   - **Not All** passes unless every listed flag is active.
   - **Not Any** requires every listed flag to be inactive.
   - **None** disables requirement checking and always passes.
3. Select requirement IDs using the database-backed dropdown.
4. Configure **Flags To Set On Success** for facts established by the action.
5. Optionally configure **Flags To Set On Failure**.
6. Wire **On Interaction Failed** to an `InteractionUIChannel.ShowMessage` call or local
   feedback handler so the player understands what is missing.

Flags describe progression; they should not represent health, frame-by-frame movement,
progress percentages, or other rapidly changing state.

### Conditional interaction messages

Add `ConditionalInteractionMessage` when the feedback itself should change with story state.
The interaction requirement still decides success or failure; this component only selects
and sends text through `InteractionUIChannel`.

1. Assign the project's `InteractionUIChannel`.
2. Add message rules in most-specific-first order.
3. Configure a `FlagRequirement` and message on each rule.
4. Add an optional fallback message.
5. Wire `ShowMessage` to the Interactable success or failure event where the feedback is
   needed. `ConditionalMessageInteractable.prefab` wires it to success by default.
6. Enable custom duration only when this message should override the UI listener's default.

The first matching non-empty rule wins. A rule using Requirement Mode **None** always
matches, so place it last when it is being used as an in-list fallback.

Example failure-message ordering:

```text
1. All [HasFood, OpenedTV] -> "I could sit and eat now."
2. Not Any [HasFood]       -> "I should get dinner first."
3. Not Any [OpenedTV]      -> "I wanted the TV on first."
Fallback                   -> "I couldn't do that yet."
```

Example:

```text
LockedDoor
|-- Collider
|-- Interactable
|   |-- Requirement: All [HasBasementKey, PowerRestored]
|   |-- Flags To Set On Success: [BasementOpened]
|   |-- On Succeeded -> DoorAnimator.Open
|   `-- On Failed -> InteractionUIChannel.ShowMessage("The door is locked.")
`-- InteractionHighlighter
```

## Recipe: hold-to-complete interaction

Use this when the player's continued input represents continued effort: eating, searching,
repairing, cleaning, or forcing a door.

1. Drag `HoldInteractable.prefab` into the scene.
2. Set **Hover Prompt**, **Progress Name**, and **Hold Duration**.
3. Choose whether **Preserve Progress** should retain partial work after release or looking
   away. Leave it disabled when interruption should restart the action.
4. Configure optional flag requirements and completion flags.
5. Wire **On Progress Changed** to visual feedback. The emitted float is normalized from
   zero to one.
6. Wire **On Completed** to the final scene behavior.
7. Enable **Disable After Completion** for a one-use task.

The persistent `InteractionUIManager` automatically displays its configured progress root,
label, and Slider while the hold is active. The prefab does not need its own Canvas.

`HoldInteractableUnlock` can translate normalized progress into an Animator float or a
shrinking visual without introducing project-specific code.

## Recipe: press once and wait for completion

Use this for microwaves, generators, crafting stations, downloads, or any process that keeps
running after the player walks away.

1. Drag `ActivatedProgressInteractable.prefab` into the scene.
2. Set **Hover Prompt**, **Duration**, and **Progress Name**.
3. Move its `ProgressAnchor` child to the desired world-space meter location.
4. Keep the assigned `WorldSpaceProgressBar` prefab, or replace it with a custom instance.
5. Wire **On Started** to startup animation or audio.
6. Wire **On Progress Changed** to optional process visuals.
7. Wire **On Completed** to output activation, flags, objectives, or the next interaction.
8. Use **Use Unscaled Time** only when the process should continue while gameplay time is
   paused.

Unlike a hold interaction, this timer belongs to the world object. Looking away or leaving
range does not cancel it.

## Recipe: dialogue interaction

Use `DialogueInteractable.prefab` when pressing Interact should ask the persistent dialogue
system to play a tree.

1. Ensure the persistent System scene contains `DialogueManager` and the UI scene contains
   `dialogue_ui`.
2. Create or import a `DialogueTree` asset.
3. Drag `DialogueInteractable.prefab` into the content scene.
4. Assign the tree to `DialogueEventPlayer.Default Dialogue`.
5. Optionally assign a camera **Focus Target** and **Speaker** transform.
6. Configure requirements and one-shot behavior on its `Interactable` as needed.
7. Use `DialogueEventPlayer.On Dialogue Started` and **On Dialogue Ended** for local
   animation, objectives, or follow-up interaction stages.

The prefab's success event already calls `DialogueEventPlayer.StartDialogue`. Scene objects
therefore never need a serialized reference to the persistent `DialogueManager`.

Dialogue nodes and choices can also test or set flags. Keep conversation content in the
tree; keep physical scene reactions in the event player or a focused scene handler.

## Recipe: audio interaction

Use `AudioInteractable.prefab` for inspectable props, switches, recordings, and objects
whose immediate result is spatial sound.

1. Assign one clip to `AudioEventPlayer`, or enable multiple clips and choose In Order or
   Random playback.
2. Set minimum distance, maximum distance, and volume.
3. Resize the collider and set the interaction display name.
4. Add other success callbacks after the prefab's existing `AudioEventPlayer.Play` call.

The sound is routed through the persistent `SfxManager`; the content object does not need
an AudioSource or a direct manager reference.

## Recipe: animated or toggleable interaction

Use `AnimatedInteractable.prefab` for doors, drawers, switches, and other Animator-driven
objects.

1. Assign an Animator Controller to the included Animator.
2. Create a trigger matching **Animation On Trigger**; the prefab defaults to `Activate`.
3. For a toggle, enable **Is Binary**, create the off trigger, and set **Animation Off
   Trigger**; the prefab defaults to `Deactivate`.
4. Configure interaction requirements and completion flags normally.
5. `Interactable.On Interaction Succeeded` is already wired to
   `InteractableUnlock.UnlockInteraction`.

The historical `InteractableUnlock` name represents the animation adapter; it can be
called directly or react automatically when its own flag requirement becomes true.

Use `AnimatedAudioInteractable.prefab` when the same interaction should also play a 3D
sound. Both the animation adapter and audio player are already connected to the success
event; assign the project's Animator Controller, trigger names, and audio clips.

## Recipe: input-sequence minigame runner

`Runtime/Minigames/InputSequenceMinigameRunner.prefab` is a persistent, screen-space runner
with its view already connected.

1. Place one runner in the persistent UI or Systems scene.
2. Create an `InputSequenceRequestChannel` and assign it to the runner.
3. Assign an optional cancel action. Leave the runner's default sequence empty when content
   scenes will submit requests through the channel.
4. Add `InputSequenceMinigameActivator` to each requesting content object and assign the
   same channel plus its `InputSequenceDefinition`.
5. Invoke the activator from an interaction and connect its result events to flags,
   objectives, animation, or object state.

The library prefab is derived from the current project's working runner composition, but
project-specific sequence, input-action, and channel references are intentionally cleared.

## Recipe: staged interactions

Use stages when the same object changes interaction type, such as “sit” followed by “hold to
eat,” or “insert fuel” followed by “start generator.”

1. Put the initial `Interactable` and later `HoldInteractable` on the same target root.
2. Disable the later hold at startup with **Start Enabled**.
3. In the initial success event, call a small coordinator that enables the hold.
4. Enable **Disable After Success** on the initial interaction.
5. Let the coordinator forward progress and completion to the components that own their
   effects.

`SeatedHoldSequence` and `EatingSequenceChannel` are examples of this pattern, not a reason
to place food behavior inside the generic interaction system. A content object publishes
sequence events; the persistent player scene owns movement and held-item visuals.

For seated activities, disable **Require Collider Focus** on `SeatedHoldSequence` and assign
the project's `InteractionUIChannel`. Once sitting begins, the sequence reads the held
Interact action directly and publishes its prompt and progress meter through the channel.
The initial sit interaction can still use the furniture collider, but eating no longer needs
a separate trigger collider or continued crosshair focus. Leave the option enabled for
ordinary world-space holds that should cancel when the player looks away.

Assign **Camera Focus Target** when the seated player should initially face an object such
as a television. **Horizontal Look Range** and **Vertical Look Range** keep normal camera
input enabled inside a limited region around that object. The player body remains aligned
to the seat while constrained, and normal camera/body behavior is restored when the
sequence completes or is cancelled.

For audio that exists only while input is held:

1. Add `AudioEventPlayer` and `HoldAudioFeedback` beside the `HoldInteractable`.
2. Assign a loopable clip and its volume/distance on `AudioEventPlayer`.
3. Reference the hold and audio player from `HoldAudioFeedback`.

`HoldAudioFeedback` listens to the hold's begin/end events, so it also stops audio when the
interaction completes, is disabled, or is cancelled—not only when the button is released.

To match a screen-space hold meter to the reusable world-space meter, add
`ScreenSpaceProgressBarStyle` to the meter root and assign `ProgressBarTheme`. Reference the
Slider's background, fill, optional label, and handle objects. The Slider continues to be
driven by `InteractionUIManager`; only its presentation is shared with
`WorldSpaceProgressBar`.

## Recipe: unlock or reveal the next interaction

For a simple same-scene chain:

1. Place the next `Interactable` on its target.
2. Add `InteractableUnlock` to the object that controls its availability.
3. Configure the required flags and whether matching requirements enable or disable it.
4. Set those flags from the preceding interaction, dialogue choice, or objective.

Use `HoldInteractableUnlock` when normalized hold progress should drive a visual in addition
to unlocking behavior.

For cross-scene chains, publish through a ScriptableObject channel rather than referencing
an object from a persistent scene directly.

## Recipe: object visuals with reusable state

Use `ObjectStateHandler` when an object has named visual states such as empty hand, plate,
and pizza-on-plate.

1. Create one `ObjectStateDefinition` asset per stable state.
2. Add `ObjectStateHandler` to the object that owns the visuals.
3. Map each definition to the GameObjects visible in that state.
4. Assign a starting state.
5. Optionally create and assign an `ObjectStateChannel` for cross-scene requests.
6. Invoke `ActivateState` directly for same-scene behavior, or invoke the channel for
   behavior originating elsewhere.

This is preferable to several unrelated `SetActive` calls because the handler guarantees
that only the selected state's objects remain visible.

## Progress bar choices

There are two intentionally different progress presentations:

- Hold interactions use the persistent screen-space meter from `interaction_ui` because the
  player is actively holding input on the current target.
- Activated progress interactions use `WorldSpaceProgressBar.prefab` because the process
  belongs to an object and remains visible after the player looks away.

For project-specific meters, assign a Slider and optional TMP label to the corresponding
component instead of modifying the shared prefab.

## Common troubleshooting

- **No prompt appears:** verify the target has a Collider, its layer is included in the
  Interactor mask, the camera/range are assigned, and no nearer collider occludes it.
- **Input does nothing:** verify the Interact action invokes `HandleInteractInput` and that
  `GameInputManager` is present.
- **Requirements always fail:** verify `FlagManager` has the correct database and the flag
  IDs exist in it.
- **Hold progress immediately resets:** enable **Preserve Progress** if release or looking
  away should retain partial work.
- **A later stage blocks the first stage:** set the later `HoldInteractable.Start Enabled`
  to false and enable it only when the first stage succeeds.
- **Dialogue does not start:** assign a tree and verify the persistent `DialogueManager` and
  dialogue UI are loaded.
- **World meter is missing:** keep the prefab reference assigned and position the
  `ProgressAnchor`, or assign an existing `WorldSpaceProgressBar` instance.
- **A content object needs a manager reference:** add a handler or typed channel at that
  boundary instead of using `FindObjectOfType` or a cross-scene serialized reference.

## Prefab customization rule

Treat toolkit prefabs as starting points. Create a Prefab Variant in the project for custom
art, collider dimensions, prompts, durations, and events. Do not edit package prefabs for a
single game's content; package updates would otherwise mix framework defaults with project
decisions.
