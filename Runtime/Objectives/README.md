# Objectives

Objectives use reusable definitions and a persistent lifecycle owner:

```text
GameplayManagers
`-- ObjectiveManager
    `-- ObjectiveDatabase

Scene
`-- ObjectiveHandler (UnityEvent commands)

UI
`-- ObjectivePresenter (title and description)
```

Create `ObjectiveDefinition` assets with stable IDs, player-facing text, and
optional flag-based activation and completion requirements. Add them to one
`ObjectiveDatabase` from lowest to highest activation priority and assign that
database to the persistent `ObjectiveManager`.

The manager owns resolution, the active definition, and completed-ID history. It
selects the highest-priority eligible definition, completes configured objectives
when their flag requirement becomes true, and implements `ISaveParticipant`, so
`SaveManager` automatically captures and restores the lifecycle state.

Use `ObjectiveHandler` on scene objects for trigger and UnityEvent commands.
Use `ObjectivePresenter` for UI. It derives all displayed text from the manager's
active definition and may show fallback text while no objective is active.
