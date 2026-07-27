# Utilities

- `FaceTarget` rotates an object toward a target.
- `SetActiveEvent` exposes activation changes to UnityEvents/animation events.
- `TriggerOnce` invokes a callback only on the first valid trigger entry.

Use these for small, local behaviors. When a component starts coordinating multiple
systems or owning progression state, move that responsibility to a focused handler.
