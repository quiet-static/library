# ILookInputSource Usage Guide

## Overview

`ILookInputSource` is an interface that standardizes how **look input** (camera movement / aiming) is provided to systems.

It abstracts away input handling so that camera or aiming systems don’t depend directly on Unity input APIs.

---

## What It Defines

`Vector2 Look { get; }`

- X → horizontal look (left/right)
- Y → vertical look (up/down)

---

## Purpose

This interface exists to:

- Decouple camera systems from input systems
- Support multiple input types (mouse, controller, AI, etc.)
- Keep camera/aim logic reusable

Instead of this:

`Input.GetAxis("Mouse X");`

You use:

`inputSource.Look;`

---

## Expected Behavior

`Look` should return a continuous value, not a one-frame event.

```cs
(0, 0) → no movement
(1, 0) → looking right
(0, -1) → looking down
```

Unlike button input, this is:

`continuous (held / delta-based), not discrete`

---

## How To Implement It

---

### Example: Mouse Input (Classic Input System)

```cs
using UnityEngine;
using QuietStatic.Input;

namespace QuietStatic.Input
{
    public class MouseLookInput : MonoBehaviour, ILookInputSource
    {
        [SerializeField] private float sensitivity = 1.0f;

        public Vector2 Look => new Vector2(
            Input.GetAxis("Mouse X") * sensitivity,
            Input.GetAxis("Mouse Y") * sensitivity
        );
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
    public class NewInputSystemLook : MonoBehaviour, ILookInputSource
    {
        [SerializeField] private InputAction lookAction;

        private void OnEnable()
        {
            lookAction.Enable();
        }

        private void OnDisable()
        {
            lookAction.Disable();
        }

        public Vector2 Look => lookAction.ReadValue<Vector2>();
    }
}
```

---

### Example: Controller Input

```cs
public class GamepadLookInput : MonoBehaviour, ILookInputSource
{
    public Vector2 Look => new Vector2(
        Input.GetAxis("RightStickHorizontal"),
        Input.GetAxis("RightStickVertical")
    );
}
```

---

## How To Use It

Any system that needs look input should depend on the interface.

---

### Example: Camera Controller

```cs
using UnityEngine;
using QuietStatic.Input;

public class CameraController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputSourceBehaviour;
    private ILookInputSource inputSource;

    private float yaw;
    private float pitch;

    [SerializeField] private float sensitivity = 100f;

    private void Awake()
    {
        inputSource = inputSourceBehaviour as ILookInputSource;
    }

    private void Update()
    {
        if (inputSource == null)
            return;

        Vector2 look = inputSource.Look;

        yaw += look.x * sensitivity * Time.deltaTime;
        pitch -= look.y * sensitivity * Time.deltaTime;

        pitch = Mathf.Clamp(pitch, -80f, 80f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
```

---

## Inspector Setup Pattern

Unity cannot serialize interfaces directly, so use:

`[SerializeField] private MonoBehaviour inputSourceBehaviour;`

Then cast:

`inputSource = inputSourceBehaviour as ILookInputSource;`

---

## Common Mistakes

**Using discrete input instead of continuous**

Wrong:

`return Input.GetKeyDown(KeyCode.Mouse0);`

This gives:

- Only one-frame values
- No smooth camera movement

---

**Not scaling input**

Raw input is often too fast or too slow.

Always include:

```cs
sensitivity
Time.deltaTime (optional depending on input type)
```

---

**Mixing input types incorrectly**

Mouse input:

- Usually already delta-based

Gamepad input:

- Needs scaling over time

---

## Design Notes

This follows the same pattern as other input interfaces:

`Input Source → Interface → System (Camera / Aim)`

Benefits:

- Swap input systems without rewriting camera logic
- Clean separation of concerns
- Reusable across projects

---

## When To Use This

Use this when:

- You have a camera or aiming system
- You want input abstraction
- You may support multiple input types

Avoid if:

- You are building a very small prototype
- You don’t need flexibility

---

## Relationship to Other Input Interfaces

Typical input layer might include:

```cs
IMovementInputSource
ILookInputSource
IInteractInputSource
IJumpInputSource
```

Each interface:

- Represents one responsibility
- Keeps systems modular

---

## Extension Ideas

If you expand this system:

- Add sensitivity profiles (per device)
- Add inversion options (invert Y axis)
- Add smoothing / filtering
- Add acceleration curves
- Add separate aim vs camera input
