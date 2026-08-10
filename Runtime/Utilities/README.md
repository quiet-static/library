# Utilities

- `FaceTarget` rotates an object toward a target.
- `SetActiveEvent` exposes activation changes to UnityEvents/animation events.
- `TriggerOnce` invokes a callback only on the first valid trigger entry.

Use these for small, local behaviors. When a component starts coordinating multiple
systems or owning progression state, move that responsibility to a focused handler.

`ObjectStateChannel` is the cross-scene command path for an `ObjectStateHandler`.
Its `ActivateState` and `ClearState` UnityEvent methods emit typed commands without
requiring the caller to reference the scene object that owns the visuals.
