# NPCs

NPCs use a controller plus opt-in behaviors. `NPCModeController` decides which behavior
is active; the NavMesh motor owns movement; animator and look-at components own presentation.

```text
NPC
├── NavMeshAgent
├── NPCController
├── NPCModeController
├── NPCNavMeshMotor
├── NPCDoorOpener                      optional cross-cutting path-door support
├── NPCAnimatorDriver
├── NPCWanderBehaviour
├── NPCWaypointRouteBehaviour
├── NPCFollowBehaviour
├── NPCDialogueBehaviour
└── Visual
    └── Animator
```

Bake a NavMesh before testing movement. Configure only the behaviors the character uses,
and change modes through the controller/handler API rather than enabling scripts manually.

## Authored waypoint routes

Use `NPCWaypointRouteBehaviour` when an NPC needs deliberate scene-authored stops rather
than random-radius wandering:

1. Create a scene object with `NPCWaypointRoute`.
2. Add child objects with `NPCWaypoint`, then use **Refresh Waypoints From Children** on
   the route to capture hierarchy order.
3. Configure each waypoint's wait range, arrival distance, optional facing, destination
   jitter, and optional Animator trigger.
4. Add `NPCWaypointRouteBehaviour` to an NPC that already has `NPCController`,
   `NPCNavMeshMotor`, and a `NavMeshAgent`.
5. Assign `NPCAnimationTrigger` when waypoint arrival should request an Animator trigger.
   `NPCAnimatorDriver` continues to own the normal locomotion Speed parameter.

Routes are scene components because their positions are scene data. Multiple NPCs may
share one route safely: every route behavior maintains an independent cursor and randomized
destination offset. `Once` supports finite arrival/departure paths; `Loop`, `PingPong`, and
`Random` support ambient background movement. A completed or unreachable route can also be
restarted or advanced from scene logic through the component's public methods.

Do not activate two movement behaviors on the same NPC at once. Put follow, wander, and
waypoint-route behaviors in mutually exclusive `NPCModeController` modes when a character
changes roles at runtime.

## Service queues

Use a scene-local `NPCQueueController` when NPCs must enter in a fixed order, optionally
occupy waiting positions, pause for game-specific service, and then leave. Add
`NPCQueueMember` beside each NPC's `NPCController` and `NPCNavMeshMotor`, assign any
pre-service stops, and list the members on the queue controller in service order.

The queue owns only spatial progression. Dialogue, interactions, transactions, objectives,
and save data stay in the scene handler: listen for `MemberReadyForService`, call
`BeginService` when the player engages the NPC, and call `CompleteService` when that game's
service step finishes. `CancelQueue`, `ResetQueue`, `PauseQueue`, `ResumeQueue`, and runtime
`Enqueue` support reuse without a persistent manager. `RestoreAt` reconstructs logical
state without replaying queue or member lifecycle callbacks.

Each member can select `NPCModeController` modes and fire animation triggers or UnityEvents
for entering, waiting, service, and leaving. Direct transform movement after a failed
NavMesh request is explicitly opt-in; leave it disabled when actors must stay on a baked
NavMesh and handle `MovementFailed` instead.

### Doors along a queue route

Add `NPCDoorOpener` beside a queue member when its route crosses animated doors. Mark each
door that NPCs may use with `NPCPathDoor`, assign its binary `InteractableUnlock`, and
optionally assign the door's `Interactable` so the same enabled state and flag requirements
govern NPC access. The marker is explicit: unrelated animated interactions are never opened.

Set the path door's **Clearance Delay** long enough for both its opening animation and any
carving `NavMeshObstacle` to update. Queue movement stops during that interval, rebuilds its
path afterward, and reports `MovementFailed` for a locked door. NPC opening is idempotent,
so two actors requesting the same open door cannot toggle it closed.

## Small ambient crowds

`NPCCrowdController` is a thin scene-local coordinator for a handful of background actors.
Assign one `NPCWaypointRouteBehaviour` per member and configure the initial delay, stagger,
and optional random stagger jitter. The controller pauses assigned routes before starting
them in order, and its Start/Stop/Restart methods are safe UnityEvent targets.

Crowd members are noninteractive by composition: omit `Interactable` and
`NPCDialogueBehaviour`. The crowd controller does not spawn characters, move agents itself,
or reference persistent managers. Use separate routes, randomized starting points, or
waypoint destination jitter when members should not bunch at identical destinations.
