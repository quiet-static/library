# Quiet Static Toolkit

A reusable Unity 6 toolkit for narrative, horror, and exploration games. The package
provides focused components for player control, interactions, flags, objectives,
dialogue, cameras, audio, cutscenes, additive scene flow, and common UI.

## Installation

In Unity, open **Window > Package Management > Package Manager**, choose
**Install package from git URL**, and enter:

`https://github.com/quiet-static/library.git`

The copy inside Stolen lives under `Assets/Packages/library` while it is being developed.

## Recommended project structure

The toolkit is designed around persistent system scenes and replaceable content scenes:

```text
Bootstrapper
└── BootstrapScenes

System (loaded once, persistent)
├── SystemManagers
│   ├── SettingsManager
│   └── PauseManager
├── GameplayManagers
│   ├── GameStateManager
│   ├── FlagManager
│   ├── DialogueManager
│   └── CutsceneManager
└── system_callers
    ├── SystemHandler
    ├── GameplayHandler
    ├── FlagHandler
    └── AudioHandler

UI (persistent)
├── UIControllers
├── dialogue_ui
├── interaction_ui
└── hud

Player (persistent when appropriate)
└── Player

Content scene (replaceable)
├── Environment
├── Interactables
├── SpawnPoints
└── SceneTransitionTriggers
```

Managers own shared runtime state. Scene objects should communicate through handlers,
events, or small service-facing components rather than directly finding managers.
Inspector-facing IDs should come from the provided databases.

## Getting started

1. Open the scenes in `Samples` to see bootstrap, system, UI, and prefab composition.
2. Add the manager prefabs from `Runtime/Managers/Prefabs` to a persistent System scene.
3. Assign flag and game-state databases before wiring scene behavior.
4. Add a player prefab and matching camera rig.
5. Load content scenes additively through the Scene Flow components.

Every runtime module contains a README with setup, hierarchy examples, and integration
notes. API comments and Inspector tooltips document individual components and fields.
