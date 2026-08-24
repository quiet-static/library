# Cross-scene commands

Commands and notifications have different ownership:

- Use a focused handler for a UnityEvent calling a persistent system from the same
  composed scene hierarchy.
- Use a `CrossSceneCommandChannel` asset when content and receiver scenes must not
  hold references to one another.
- Use events on the authoritative runtime component for observations that may have
  many consumers and do not ask that system to perform work. Always unsubscribe when
  the listener is disabled or destroyed.

Concrete command channels retain UnityEvent-friendly methods such as
`ShowMessage`, `RequestSave`, and `TransitionToScene`. Internally each method emits
one typed `CommandRequested` payload. Persistent receivers should use
`CrossSceneChannelSubscription<T>` so disabling a component or changing its channel
always detaches from the exact asset previously subscribed.

Command receivers consume the typed command stream; lifecycle notifications remain
separate observation events.
