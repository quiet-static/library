# Input

`PlayerInputReader` adapts Unity's Input System into the shared gameplay state used
by player components.

```text
Player
├── PlayerInput
├── PlayerInputReader
├── CharacterMotor
├── PlayerController
└── Interactor
```

Assign an Input Actions asset and action map to `PlayerInputReader`. Movement and look
components can depend on `IMoveInputSource` and `ILookInputSource`; continuous hold
activities use `IHoldInteractInputSource`. Interaction presses use the manager's
buffered `QueueInteract`/`ConsumeInteract` path.

## Input for held props

Assign the player's `Interactor` to **Grabbable Interactor** on `PlayerInputReader`.
Add optional Button actions to the same action map and match their names in the reader.
**Throw** uses press and release, **Drop** releases the held prop without throwing it,
and **CancelThrow** clears the charge while keeping the prop held. A missing action or
blank action name leaves that input unbound. The reader cancels a charge if it or its
action map is disabled. See [Grabbable objects](../Interactions/README.md#grabbable-objects)
for the prop and HUD setup.

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
