# Cinematics, Scene Fades, and Readable Items

## Generate the examples

Choose **Tools > Quiet Static > Toolkit > Cinematics > Generate Cinematic & Readable Examples**. The generator creates
shared channel assets, sample letter content, and four prefabs under
`Assets/QuietStatic Examples/Cinematics and Readables`. Generation is repeatable and only
updates assets with those example names.

## CutsceneSequenceRunner

Add the runner to a scene object named after the cutscene. Add ordered Steps in the Inspector.
For each step, optionally assign a camera director and select its named shot from the
**Camera Shot** dropdown, then configure a dialogue runner or another
`ICinematicWaitSource`, delays, and start/finish events. The direct camera-rig/pose pair
remains supported for older sequences. Assign a `ScreenFadeChannel` when
the fader is in another scene; otherwise assign a direct `ScreenFader`. Call `Play()` from
an interaction/event, or enable Play On Start. `Stop()` and `PlayStep(index)` are primarily
useful to debug tools.

On `CinematicCutsceneCameraDirector`, give each entry a stable **Shot ID** and a friendly
**Shot Name**. Reordering the list then does not retarget steps or dialogue cues. To frame
a shot without entering Play Mode, select it under **Editor Shot Preview** and choose
**Move Camera to Selected Shot**. The same undoable **Move** action appears beside every
shot dropdown. For a generic UnityEvent, add `CutsceneCameraShotTrigger`, select its shot,
and wire the event to `Run()`.

Existing dialogue cues keep serialized shot indexes for compatibility and label them as
legacy references. After assigning explicit Shot IDs, use the cue's **Migrate** button to
replace the index with its stable ID.

Use **Tools > Quiet Static > Toolkit > Cinematics > Cutscene Explorer** to create a starter runner, search loaded
scenes, select its object, and preview the sequence or one step during Play Mode.

## CutsceneTransitionPlayer

Put this component in the persistent Systems scene. Configure Target Scene and Cutscene
Name, where Cutscene Name is the destination runner GameObject's name. Assign the optional
`SceneFlowRequestChannel`, or allow it to use `SceneFlowManager.Instance`. Invoke
`PlayConfigured()` from an Inspector event, or call
`TransitionAndPlay(sceneName, cutsceneName)` from code. The coordinator waits for scene-flow
completion before finding and playing the runner.

The Stolen debug dashboard exposes the same operation under **Cutscenes > Transition to
Cutscene** and also lists runners already present in loaded scenes.

## ScreenFader

Place the fader on a fullscreen UI `Image` with a `CanvasGroup`. Assign both references,
choose its fade color and durations, and choose whether it starts clear. The fader owns
only the visual fade and can still be referenced directly in small, single-scene setups.

## ScreenFadeChannel

Create it from **Create > Quiet Static Toolkit > Cinematics > Screen Fade Channel**. Use
one shared asset for all systems that address the same fullscreen fader. Assign it to
`SceneFlowManager`, cutscene runners that fade between shots, and the handler in the UI
scene. Requests are completion-aware, so scene or cutscene work waits for the fade.

## ScreenFadeChannelHandler

Add this beside the `ScreenFader` in the separate persistent UI/fader scene. Assign the
shared channel and scene-local fader. Only one enabled handler should listen to a given
channel. The generated `ExampleScreenFader` prefab contains the complete setup.

## SceneFlowManager fade fields

Assign Screen Fade Channel and set Transition Fade Duration. Leave Screen Fader empty for
the cross-scene setup. If the channel has no enabled handler, the manager uses its direct
fader fallback when assigned; otherwise the transition continues without a fade.

## ReadableContentDefinition

Create it from **Create > Quiet Static Toolkit > Interactions > Readable Content**. Enter
the optional title, long-form body, and optional close-control label. Reuse the asset from
any number of item instances. The example generator creates `ExampleLetter`.

Select one or more readable assets and choose **Tools > Quiet Static > Toolkit > Readables > Export
Selected Readable Content JSON...** to migrate them into one narrative-authorer catalog.
The asset filename becomes its stable authoring ID, and round-trip metadata preserves the
original Unity asset and GUID when the JSON is imported again.

## ReadableInteractionTrigger

Add it to the same object as an `Interactable`. Assign the readable content asset and the
shared `InteractionUIChannel`. When that exact interactable succeeds, the trigger sends
the content to the UI scene automatically. `Show()` is also available for manual
UnityEvent wiring. The generated `ExampleReadableLetter` prefab demonstrates this setup.

## ReadableOverlayHandler

Put this in the persistent UI scene and assign:

- the same `InteractionUIChannel` used by item triggers;
- a root `CanvasGroup`;
- a fullscreen translucent backdrop `Image`;
- TextMeshPro title, body, and close-label fields.

Connect the close `Button.onClick` to `Close()`. Configure backdrop opacity and Escape-key
dismissal. Wire On Opened and On Closed to the project's input-lock handler if gameplay
movement/look must pause while reading. The generated `ExampleReadableOverlay` prefab is a
complete functional hierarchy intended as a starting point for visual styling.

## InteractionUIChannel and InteractionUIChannelListener

Use one `InteractionUIChannel` asset across replaceable gameplay scenes and the persistent
UI scene. The existing listener continues routing short prompts, messages, and progress to
`InteractionUIManager`; `ReadableOverlayHandler` independently consumes only Show/Hide
Readable commands. This keeps short interaction feedback and modal documents modular.

## DebugDashboard cutscene controls

Open the dashboard with F3 in Play Mode and enable **Cutscenes**. Search loaded runners,
play all steps, preview one step, or stop playback. For an unloaded destination, enter its
Build Settings scene name and destination runner name, then choose **Transition + Play**.
All debug launches are recorded in Logs & Events.
