# Quiet Static Toolkit

A reusable Unity 6 toolkit for narrative, horror, and exploration games. The package
provides focused components for player control, interactions, flags, objectives,
dialogue, cameras, audio, cutscenes, additive scene flow, and common UI.

## Installation

In Unity, open **Window > Package Management > Package Manager**, choose
**Install package from git URL**, and enter:

`https://github.com/quiet-static/library.git`

## Workspace development

The canonical package source in the Quiet Static workspace is
`libraries/library`. Do not copy the package into a project's `Assets`
directory; Unity would compile a second set of assemblies and asset GUIDs.

Workspace projects consume this checkout through a local Package Manager dependency.
Keep each project's `Packages/manifest.json` and `packages-lock.json` entries together.
For a standalone release, replace the local path with an immutable Git revision.

## Recommended project structure

The toolkit is designed around persistent system scenes and replaceable content scenes:

```text
Bootstrapper
└── SceneBootstrapper (profile)

System (loaded once, persistent)
├── SystemManagers
│   ├── SettingsManager
│   └── PauseManager
├── GameplayManagers
│   ├── GameStateManager
│   ├── FlagManager
│   ├── DialogueManager
│   └── CutsceneManager
├── SceneFlowManager
└── SceneModeManager

UI (persistent)
├── UIControllers
├── dialogue_ui
├── interaction_ui
└── hud

Player (persistent when appropriate)
└── Player

Content scene (replaceable)
├── SceneModeDefinition
├── Environment
├── Interactables
├── SpawnPoints
└── SceneTransitionTriggers
```

Managers own shared runtime state. Scene objects should communicate through handlers,
events, or small service-facing components rather than directly finding managers.
Inspector-facing IDs should come from the provided databases.

## Getting started

1. Import **Toolkit Examples** from this package's Samples tab in Package Manager (or
   open `Samples` directly while developing the package) to see bootstrap, system, UI,
   interaction, narrative, horror, and prefab composition.
2. Add the manager prefabs from `Runtime/Managers/Prefabs` to a persistent System scene.
3. Assign flag and game-state databases before wiring scene behavior.
4. Add a player prefab and matching camera rig.
5. Load content scenes additively through the Scene Flow components.
6. Use the neutral TextMeshPro title, pause, and settings prefabs under
   `Runtime/UI/Prefabs`, then create prefab variants for game-specific presentation.

Every runtime module contains a README with setup, hierarchy examples, and integration
notes. API comments and Inspector tooltips document individual components and fields.

For step-by-step scene recipes, see
[`docs/Runtime/CommonComponentRecipes.md`](docs/Runtime/CommonComponentRecipes.md). It covers
basic and flag-gated interactions, hold and autonomous progress, dialogue, staged behavior,
object states, reusable prefabs, and troubleshooting.
