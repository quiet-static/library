# Dialogue, Conditional Choices, and Story Sequences

## Dialogue

1. Create a `DialogueTree` asset or import supported dialogue JSON.
2. Add nodes with stable authoring IDs, speaker, line, and either a linear next index
   or choices.
3. For a conditional choice, configure its `FlagRequirement`. `All` and `Any` reveal
   choices when flags exist; `NotAll` and `NotAny` support choices that disappear.
4. Put `DialogueManager` and `DialogueUIManager` in the systems/UI scenes.
5. Start dialogue from a `DialogueEventPlayer`, NPC behavior, or interaction event.

Use **Tools > Quiet Static > Workspace**, then **Story > Dialogue** for inline node editing or
**Open Graph** for visual connections and Path Preview. Generated trees can be updated
through JSON or detached with **Editable Copy**. See the
[Dialogue Workspace guide](../Editor/NarrativeAndValidationTools.md#dialogue-workspace).

## Importing one authored dialogue

Save the JSON in the Narrative Authorer, then open **Tools > Quiet Static >
Workspace** in Unity and choose **Narrative JSON > Import Single Dialogue…**. Select the saved JSON
directly, including a file in `Authoring/Narrative` outside `Assets`. You can also
use **Tools > Quiet Static > Importers > Import Dialogue JSON...**. No batch
manifest or export step is required for this single-dialogue operation.

The importer uses `unityAssetPath` from the JSON when present. Otherwise it writes
`Assets/Generated/Dialogue/<treeId>.asset`. Reimporting to the same target updates
the asset in place and preserves scene references. For dialogues intended to live
elsewhere, set `unityAssetPath` in the authorer's tree settings before importing;
keep that path stable on future exports. Assign a new tree to the NPC/resolver once.

This imports the dialogue only. If you add flags, import the updated flags catalog
too (select its exported JSON and use **Assets > Quiet Static > Import JSON >
Flags, Objectives, or Readables**), or use the author's **Export to Unity** and
**Narrative JSON > Preview Batch Import…** to
update linked content together. Existing exported dialogue JSON inside `Assets`
can also be selected and imported using **Assets > Quiet Static > Import JSON >
Dialogue**. Authoring source remains the place to edit generated dialogue text.

## Selecting NPC conversations by story state

To find and edit existing scene selectors together, open **Tools > Quiet Static >
Workspace**, then choose **Story > Dialogue Resolvers**. Choose one loaded scene or all loaded scenes;
inactive objects are included. The workspace search matches hierarchy paths, rule
labels, and assigned tree names. Expand each resolver to edit its ordered rules,
flag conditions, and fallback directly, or use **Select** to locate its GameObject.
Edits support Undo and prefab overrides; save the affected scenes afterward.

Add `DialogueTreeResolver` to the NPC and assign it to the optional **Dialogue
Resolver** field on `NPCDialogueBehaviour`. With no resolver assigned, the NPC uses
its fixed **Dialogue Tree**.

For a dialogue-only NPC, assign its `NPCDialogueBehaviour` to **Dialogue Action** on
the `Interactable`. The interaction prompt uses the same availability check as the
dialogue start, and the interaction succeeds when the dialogue player accepts the
request. Remove any `NPCDialogueBehaviour.StartDialogue` call from the interaction's
success event when setting up **Dialogue Action** on an existing NPC. Scenes can
also continue to call `StartDialogue` through their existing UnityEvents.

The resolver checks its rules from top to bottom on each interaction. Each rule
has a label, tree, required flags, blocking flags, and optional live conditions.
The first eligible tree wins. Flag mode `None` skips the Boolean flag check.
Typed comparisons still apply, including in blocking requirements. Put specific
conversations before general ones.
Null rules and missing trees are skipped. When no rule matches, the resolver uses
its optional fallback; an empty fallback means no dialogue starts (the NPC's fixed
tree is not used when a resolver is assigned).

For an optional Sam conversation after the missing-phone discussion:

- Set Requirement to `All`: `dialogue.house.sam-phone`.
- Set Blocking Requirement to `Any`: `story.house.departed` and a new,
  database-registered completion flag for this conversation.
- Assign the intended dialogue tree. Choose a later prerequisite instead if the
  conversation should open after checking the car or obtaining protection.

Selection never sets flags. Author the completion flag on the dialogue's final
node or final response, after any meaningful content the player must see. Do not
set it on interaction success: the dialogue manager may reject a start. The
existing dialogue-ended event also fires when stopped, so it does not by itself
prove the conversation was completed. Rules use the normal saved story flags and
do not need a separate conversation-history store.

### Live player conditions

Create an asset with **Assets > Create > Quiet Static Toolkit > Dialogue >
Conditions > Player Holding Object**. Set whether the player must be carrying
an object or must be empty-handed. Add the asset to a rule's **Context Conditions**
list. A rule can combine saved flags and live conditions, and every listed live
condition must pass. For example, put a reminder to set an object down above a
general greeting. The resolver selects a tree when the interaction starts, and
later changes can affect the next conversation.

For other game-specific facts, create a `DialogueContextCondition` asset type and
implement `IsMet(GameObject player, Interactor interactor)` by reading the player's
gameplay components. Keep the check side-effect-free so it can also be used to
show interaction prompts. Return false when the required component is unavailable.
With **Dialogue Action**, the initiating `Interactor` is passed even when it sits
on a separate camera rig. Scene-local UnityEvent calls use the active player
registered with `PlayerManager` and pass no `Interactor`.

Expand **Rule Preview** in **Story > Dialogue Resolvers** to try combinations of
flags, typed values, and live condition results. The preview shows the selected
tree or fallback and explains why earlier rules were skipped. Simulation leaves
the scene, dialogue assets, and saved state untouched.

## Collider-triggered dialogue

1. Create a scene object for the volume, separate from any manual interaction.
   In the Hierarchy, choose **Quiet Static > Attach Setup > Scene Objects >
   Dialogue Trigger**. Size its trigger collider to fit the intended area. Use a
   player with a Character Controller or a Rigidbody so the physics trigger can fire.
2. Assign the **Dialogue Tree** on its `DialogueEventPlayer`. To select a different
   tree from flags or the entering player, assign a **Dialogue Resolver** and put the
   fallback on that resolver. Use **Start Requirement** for a flag gate shared by
   every tree. **Trigger Once** consumes the volume after a conversation starts.
   If the start is blocked, the volume can try again on the next entry.
3. For an NPC response, add **Dialogue NPC Reaction** to the volume and assign the
   NPC. Turn on **Look At Player** if the NPC has `NPCLookAtBehaviour`. The NPC looks
   toward the entering player during dialogue and releases that look when it ends.
4. To animate the NPC, enter Animator trigger names for **Animation On Start** or
   **Animation On End**. The NPC uses its existing `NPCAnimationTrigger`. For movement,
   enter an NPC mode name in **Mode On Start** or **Mode On End**. A mode that
   enables a managed waypoint-route behavior can send the NPC to a door after the
   conversation. If the end mode is blank, the prior mode resumes when the start
   mode is still active and there was a prior mode.

Use the dialogue player's events for scene cues such as audio. Options on the end
event and reaction also run when a conversation is stopped. For a custom sensor,
call `TryStartDefaultDialogue(actor)` to use the same tree selection and reactions.

Stolen's House uses this setup on `dialogues/sam_final`. The volume starts the
missing-phone conversation, Sam looks at Piper during the exchange, and his
reaction fires `idle` when it ends. The dialogue player's events handle ambient
audio and the story flag.

## Story progression

1. Create a `StorySequenceDefinition`.
2. Assign a stable sequence ID and starting stage ID.
3. Add stages with unique IDs, entry/completion requirements, optional objectives,
   flags, scene connection IDs, and next-stage links.
4. Add `StorySequenceRunner` to a persistent narrative object and assign the asset.
5. Use stage events for local presentation; keep progression data in the definition.

Use the narrative browser and validation tools to catch duplicate IDs, broken links,
and invalid starting stages before Play Mode.
