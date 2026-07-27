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
