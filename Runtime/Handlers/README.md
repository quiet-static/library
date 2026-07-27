# Handlers

Handlers are UnityEvent-friendly entry points between scene objects and persistent
systems. They keep triggers, animation events, and UI buttons from referencing managers.

```text
system_callers
├── SystemHandler
├── GameplayHandler
├── InteractionHandler
├── FlagHandler
├── ObjectiveHandler
├── PlayerLockHandler
├── PlayerLookHandler
└── AudioHandler
```

Place the handler prefab in the persistent System or Player scene. Drag a handler method
into a scene object's UnityEvent. Prefer one handler responsibility per component.
