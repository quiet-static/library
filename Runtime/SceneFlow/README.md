# Scene flow

Scene flow supports a tiny bootstrap scene, persistent system/UI/player scenes, and
replaceable additive content scenes.

```text
Bootstrapper
└── BootstrapScenes
    ├── Persistent: System
    ├── Persistent: UI
    ├── Persistent: Player
    └── Startup content: House
```

Use `SceneReference` fields instead of hand-typed scene names where available. Add every
loadable scene to Build Settings. `SceneTransitionTrigger` should describe local trigger
behavior while `SceneFlowManager` owns the actual transition.
