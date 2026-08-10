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

## Temporary input ownership

`InputModeManager` still selects the normal gameplay, UI, or cutscene group from
the global game state. Temporary activities can additionally acquire an
`InputBlockHandle` for one or more `InputBlockGroups`.

Claims compose: a group remains disabled until every owner has disposed its handle.
Changing game state while a claim is active updates the desired mode without bypassing
the block, and releasing a claim reapplies the latest desired mode.

Use `InputContextClaim` when a UnityEvent needs to acquire and release ownership.
The component releases automatically when disabled. Code-owned claims should be kept
for the activity lifetime and disposed on every completion, failure, cancellation, and
destruction path.

`InputSequenceMinigame` acquires a gameplay block by default. Use a separate Input
Action map for minigame actions so disabling the player action map cannot interfere
with prompts.
