# MovementStateController Usage Guide

## Overview

`MovementStateController` is a lightweight state resolver that determines a character’s high-level movement state based on:

- Grounded status
- Vertical velocity
- Horizontal speed

It does **not** move the character — it only classifies movement into states that other systems (like animation or effects) can use.

---

## What It Handles

The controller resolves movement into the following states:

```cs
Idle
Moving
Jumping
Falling
```

These come from:

`EntityState.MovementState`

---

## Purpose

This script exists to:

- Centralize movement state logic
- Avoid duplicating logic across systems
- Provide a clean interface for animation, VFX, etc.

Instead of every system checking:

- velocity
- grounded
- input

They just read:

`stateController.CurrentState`

---

## Setup Instructions

1. Attach Component

   - Add MovementStateController to your character GameObject.

2. No Required References

    - This script does not require other components directly.

    - You drive it manually by calling:

        `UpdateState(isGrounded, verticalVelocity, speed);`

---

3. Configure Settings

| Setting              | Description                              |
|----------------------|------------------------------------------|
| Idle Speed Threshold | Minimum speed before considered "Moving" |
| Enable Debug         | Enables logging via `GameLogger`         |

---

## How To Use It

### Core Usage Pattern

Call `UpdateState()` every frame from your movement system:

```cs
stateController.UpdateState(
    isGrounded,
    verticalVelocity,
    horizontalSpeed
);
```

---

## Example Integration (Typical)

```cs
void Update()
{
    stateController.UpdateState(
        characterController.isGrounded,
        velocity.y,
        horizontalSpeed
    );
}
```

---

## State Logic Breakdown

**Airborne**

```cs
if NOT grounded:
    if verticalVelocity > 0 → Jumping
    else → Falling
```

**Grounded**

```cs
if speed < threshold → Idle
else → Moving
```

---

## Public API

**CurrentState**

`EntityState.MovementState CurrentState`

- The resolved state for this frame

**PreviousState**

`EntityState.MovementState PreviousState`

- The state from the previous frame
- Useful for detecting transitions

**StateChangedThisFrame**

`bool StateChangedThisFrame`

- True if state changed this frame
- Useful for triggering one-time events

**UpdateState(...)**

`void UpdateState(bool isGrounded, float verticalVelocity, float speed)`

- Main entry point
- Should be called every frame

**SetState(...)**

`void SetState(EntityState.MovementState newState)`

- Manually override the state
- Useful for:
  - cutscenes
  - scripted events
  - special mechanics

---

## Example Usage Patterns

### 1. Animation System

```cs
switch (stateController.CurrentState)
{
    case MovementState.Idle:
        // play idle animation
        break;

    case MovementState.Moving:
        // play run animation
        break;

    case MovementState.Jumping:
        // trigger jump animation
        break;

    case MovementState.Falling:
        // play fall animation
        break;
}
```

### 2. Triggering Events on State Change

```cs
if (stateController.StateChangedThisFrame)
{
    if (stateController.CurrentState == MovementState.Jumping)
    {
        // play jump sound
    }
}
```

---

## Debugging

If `Enable Debug` is ON:

- Logs state transitions using GameLogger

Example log:

`State changed: Idle -> Moving`

---

### Common Issues

**State never changes**

- `UpdateState()` is not being called every frame

**Always stuck in Idle**

- Speed is below `idleSpeedThreshold`

**Jump never triggers**

- `verticalVelocity` is not positive when leaving ground

**Incorrect transitions**
Input values being passed are wrong (especially grounded state)

---

## Design Notes

This script is intentionally:

- Stateless beyond current/previous tracking
- Driven externally (no assumptions about movement system)
- Minimal and reusable

*Key idea:*

- This is a state resolver, not a controller.

---

## When To Use This

Use this when:

- You want clean separation between movement logic and state
- Multiple systems depend on movement state
- You want reusable architecture

Avoid if:

- Your project is extremely simple
- You don’t need state abstraction

---

## Relationship to Other Systems

Typical flow:

`Movement System → MovementStateController → AnimationController`

- Movement system calculates velocity and grounded state
- MovementStateController resolves state
- AnimationController reacts to that state

---

Extension Ideas

If you expand this later:

- Add states:
  - Sprinting
  - Crouching
  - Sliding
- Add events:
  - OnStateEnter
  - OnStateExit
- Add timers:
  - Time in state
