# Saving

`SaveManager` writes versioned JSON slots beneath `Application.persistentDataPath`.
It coordinates systems that already own authoritative state rather than replacing them.

Version 1 stores:

- the active content scene;
- the spawn point used when the slot is loaded;
- active progression flags; and
- JSON payloads from explicitly implemented `ISaveParticipant` components.

Add one `SaveManager` to the persistent Systems scene. Create a
`SaveRequestChannel` asset and assign it to the manager when content scenes should
request save operations without referencing the persistent object.
The manager consumes the channel's typed `SaveCommand` stream; the asset's
`RequestSave`, `RequestLoad`, and `RequestDelete` methods remain the UnityEvent API.

Use `Checkpoint.Save()` from a UnityEvent, or enable its trigger behavior. The
checkpoint's arrival spawn ID should match a `SpawnPoint` in the saved scene. The
player must be registered with `SpawnManager` under the Save Manager's configured
target ID before restoration can place it.

`ISaveParticipant` is intentionally opt-in. IDs must remain stable and unique, and
participants own the schema inside their JSON payload. Do not use it to serialize
arbitrary scene hierarchies.

Save files keep a `.bak` copy when an existing slot is replaced. Increase
`SaveGameData.CurrentVersion` only alongside explicit migration support.

Use `TryGetSlotMetadata` to populate save-menu rows without loading gameplay.
Metadata includes the scene, arrival point, schema version, UTC timestamp, and
whether the primary file had to be recovered from its backup.

Add `ObjectStateSaveParticipant` beside an `ObjectStateHandler` to persist that
handler. Give the participant a globally unique save ID and ensure every referenced
`ObjectStateDefinition` has a stable ID. This remains opt-in so decorative state
handlers do not silently increase save scope.
