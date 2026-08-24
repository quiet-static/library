# Scene flow

Scene flow supports a tiny bootstrap scene, persistent system/UI/player scenes, and
replaceable additive content scenes.

## Profile-driven bootstrap

The preferred startup authority is a `SceneBootstrapProfile`. It defines ordered
persistent scenes, the first active content scene, optional initial support/retained
scenes, and whether unrelated scenes (including the tiny bootstrap scene) unload.

Open `Tools > Quiet Static > Project Setup` to create a bootstrap profile, scene map,
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
owns startup so only one component can initiate content loading.

Use `SceneReference` fields instead of hand-typed scene names where available. Add every
loadable scene to Build Settings. `SceneTransitionTrigger` should describe local trigger
behavior while `SceneFlowManager` owns the actual transition.

Create a `SceneFlowRequestChannel` asset when replaceable content scenes need to issue
commands to the persistent manager. Assign it to both the manager and transition
triggers. The same channel also exposes UnityEvent-friendly additive load, unload, and
active-scene commands. A trigger calls `SceneFlowManager` directly when no channel is
assigned.

For a full additive content transition, create a `SceneTransitionRequest`.
The request can load support scenes, retain selected nonpersistent scenes, and carry
an optional transient condition ID for destination-owned entry behavior. This condition
is not a saved gameplay flag. Await `SceneFlowManager.TransitionToSceneRoutine` when
other persistent code needs to coordinate with the complete transition.

## Faded transitions and connection maps

Assign the persistent UI scene's `ScreenFader` to `SceneFlowManager` and enable
**Fade During Transitions**. A full transition then fades to black, loads the target
and its support scenes, makes the target active, unloads the previous nonpersistent
content, and fades clear. Time scale does not affect either fade.

Create a **Scene Flow Map** from `Assets > Create > Quiet Static Toolkit > Scene Flow`,
then open the Scene Flow tab in `Tools > Quiet Static > Workspace`. Each connection has a stable ID,
source, destination, optional support/retained scenes, and cleanup policy. Scene fields
select from enabled Build Settings scenes. A mapped request carries its connection ID
as the destination condition, so multiple routes into one scene remain distinguishable.
Assign the map and connection ID to a `SceneTransitionTrigger`; triggers and handlers
dispatch only configured map connections.

Add one `SceneTransitionDefinition` to a destination scene. Its ordered responses each
contain a condition ID, an optional persistent `FlagRequirement`, and a UnityEvent. The
first response whose exact condition and flag requirement match is invoked. A blank
condition is ignored. Requests without a condition do not invoke the definition, which
keeps existing transitions and save restoration unchanged. The definition's general
entry event runs for every conditioned transition into the scene. Assign the same
`SceneFlowMap` to the definition to select inbound connection IDs from its Inspector;
custom IDs remain available for direct transitions.

Destination responses run after the target becomes active and old content is unloaded,
but before the transition fades clear. Use their UnityEvents for scene-owned setup such
as choosing an entrance, placing a spawn target through `SpawnHandler`, starting local
dialogue, or selecting route-specific presentation.

For UnityEvents, add a `SceneTransitionHandler` to the event-owning scene object. Assign
the map and select a connection, assign the request channel, then connect an
interactable's **On Interaction Succeeded** (or a hold/progress interactable's
**On Completed**) to `SceneTransitionHandler.Transition()`. The string-parameter
`TransitionToConnection` and `TransitionToScene` methods are also available for dynamic
UnityEvents, buttons, animation events, and Timeline signals.

## Scene modes

Add one `SceneModeDefinition` to each content scene to declare whether the scene is for
normal play or a cutscene and which game state should become active. Keep one
`SceneModeManager` in the persistent Systems scene; it reads the definition whenever the
active scene changes and applies the configured game state.

Add `SceneModeCameraHandler` to play and cutscene cameras that share a loaded scene set.
Each handler enables its camera and optional audio listener only for its configured mode.
When a content scene is opened directly without the persistent manager, the handler uses
that scene's local definition so isolated scene testing still selects the correct camera.
