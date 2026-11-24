# AnimationController Usage Guide

## Overview

`AnimationController` is a reusable Unity component that synchronizes movement state and motor data with an Animator.

Instead of hardcoding animation logic into a specific character, this script:

- Reads movement state from a controller
- Reads speed/grounded data from a motor
- Updates Animator parameters dynamically

This makes it portable across projects and characters.

---

## What It Handles

The controller updates three types of Animator parameters:

| Type    |    Purpose       | Example Name  |
|---------|------------------|---------------|
| Float   | Movement speed   | Speed         |
| Bool    | Grounded state   | IsGrounded    |
| Trigger | Jump start event | Jump          |

All parameters are optional and configurable.

More parameters can be added, however should be configured by your program.

---

## Dependencies

This component requires:

- `Animator`
- `ThirdPersonMotor`
- `MovementStateController`

### Required Components on the Same GameObject

- `Animator`
- `ThirdPersonMotor`
- `MovementStateController`
- `AnimationController`

If motor or stateController are not manually assigned, the script will try to fetch them automatically.

**If missing, the script disables itself.**

---

## Setup Instructions

### **1. Attach Component**

Add AnimationController to your character GameObject.

### **2. Assign References**

In the Inspector:

Motor → `ThirdPersonMotor`
State Controller → `MovementStateController`

If left empty, the script will attempt GetComponent.

### **3. Configure Animator Parameters**

Match these EXACTLY with your Animator:

| Field                  | Description                  |
|------------------------|------------------------------|
| Speed Parameter        | Float parameter for movement |
| Grounded Parameter     | Bool parameter for grounded  |
| Jump Trigger Parameter | Trigger for jump start       |

Example:

```cs
Speed
IsGrounded
Jump
```

### **4. Optional: Disable Parameters**

Leave any parameter name blank to disable it.

Example:

- No jump animation → leave `Jump Trigger Parameter` empty

### **5. Adjust Settings**

| Setting         | Description                    |
|-----------------|--------------------------------|
| Speed Damp Time | Smooths speed transitions      |
| Enable Debug    | Enables logging via GameLogger |

---

## How It Works

*State-Based Animation* using other State based scripts

The script reacts to movement states:

| State   | Behavior                       |
|---------|--------------------------------|
| Idle    | Speed = 0, Grounded = true     |
| Moving  | Speed = motor.NormalizedSpeed  |
| Jumping | Grounded = false, trigger jump |
| Falling | Grounded = false               |

---

### Jump Trigger Logic

The jump trigger fires once per jump using an internal flag:

`hasTriggeredJump`

This prevents:

- Trigger spam
- Animation restarting mid-air

---

### Speed Handling

- Uses *damping* while moving
- Uses *instant set* when idle

This keeps transitions smooth but responsive.

---

## Animator Setup Example

You should have:

### **Parameters**

```cs
Float: Speed
Bool: IsGrounded
Trigger: Jump
```

### **Basic Transitions**

- Idle ↔ Walk/Run → based on Speed
- Grounded → Jump → via Jump trigger
- Jump → Fall → based on IsGrounded = false
- Fall → Land → when IsGrounded = true

---

## Debugging

If `Enable Debug` is ON:

- Logs normalized speed while moving
- Uses `GameLogger`

If something breaks:

### Common Issues

Animations not playing

- Parameter names don’t match Animator EXACTLY

Character stuck in jump

- Animator transitions not set up correctly

Script disables itself

- Missing `ThirdPersonMotor` or `MovementStateController`

---

## Design Notes

This script is intentionally:

- Decoupled from specific characters
- Configurable via Inspector
- Safe to reuse across projects

Key design choices:

- Uses Animator hashes for performance
- Uses state-driven logic instead of polling input
- Allows partial usage (you don’t need all parameters)

---

## When to Use This

Use this when:

- You have a movement system already
- You want clean separation between logic and animation
- You want reusable character controllers

Avoid if:

- You need highly custom animation logic per character
- Your animation system is already tightly integrated

---

## Extension Ideas

If you expand this later, consider:

1. Adding support for:

   - crouch
   - sprint
   - attack states

2. Animation events → gameplay hooks

3. Blend trees driven by direction

4. Separate animation layers
