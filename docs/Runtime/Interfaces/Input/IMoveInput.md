# IMoveInputSource Usage Guide

## Overview

`IMoveInputSource` defines a standardized way to provide movement input to gameplay systems.

It bundles together:

- Directional movement
- Sprint state
- Jump input (with consumption handling)

This interface is typically used by systems like `ThirdPersonMotor`.

---

## What It Defines

```cs
Vector2 Move { get; }
bool Sprint { get; }
bool ConsumeJump();
```

---

## Purpose

This interface exists to:

- Decouple movement logic from input systems
- Support multiple input implementations (keyboard, controller, AI, etc.)
- Prevent input bugs like repeated jump triggers

Instead of this:

```cs
Input.GetAxis("Horizontal");
Input.GetKey(KeyCode.LeftShift);
Input.GetKeyDown(KeyCode.Space);
```

You use:

```cs
inputSource.Move
inputSource.Sprint
inputSource.ConsumeJump()
```

---

## Input Behavior Expectations

### Move

`Vector2 Move`

- Continuous input
- Range typically `[-1, 1]`
- X = left/right
- Y = forward/back

---

### Sprint

`bool Sprint`

- True while sprint input is held
- Continuous (not one-frame)

---

### ConsumeJump

`bool ConsumeJump()`

- Returns true only once per press
- Automatically clears the jump state after being read

This prevents:

- Double jumps from a single press
- Frame timing issues

---

## How To Implement It

---

### Example: Keyboard Input (Classic Input System)

```cs
using UnityEngine;
using QuietStatic.Input;

namespace QuietStatic.Input
{
    public class KeyboardMoveInput : MonoBehaviour, IMoveInputSource
    {
        private bool jumpQueued;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                jumpQueued = true;
            }
        }

        public Vector2 Move => new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        public bool Sprint => Input.GetKey(KeyCode.LeftShift);

        public bool ConsumeJump()
        {
            if (!jumpQueued)
                return false;

            jumpQueued = false;
            return true;
        }
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
    public class NewInputSystemMove : MonoBehaviour, IMoveInputSource
    {
        [SerializeField] private InputAction moveAction;
        [SerializeField] private InputAction sprintAction;
        [SerializeField] private InputAction jumpAction;

        private bool jumpQueued;

        private void OnEnable()
        {
            moveAction.Enable();
            sprintAction.Enable();
            jumpAction.Enable();

            jumpAction.performed += ctx => jumpQueued = true;
        }

        private void OnDisable()
        {
            moveAction.Disable();
            sprintAction.Disable();
            jumpAction.Disable();

            jumpAction.performed -= ctx => jumpQueued = true;
        }

        public Vector2 Move => moveAction.ReadValue<Vector2>();

        public bool Sprint => sprintAction.IsPressed();

        public bool ConsumeJump()
        {
            if (!jumpQueued)
                return false;

            jumpQueued = false;
            return true;
        }
    }
}
```

---

### Example: AI Input

```cs
public class AIMoveInput : IMoveInputSource
{
    public Vector2 Move { get; set; }
    public bool Sprint { get; set; }

    private bool jumpQueued;

    public void TriggerJump()
    {
        jumpQueued = true;
    }

    public bool ConsumeJump()
    {
        if (!jumpQueued)
            return false;

        jumpQueued = false;
        return true;
    }
}
```

---

## How To Use It

---

### Example: Connecting to ThirdPersonMotor

```cs
using UnityEngine;
using QuietStatic.Input;
using QuietStatic.Characters;

public class PlayerMotorDriver : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputSourceBehaviour;
    [SerializeField] private ThirdPersonMotor motor;

    private IMoveInputSource inputSource;

    private void Awake()
    {
        inputSource = inputSourceBehaviour as IMoveInputSource;

        if (motor == null)
        {
            motor = GetComponent<ThirdPersonMotor>();
        }
    }

    private void Update()
    {
        if (inputSource == null || motor == null)
            return;

        motor.Tick(
            inputSource.Move,
            inputSource.Sprint,
            inputSource.ConsumeJump()
        );
    }
}
```

---

## Inspector Setup Pattern

Unity does not serialize interfaces directly.

Use:

`[SerializeField] private MonoBehaviour inputSourceBehaviour;`

Then cast:

`inputSource = inputSourceBehaviour as IMoveInputSource;`

---

## Why ConsumeJump Exists

Without consumption:

`GetKeyDown → might be missed or duplicated depending on timing`

With consumption:

`Input is buffered and guaranteed to fire once`

This makes your movement system:

- More stable
- Frame-independent
- Less bug-prone

---

## Common Mistakes

**Using GetKey instead of GetKeyDown**

Wrong:

`public bool ConsumeJump() => Input.GetKey(KeyCode.Space);`

This causes:

- Infinite jumping attempts
- Broken jump logic

---

**Not buffering jump input**

If you only check input inside the motor:

  - You may miss the frame the key was pressed

Always queue it in the input layer.

---

**Forgetting to reset jump**

If you don’t clear the flag:

  - Jump will trigger every frame

---

## Design Notes

This follows your overall architecture:

`Input Source → Interface → Motor → State → Animation`

Specifically:

`IMoveInputSource → ThirdPersonMotor → MovementStateController → AnimationController`

Each layer has one responsibility.

---

## When To Use This

Use this when:

- You want clean separation of input and movement
- You want reusable movement systems
- You want to support multiple input types

Avoid if:

- You are making a very small prototype

---

## Extension Ideas

If you expand this later:

- Add crouch:
`bool Crouch`

- Add dodge:
`bool ConsumeDodge()`

- Add analog sprint:
`float SprintIntensity`

- Add input buffering system:
`Jump buffer time (e.g. 0.1s window)`
