# Handlers

Handlers are UnityEvent-friendly entry points between scene objects and persistent
systems. They keep triggers, animation events, and UI buttons from referencing managers.

```text
system_callers
|-- InteractionHandler
|-- FlagHandler
|-- ObjectiveHandler
|-- SpawnHandler
|-- PlayerLockHandler
|-- PlayerLookHandler
`-- AudioHandler
```

Place handlers in the persistent System or Player scene, or add the focused
handler needed by a trigger. Drag a handler method into the scene object's
UnityEvent. Prefer one handler responsibility per component.

Use a ScriptableObject command channel instead when the caller and receiver live in
independently loaded scenes and therefore cannot safely serialize references to one
another. Channels carry commands; handlers remain the convenient local UnityEvent edge.

`SceneTransitionHandler` bridges successful interactions, buttons, animation events,
and Timeline signals to the persistent scene-flow system. Prefer a configured
`SceneFlowMap` connection and request channel; `Transition()` is the parameterless
UnityEvent entry point. A mapped transition carries its connection ID into the
destination definition; direct transitions can provide an optional custom condition ID.
