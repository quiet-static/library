# ThirdPersonMotor Usage Guide

## Overview

`ThirdPersonMotor` handles reusable third-person character movement using Unity’s `CharacterController`.

It supports:

- Camera-relative movement
- Walking
- Sprinting
- Smooth turning
- Jumping
- Gravity
- Optional movement state updates

It does **not** read input directly. Another script should collect input and pass it into `Tick(...)`.

---

## Required Components

This script requires:

```cs
CharacterController
ThirdPersonMotor
```

Because of:

```cs
[RequireComponent(typeof(CharacterController))]
```

Unity will automatically add a `CharacterController` if one is missing.

---

## Optional Components

For the full character system, also add:

```cs
MovementStateController
AnimationController
```

Typical setup:

```cs
Player
├── CharacterController
├── ThirdPersonMotor
├── MovementStateController
├── AnimationController
└── PlayerInput / Input Handler Script
```

---

## Inspector Setup

### References

| Field                     | Purpose                               |
|---------------------------|---------------------------------------|
| Camera Transform          | Makes movement relative to the camera |
| Movement State Controller | Updates movement state after movement |

If `Camera Transform` is empty, movement uses world/local X/Z input instead.

---

### Movement Settings

| Field          | Purpose                                                   |
|----------------|-----------------------------------------------------------|
| Walk Speed     | Normal movement speed                                     |
| Sprint Speed   | Faster sprint movement speed                              |
| Rotation Speed | How quickly the character turns toward movement direction |

---

## Jump / Gravity Settings

| Field            | Purpose                             |
|------------------|-------------------------------------|
| Jump Height      | Desired jump height in world units  |
| Gravity          | Downward acceleration               |
| Grounded Gravity | Small downward force while grounded |

Recommended default gravity:

`-9.81`

---

## Core Usage

Call `Tick(...)` once per frame from an input/controller script.

```cs
motor.Tick(moveInput, sprintHeld, jumpPressed);
```

Parameters:

| Parameter   | Type    | Meaning                             |
|-------------|---------|-------------------------------------|
| input       | Vector2 | X = left/right, Y = forward/back    |
| sprint      | bool    | Whether sprint speed should be used |
| jumpPressed | bool    | Whether jump was pressed this frame |

---

## Example Input Script

This script is grabbed from *Death Game Jam* check out the repo for more context.

