# NPCs

NPCs use a controller plus opt-in behaviors. `NPCModeController` decides which behavior
is active; the NavMesh motor owns movement; animator and look-at components own presentation.

```text
NPC
├── NavMeshAgent
├── NPCController
├── NPCModeController
├── NPCNavMeshMotor
├── NPCAnimatorDriver
├── NPCWanderBehaviour
├── NPCFollowBehaviour
├── NPCDialogueBehaviour
└── Visual
    └── Animator
```

Bake a NavMesh before testing movement. Configure only the behaviors the character uses,
and change modes through the controller/handler API rather than enabling scripts manually.
