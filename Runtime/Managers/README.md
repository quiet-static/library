# Managers

Managers coordinate persistent, shared systems. Each manager is focused on one
concern and most derive from `ToolkitSingleton<T>`.

Use the provided prefabs:

```text
SystemManagers
|-- SettingsManager
|-- PauseManager
`-- SaveManager

GameplayManagers
|-- GameStateManager
|-- FlagManager
|-- DialogueManager
|-- CutsceneManager
|-- ObjectiveManager
`-- SpawnManager

AudioManagers
|-- MusicManager
`-- SfxManager

UIManagers
|-- DialogueUIManager
`-- InteractionUIManager
```

Keep one authoritative instance in a persistent System/UI scene. Scene objects
should raise events or call handlers instead of storing direct manager
references.

`QuietStatic.Toolkit.State.GameStateManager` is the authoritative game-state
owner. `QuietStatic.SettingsManager` is the authoritative settings owner and
does not reference scene UI. A loaded `SettingsMenuView` binds reusable UI
controls to that state when a settings menu is open.
The deprecated manager types are isolated in
`QuietStatic.Compatibility.Runtime` and remain only for serialized compatibility.

`QuietStatic.PlayerManager` stores only the generic active player root.
Project-owned spawning or character-selection code should call
`SetPlayer(GameObject)` after completing its transition. Consumers can listen
to `OnPlayerChanged(previousPlayer, newPlayer)`; either argument may be null.
The manager deliberately contains no character-switching policy.
