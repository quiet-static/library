# Managers

Managers coordinate persistent, shared systems. Each manager is focused on one concern
and most derive from `ToolkitSingleton<T>`.

Use the provided prefabs:

```text
SystemManagers
├── SettingsManager
└── PauseManager

GameplayManagers
├── GameStateManager
├── FlagManager
├── DialogueManager
└── CutsceneManager

AudioManagers
├── MusicManager
└── SfxManager

UIManagers
├── DialogueUIManager
└── InteractionUIManager
```

Keep one authoritative instance in a persistent System/UI scene. Scene objects should
raise events or call handlers instead of storing direct manager references.
