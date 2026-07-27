# Input

`PlayerInputReader` adapts Unity's Input System into the small movement, look, and
interaction interfaces used by player components.

```text
Player
├── PlayerInput
├── PlayerInputReader
├── CharacterMotor
├── PlayerController
└── Interactor
```

Assign an Input Actions asset and action map to `PlayerInput`. Components can depend on
`IMoveInputSource`, `ILookInputSource`, or `IInteractInputSource`, which makes alternate
AI, replay, or test input sources possible without changing movement code.
