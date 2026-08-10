# Cinematics

See [`CinematicsAndReadablesSetup.md`](../../docs/Runtime/CinematicsAndReadablesSetup.md)
for complete component setup and generated example-prefab instructions.

`CutsceneSequenceRunner` provides an ordered, scene-authored cutscene made from camera,
dialogue/wait-source, timing, and UnityEvent beats. Existing Lab Partners-style step arrays
remain supported. At runtime, `Play`, `Stop`, and `PlayStep` support normal playback and
isolated beat previews; `Steps` and `CurrentStepIndex` expose read-only debug state.

Use **Tools > Narrative > Cutscene Explorer** to create a starter runner, browse all
cutscenes in loaded scenes, inspect their steps, select their objects, and preview a whole
sequence or individual step in Play Mode. Scene references remain on the runner so camera
poses, dialogue runners, and character controllers are explicit and safe to serialize.

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

The former `QuietStatic.Toolkit.Cinematics.CreditsScroller` type lives only in
the Compatibility assembly. New scenes should use
`QuietStatic.Toolkit.UI.CreditsScroller`.
