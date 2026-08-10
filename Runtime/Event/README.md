# Event bus

`EventBus<T>` is a small typed publish/subscribe utility for custom data events that
implement `IEvent`.

```csharp
public readonly struct AlarmRaised : IEvent
{
    public AlarmRaised(string room) => Room = room;
    public string Room { get; }
}

EventBus<AlarmRaised>.Subscribe(HandleAlarm);
EventBus<AlarmRaised>.Publish(new AlarmRaised("Kitchen"));
EventBus<AlarmRaised>.Unsubscribe(HandleAlarm);
```

Always unsubscribe when the listener is disabled or destroyed. Use `ToolkitEvents` for
the built-in broad notifications and UnityEvents for explicit scene-local wiring.

## Cross-scene commands

Commands and notifications have different ownership:

- Use a focused handler for a UnityEvent calling a persistent system from the same
  composed scene hierarchy.
- Use a `CrossSceneCommandChannel` asset when content and receiver scenes must not
  hold references to one another.
- Use `ToolkitEvents` or `EventBus<T>` for observations that may have many consumers
  and do not ask an authoritative system to perform work.

Concrete command channels retain UnityEvent-friendly methods such as
`ShowMessage`, `RequestSave`, and `TransitionToScene`. Internally each method emits
one typed `CommandRequested` payload. Persistent receivers should use
`CrossSceneChannelSubscription<T>` so disabling a component or changing its channel
always detaches from the exact asset previously subscribed.

Existing channel-specific events remain available for compatibility. New receivers
should consume the typed command stream so adding a command does not require another
pair of lifecycle subscriptions.
