# Cinematics

The cinematic components coordinate screen fades, camera poses, character steps, and
ordered UnityEvent sequences without embedding game-specific story logic.

```text
Cutscene
├── CutsceneSequenceRunner
├── CinematicCutsceneCameraDirector
├── ScreenFader
└── Steps
    ├── Camera pose + On Started callbacks
    └── Character action + On Finished callbacks
```

Keep progression, scene loading, and objectives in handlers invoked by sequence events.
Use `EndCutsceneWhenDialogueEnds` when dialogue determines sequence completion.
