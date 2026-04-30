# IInteractInputSource Usage Guide

## Overview

`IInteractInputSource` is a simple interface that standardizes how interaction input is exposed in your project.

Instead of systems directly reading from Unity’s input APIs, they depend on this interface, allowing input to be:

- Swapped easily (keyboard, controller, AI, etc.)
- Tested or mocked
- Decoupled from gameplay systems

---

## What It Defines

`bool Interact { get; }`

- Returns true only on the frame the interact input is pressed
- Intended to behave like GetKeyDown / WasPressedThisFrame

---

Purpose

This interface exists to:

- Decouple input handling from gameplay logic
- Allow multiple input implementations
- Make systems reusable and testable

Instead of doing this everywhere:

`Input.GetKeyDown(KeyCode.E);`

You do:

`inputSource.Interact;`

---

## How To Implement It

Create a class that implements the interface.

---

### Example: Keyboard Input

```cs
using UnityEngine;
using QuietStatic.Input;

namespace QuietStatic.Input
{
    public class KeyboardInteractInput : MonoBehaviour, IInteractInputSource
    {
        public bool Interact => Input.GetKeyDown(KeyCode.E);
    }
}
```

---

### Example: Unity Input System

```cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Input
{
    public class NewInputSystemInteract : MonoBehaviour, IInteractInputSource
    {
        [SerializeField] private InputAction interactAction;

        private void OnEnable()
        {
            interactAction.Enable();
        }

        private void OnDisable()
        {
            interactAction.Disable();
        }

        public bool Interact => interactAction.WasPressedThisFrame();
    }
}
```

---

### Example: AI / Scripted Input

```cs
public class AIInteractInput : IInteractInputSource
{
    public bool Interact { get; private set; }

    public void TriggerInteract()
    {
        Interact = true;
    }

    public void Reset()
    {
        Interact = false;
    }
}
```

---

## How To Use It

Any system that needs interaction input should depend on the interface.

---

### Example: Interaction System

```cs
using UnityEngine;
using QuietStatic.Input;

public class InteractableUser : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputSourceBehaviour;

    private IInteractInputSource inputSource;

    private void Awake()
    {
        inputSource = inputSourceBehaviour as IInteractInputSource;
    }

    private void Update()
    {
        if (inputSource == null)
            return;

        if (inputSource.Interact)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        // interaction logic
    }
}
```

---

### Inspector Setup Pattern

Because Unity does not serialize interfaces directly:

`[SerializeField] private MonoBehaviour inputSourceBehaviour;`

Then cast:

`inputSource = inputSourceBehaviour as IInteractInputSource;`

---

### Expected Behavior

`Interact` should behave like:

```cs
True → only on the frame the input is pressed
False → all other frames
```

Do **NOT** implement it like:

`Held down = true every frame`

That would break most interaction systems.

---

## Common Mistakes

**Using GetKey instead of GetKeyDown**

Wrong:

`public bool Interact => Input.GetKey(KeyCode.E);`

This causes:

- Interaction to trigger every frame
- Spam behavior

---

**Forgetting to cast the interface**

If you don’t cast:

`inputSource = inputSourceBehaviour as IInteractInputSource;`

Then `inputSource` will be null.

---

**Not assigning the MonoBehaviour**

If `inputSourceBehaviour` is not assigned in Inspector:

- No interaction will ever trigger

---

## Design Notes

This follows a common architecture pattern:

`Input Source → Interface → Gameplay System`

Benefits:

- Swappable input systems
- Clean separation of concerns
- Easier debugging and testing

---

## When To Use This

Use this when:

- You want flexible input handling
- You are building reusable systems
- You may support multiple input types

Avoid if:

- Your project is extremely small
- You don’t need abstraction

---

## Relationship to Other Input Interfaces

You will likely have similar interfaces like:

```cs
IMovementInputSource
IJumpInputSource
ICameraInputSource
```

Each one:

- Represents a single responsibility
- Keeps systems modular

---

Extension Ideas

If you expand this system:

- Add interaction types:
  - Hold-to-interact
  - Tap vs hold distinction
- Add interaction context:
  - Which object is targeted
- Add events:
  - OnInteractPressed
- Add buffering:
  - Allow input slightly before interaction is valid
