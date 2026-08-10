# Scene flow

Scene flow supports a tiny bootstrap scene, persistent system/UI/player scenes, and
replaceable additive content scenes.

## Profile-driven bootstrap

The preferred startup authority is a `SceneBootstrapProfile`. It defines ordered
persistent scenes, the first active content scene, optional initial support/retained
scenes, and whether unrelated scenes (including the tiny bootstrap scene) unload.

Open `Tools > Narrative > Scene Flow Setup` to create a bootstrap profile, scene map,
and request channel; locate and add referenced scenes to Build Settings; and create
bootstrap or manager objects in the current scene.

Recommended layout:

```text
Bootstrap scene
└── SceneBootstrapper (profile)

Persistent systems scene
└── SceneFlowManager (Load Startup Scene On Awake disabled)

Persistent UI/player/audio scenes
└── Project-specific persistent services
```

At runtime `SceneBootstrapper` loads persistent scenes in profile order, waits one
frame for initialization, configures the manager's persistent-scene policy, invokes
`On Persistent Scenes Ready`, and asks the manager to perform the initial faded
transition. Use the ready event for general setup components that must run after all
persistent services exist but before content begins. The transition runs on the
persistent manager, so unloading the bootstrap scene cannot cancel it.

Keep `SceneFlowManager.Load Startup Scene On Awake` disabled when a bootstrap profile
owns startup. Its legacy startup fields and `BootstrapScenes` remain available for
smaller existing projects, but do not configure both startup authorities.

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

Create a `SceneFlowRequestChannel` asset when replaceable content scenes need to issue
commands to the persistent manager. Assign it to both the manager and transition
triggers. The same channel also exposes UnityEvent-friendly additive load, unload, and
active-scene commands. A trigger falls back to `SceneFlowManager`, then the legacy
`SceneLoadService`, when no channel is assigned.

For a full additive content transition, create a `SceneTransitionRequest`.
The request can load support scenes and retain selected nonpersistent scenes
for that transition. Await `SceneFlowManager.TransitionToSceneRoutine` before
applying project-specific spawning, game-state, or narrative policy.

`SceneLoadService` remains the smaller single/additive load primitive used by
`BootstrapScenes` and simple triggers. Do not install two components that both
own the same high-level transition policy; use `SceneFlowManager` as the
content-stack authority when persistent scenes and cleanup are required.

## Faded transitions and connection maps

Assign the persistent UI scene's `ScreenFader` to `SceneFlowManager` and enable
**Fade During Transitions**. A full transition then fades to black, loads the target
and its support scenes, makes the target active, unloads the previous nonpersistent
content, and fades clear. Time scale does not affect either fade.

Create a **Scene Flow Map** from `Assets > Create > Quiet Static Toolkit > Scene Flow`,
then open `Tools > Narrative > Scene Flow Explorer`. Each connection has a stable ID,
source, destination, optional support/retained scenes, and cleanup policy. Scene fields
select from enabled Build Settings scenes. Assign the map and connection ID to a
`SceneTransitionTrigger` to use that configured route; the legacy target-scene fields
remain supported for existing content.

For UnityEvents, add a `SceneTransitionHandler` to the event-owning scene object. Assign
the map and select a connection, assign the request channel, then connect an
interactable's **On Interaction Succeeded** (or a hold/progress interactable's
**On Completed**) to `SceneTransitionHandler.Transition()`. The string-parameter
`TransitionToConnection` and `TransitionToScene` methods are also available for dynamic
UnityEvents, buttons, animation events, and Timeline signals.
