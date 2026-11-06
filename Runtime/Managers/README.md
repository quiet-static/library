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

`QuietStatic.PlayerManager` stores only the generic active player root.
Project-owned spawning or character-selection code should call
`SetPlayer(GameObject)` after completing its transition. Consumers can listen
to `PlayerChanged(previousPlayer, newPlayer)`; either argument may be null.
The manager deliberately contains no character-switching policy.

`SfxManager.PauseSpawnedSounds()` remembers the sounds it paused until
`ResumeSpawnedSounds()` is called, including across repeated pause requests.
Sounds paused directly through `EventSound3D` remain under the caller's control.
Pausing existing sounds does not block new spawns; use `DisableSpawning()` when
new playback should also be blocked.

## Game clock

GameClockManager owns continuous game minutes in a retained systems scene. Configure
Seconds Per Game Minute and initial 24-hour time in its Inspector. It respects scaled
Unity time. Query Hour, Minute, Day, MinuteOfDay, TotalMinutes, or DisplayTime; mutate
with SetTime, SetTotalMinutes, AdvanceMinutes, PauseClock and ResumeClock. Changed
fires after every mutation. GameClockChannel provides a cross-scene facade; load the
manager first, because commands are synchronous. The channel stores only a live
binding, never session time in the asset. Game-specific schedules and presentation
belong to consuming projects. Save persistence requires explicit game integration.
