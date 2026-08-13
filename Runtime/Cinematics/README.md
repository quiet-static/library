# Cinematics

See [`CinematicsAndReadablesSetup.md`](../../docs/Runtime/CinematicsAndReadablesSetup.md)
for complete component setup and generated example-prefab instructions.

`CutsceneSequenceRunner` provides an ordered, scene-authored cutscene made from camera,
dialogue/wait-source, timing, and UnityEvent beats. Existing Lab Partners-style step arrays
remain supported. At runtime, `Play`, `Stop`, and `PlayStep` support normal playback and
isolated beat previews; `Steps` and `CurrentStepIndex` expose read-only debug state.
The runner also implements `ICinematicWaitSource`, so a definition-based
`CinematicScenePlayer` can select and await a complete scene-authored runner as one
activity. Keep the runner's **Play On Start** disabled when the scene player owns startup.

Use **Tools > Quiet Static > Cinematics > Cutscene Explorer** to create a starter runner, browse all
cutscenes in loaded scenes, inspect their steps, select their objects, and preview a whole
sequence or individual step in Play Mode. Scene references remain on the runner so camera
poses, dialogue runners, and character controllers are explicit and safe to serialize.

For director-owned camera shots, give every shot a stable **Shot ID** and a friendly
**Shot Name**. Sequence steps and dialogue-node cues show **Camera Shot** as a dropdown
after their **Camera Director** is assigned, and store the stable ID rather than the
shot's list index. Use the adjacent **Move** button, or the director's **Editor Shot
Preview** section, to move the cutscene camera to that shot in Edit Mode; the transform
and optional field-of-view change are undoable. `CutToShot(int)` remains available for
older serialized UnityEvents. For new arbitrary UnityEvents, configure a
`CutsceneCameraShotTrigger` and invoke its parameterless `Run()` method.
Older dialogue cues display their legacy index explicitly; assign Shot IDs and click
**Migrate** beside the cue to replace that index with the stable ID.

For scene-to-cutscene launches, add `CutsceneTransitionPlayer` to the persistent Systems
scene. Configure a destination scene and runner GameObject name, then call
`PlayConfigured()`, or call `TransitionAndPlay(sceneName, cutsceneName)` from code. The
coordinator uses `SceneFlowManager`, waits until the destination is active and cleanup has
finished, and only then starts the matching runner in that scene.

The cinematic components coordinate screen fades, camera poses, character steps, and
ordered UnityEvent sequences without embedding game-specific story logic.

```text
Cutscene
├── CutsceneSequenceRunner
├── CinematicCutsceneCameraDirector
├── ScreenFader
├── FadeToClearOnStart
└── Steps
    ├── Camera pose + On Started callbacks
    └── Character action + On Finished callbacks
```

Keep progression, scene loading, and objectives in handlers invoked by sequence events.
Use `EndCutsceneWhenDialogueEnds` when dialogue determines sequence completion.

`CutsceneCameraIdle` provides independently configurable position and rotation
noise, runtime enable/disable, and base-pose refresh after a shot change.

`ScreenFader` owns the reusable overlay mechanics. Assign its `CanvasGroup`,
choose its default black/clear durations, and set the startup state. Add
`FadeToClearOnStart` to destination scenes that should reveal themselves after
an additive transition. It can use an explicitly assigned fader or discover
the active persistent fader after waiting one frame and an optional unscaled
delay.

When the fader lives in a separate UI scene, create a **Screen Fade Channel** asset and
assign it to both `SceneFlowManager`/`CutsceneSequenceRunner` and a
`ScreenFadeChannelHandler` beside the `ScreenFader`. Fade requests carry completion state,
so callers still wait until the screen is fully black or clear before continuing. Direct
fader references remain supported as a fallback.

For a project-specific activity, implement `ICinematicWaitSource` and assign
the component to a sequence step's **Wait Source** field. The sequence calls
`Play()` and waits for `IsRunning` to become false. This keeps custom dialogue,
animation, and presentation systems outside the toolkit assembly. Step waits
use unscaled time by default and can be switched to scaled time on the runner.

For presentation changes on individual dialogue lines, add
`DialogueNodeCinematicCue` beside the sequence. Assign the sequence's `DialogueRunner`,
then add one cue per stable `DialogueTree.Node.id`. Each cue can select a camera-director
shot by stable ID, run a `CutsceneCharacterStepTrigger`, and invoke extra scene-local
events. This keeps reusable dialogue assets free of scene camera and character references.

Use `QuietStatic.Toolkit.UI.CreditsScroller` for credits presentation.

## Definition-based cinematics

For new scenes, create a `CinematicDefinition` asset and add it to a project-level
`CinematicDatabase`. Each definition contains ordered beats, and each beat keeps camera,
character animation, activity/dialogue, and timing data in independent tracks.

Add one `CinematicScenePlayer` to the content scene. Its bindings map the stable IDs in the
asset to scene-specific shot markers, camera rigs, Animators, and playable activities. The
player is the convergence point: sequence events, typed beat-ID events, and optional
per-beat UnityEvents are all configured there. This keeps reusable content out of scene
objects while allowing local props, audio, lighting, and gameplay handlers to remain wired
with UnityEvents.

Use **Tools > Quiet Static > Cinematics > Cinematic Database** to create and search definitions, validate
IDs, select assets, and find or play setups in loaded scenes. Existing
`CutsceneSequenceRunner` scenes remain supported and can be migrated incrementally.

### Several cinematics at one location

Give the location's `CinematicScenePlayer` a Location ID, a `CinematicDatabase`, and a
shared `CinematicLaunchChannel`. All definitions in that database can reuse the player's
scene bindings. To enter the scene through a specific cinematic, configure a persistent
`CinematicSceneLauncher` with the destination scene, matching Location ID, cinematic ID,
launch channel, and scene-flow channel, then call `LaunchConfigured()`. `Launch(string)`
allows a UnityEvent or code path to choose the cinematic dynamically.

The launcher records the selection before requesting the transition. On load, only the
player with the matching Location ID consumes it and resolves the cinematic by stable ID.
For normal scene entry with no request, the player can either play its default definition
or remain idle using `Play Default On Start`.