[Death Game Jam Repo](https://github.com/quiet-static/death-game-jam)

```cs
using System;
using QuietStatic.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Characters.Player
{
    /// <summary>
    /// Input data capture to log and process
    /// </summary>
    public class PlayerInputReader : MonoBehaviour, ILookInputSource
    {
        [Header("Input Actions Asset")]
        /// <summary>
        /// Project wide Input Action set
        /// </summary>
        [SerializeField] private InputActionAsset inputActions;

        // Action map
        private InputActionMap playerActionMap;

        // Individual actions
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction interactAction;

        // Events
        public event Action OnSwitchCharacter;

        // Captured input data
        /// <summary>
        /// Move direction in forward/backward and left/right
        /// </summary>
        public Vector2 Move { get; private set; }

        /// <summary>
        /// Look direction in up/down and left/right
        /// </summary>
        public Vector2 Look { get; private set; }

        /// <summary>
        /// If Jump action was pressed this frame
        /// </summary>
        public bool Jump { get; private set; }

        /// <summary>
        /// If Sprint action is being held this frame
        /// </summary>
        public bool Sprint { get; private set; }

        /// <summary>
        /// If Interact action was pressed this frame
        /// </summary>
        public bool Interact { get; private set; }

        // Debug help
        private string callingClass = "PlayerInputReader";

        private void Awake()
        {
            if (inputActions == null)
            {
                GameLogger.Error(callingClass, gameObject, "Missing an InputActionAsset reference.");
                enabled = false;
                return;
            }

            // Set up action captures
            playerActionMap = inputActions.FindActionMap("Player", true);
            moveAction = playerActionMap.FindAction("Move", true);
            lookAction = playerActionMap.FindAction("Look", true);
            jumpAction = playerActionMap.FindAction("Jump", true);
            sprintAction = playerActionMap.FindAction("Sprint", true);
            interactAction = playerActionMap.FindAction("Interact", true);
        }

        private void OnEnable()
        {
            if (playerActionMap != null)
            {
                playerActionMap.Enable();
            }
        }

        private void OnDisable()
        {
            if (playerActionMap != null)
            {
                playerActionMap.Disable();
            }
        }

        private void Update()
        {
            CaptureMovement();
            CaptureAction();
        }

        /// <summary>
        /// Capture movement input from user (Player movement, jump, sprint)
        /// </summary>
        private void CaptureMovement()
        {
            Move = moveAction.ReadValue<Vector2>();
            Look = lookAction.ReadValue<Vector2>();

            if (jumpAction.WasPressedThisFrame())
            {
                Jump = true;
            }

            Sprint = sprintAction.IsPressed();
        }

        /// <summary>
        /// Capture interact input from user (Interact, fight, etc)
        /// </summary>
        private void CaptureAction()
        {
            Interact = interactAction.WasPressedThisFrame();
        }

        public void OnSwitchCharacters(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
                OnSwitchCharacter?.Invoke();
            }
        }

        public bool ConsumeJump()
        {
            bool jumped = Jump;
            Jump = false;
            return jumped;
        }
    }
}
```

---

## How Movement Works

Each `Tick(...)` does this order:

```CS
CheckGrounded()
HandleJump()
HandleMove()
HandleGravity()
UpdateMovementState()
```

This means:

: **1.**  Grounded state is checked first
: **2.**  Jump can override vertical velocity
: **3.**  Horizontal movement is applied
: **4.**  Gravity is applied
: **5.**  Movement state is updated for animation or gameplay systems

---

## Camera-Relative Movement

If `Camera Transform` is assigned:

```CS
input.y = move forward relative to camera
input.x = move sideways relative to camera
```

This gives normal third-person controls.

If no camera is assigned:

```CS
input.x = world/local X movement
input.y = world/local Z movement
```

---

## Public Properties

### VerticalVelocity

`public float VerticalVelocity`

Used by animation/state systems to detect jumping or falling.

---

### NormalizedSpeed

`public float NormalizedSpeed`

Returns horizontal speed normalized against sprintSpeed.

Example:

```CS
0.0 = idle
0.5 = walking-ish
1.0 = sprinting/full speed
```

---

### IsGrounded

`public bool IsGrounded`

Returns whether the CharacterController is currently grounded.

---

## Relationship to MovementStateController

If assigned, this motor automatically updates the movement state:

`ThirdPersonMotor → MovementStateController → AnimationController`

The motor sends:

```cs
IsGrounded
VerticalVelocity
NormalizedSpeed
```

Then `MovementStateController` resolves the state.

---

## Common Issues

**Character does not move**

Check:

- `Tick(...)` is being called every frame
- Input values are not always `(0, 0)`
- `Walk Speed` and `Sprint Speed` are above `0`
- `CharacterController` is enabled

---

**Character does not jump**

Check:

- jumpPressed is true only on the frame jump is pressed
- Character is grounded
- Jump Height is above 0
- Gravity is negative

---

**Character moves in weird directions**

Check:

- `Camera Transform` is assigned correctly
- Camera is not tilted in a strange way
- The player model’s forward direction matches Unity’s forward axis

---

**Character floats or jitters on slopes**

Try adjusting:

```cs
Grounded Gravity
CharacterController Skin Width
CharacterController Step Offset
CharacterController Slope Limit
```

---

## Design Notes

This script is intentionally input-agnostic.

That means it can work with:

- Old Unity Input Manager
- New Unity Input System
- AI controllers
- Replay systems
- Cutscene controllers

Anything can drive it as long as it calls:

`Tick(Vector2 input, bool sprint, bool jumpPressed)`

---

## Extension Ideas

Possible future additions:

- Crouching
- Dashing
- Air control
- Root motion support
- Slope sliding
- Step sounds
- Events like OnJump, OnLand, or OnSprintStart
