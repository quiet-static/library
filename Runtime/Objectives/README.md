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

Create `ObjectiveDefinition` assets with stable IDs, player-facing text, and an
optional flag-based completion requirement. Add them to one `ObjectiveDatabase`
and assign that database to the persistent `ObjectiveManager`.

The manager owns the active definition and completed-ID history. It completes
configured objectives when their flag requirement becomes true and implements
`ISaveParticipant`, so `SaveManager` automatically captures and restores the
lifecycle state.

Use `ObjectiveHandler` on scene objects for trigger and UnityEvent commands.
Use `ObjectivePresenter` for new UI. `ObjectiveResolver` still supports its
original embedded text entries; assigning a definition to an entry opts that
rule into manager-backed activation without breaking existing serialized data.
