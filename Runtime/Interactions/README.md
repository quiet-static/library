# Interactions

`Interactor` searches from the player/camera, while `Interactable` owns an object's
requirements and success/failure behavior. `InteractionHighlighter` handles presentation,
and `InteractableUnlock` reacts to progression flags.

```text
Player
└── Interactor
    └── Interaction Origin: PlayerCamera

Locked Door
├── Collider
├── Interactable
│   ├── Requirement: HasDoorKey
│   └── On Succeeded → Door animation
└── InteractionHighlighter
```

Put behavior in the interactable's UnityEvents or focused handler components. Keep the
global manager out of content-object references.
