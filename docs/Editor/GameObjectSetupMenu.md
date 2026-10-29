# Hierarchy component setups

Select one or more GameObjects in the Hierarchy, right-click, and choose
**Quiet Static > Attach Setup**. The same commands are available from the main
**GameObject > Quiet Static > Attach Setup** menu. Commands attach to existing
objects; they do not create replacement objects or persistent managers.

| Category | Setup | Components and wiring |
| --- | --- | --- |
| Interactions | Basic | Interactable, highlighter, and a collider when needed |
| Interactions | Flavor Text | Basic plus FlavorTextInteraction, using the existing clutter setup |
| Interactions | Conditional Message | Basic plus ConditionalInteractionMessage, wired to success |
| Interactions | Dialogue | Basic plus DialogueEventPlayer, wired to success |
| Interactions | Readable | Basic plus ReadableInteractionTrigger |
| Interactions | Animated | Basic plus Animator and InteractableUnlock, wired to success |
| Interactions | Audio | Basic plus AudioSource.Play on success; new sources use spatial audio and do not play on awake |
| Interactions | Hold | HoldInteractable, highlighter, and collider |
| Interactions | Timed Progress | ActivatedProgressInteractable, highlighter, and collider |
| NPCs | Stationary | NPCController and NPCLookAtBehaviour |
| NPCs | Dialogue with Resolver | Stationary NPC plus basic interaction, NPCDialogueBehaviour, DialogueEventPlayer, and resolver |
| NPCs | Follow / Wander / Waypoint Route | Controller, look behaviour, NavMeshAgent, motor, selected movement behaviour, and animator driver when an Animator exists |
| Scene Objects | Scene Exit Trigger | SceneTransitionTrigger and a trigger collider |

Setups reuse existing components, fill empty local references, and append missing
event callbacks without duplicating them. Existing prompts, assigned references,
events, and colliders are preserved. New interaction components use the
`Interactables` layer when it exists. Missing colliders are fitted to child
renderer bounds, or use Unity's default box size on empty objects. Review collider
bounds before playtesting.

One menu action supports Undo across the entire selection. Save the scene to keep
the changes. Prefab instances and objects in Prefab Mode are supported; project
prefab assets must be opened in Prefab Mode first. Commands are unavailable in Play
Mode or without a selected scene object.

Assign the actual dialogue trees/resolver rules, flavor text, message rules,
readable content, audio clip, animation controller/triggers, and progress bar as
appropriate. New dialogue NPCs use an empty resolver; an existing fixed NPC
conversation is kept instead of being hidden by an empty resolver. If movement
is also present, dialogue pauses its movement behaviours and resumes them afterward.
The look behaviour needs a controller target at runtime; follow NPCs also need
that target. Moving NPCs need a baked NavMesh; waypoint NPCs need a route.

Readable/message and scene-exit setup fills shared channel/map references only when
one matching asset exists. If there are zero or multiple candidates, choose the
correct one in the Inspector. The flavor setup follows the existing Clutter tool's
channel-selection behaviour. Scene exits also require a connection ID. The toolkit's
persistent player, UI, dialogue, and scene-flow services must already be configured.

Different interaction types are not stacked on the same object. A second movement
behaviour is skipped when another movement type exists; configure explicit NPC
modes if a character needs several movement roles.
