# Player

The player stack separates input, motor physics, movement state, and animation.

```text
Player
├── CharacterController
├── PlayerInputReader
├── CharacterMotor
├── PlayerController
├── MovementStateController
├── EntityState
└── Visual
    └── Animator + AnimationController
```

`PlayerController` translates input into intent, `CharacterMotor` performs movement,
`MovementStateController` classifies locomotion, and `AnimationController` presents it.
Assign references on the prefab rather than searching for them every frame.
