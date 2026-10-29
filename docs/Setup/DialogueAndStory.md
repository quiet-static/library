# Dialogue, Conditional Choices, and Story Sequences

## Dialogue

1. Create a `DialogueTree` asset or import supported dialogue JSON.
2. Add nodes with stable authoring IDs, speaker, line, and either a linear next index
   or choices.
3. For a conditional choice, configure its `FlagRequirement`. `All` and `Any` reveal
   choices when flags exist; `NotAll` and `NotAny` support choices that disappear.
4. Put `DialogueManager` and `DialogueUIManager` in the systems/UI scenes.
5. Start dialogue from a `DialogueEventPlayer`, NPC behavior, or interaction event.

## Importing one authored dialogue

Save the JSON in the Narrative Authorer, then open **Tools > Quiet Static >
Workspace** in Unity and click **Import Dialogue JSON**. Select the saved JSON
directly, including a file in `Authoring/Narrative` outside `Assets`. You can also
use **Tools > Quiet Static > Importers > Import Dialogue JSON...**. No batch
manifest or export step is required for this single-dialogue operation.

The importer uses `unityAssetPath` from the JSON when present. Otherwise it writes
`Assets/Generated/Dialogue/<treeId>.asset`. Reimporting to the same target updates
the asset in place and preserves scene references. For dialogues intended to live
elsewhere, set `unityAssetPath` in the authorer's tree settings before importing;
keep that path stable on future exports. Assign a new tree to the NPC/resolver once.

This imports the dialogue only. If you add flags, import the updated flags catalog
too (select its exported JSON and use **Assets > Quiet Static > Import Narrative
Content JSON**), or use the author's **Export to Unity** and **Import Batch** to
update linked content together. Existing exported dialogue JSON inside `Assets`
can also be selected and imported using **Assets > Quiet Static > Import Dialogue
JSON**. Authoring source remains the place to edit generated dialogue text.

## Selecting NPC conversations by story state

To find and edit existing scene selectors together, open **Tools > Quiet Static >
Workspace > Dialogue Resolvers**. Choose one loaded scene or all loaded scenes;
inactive objects are included. The workspace search matches hierarchy paths, rule
labels, and assigned tree names. Expand each resolver to edit its ordered rules,
flag conditions, and fallback directly, or use **Select** to locate its GameObject.
Edits support Undo and prefab overrides; save the affected scenes afterward.

Add `DialogueTreeResolver` to the NPC and assign it to the optional **Dialogue
Resolver** field on `NPCDialogueBehaviour`. Keep the interaction event wired to
`NPCDialogueBehaviour.StartDialogue` so movement, facing, focus, and lifecycle
events continue to work. With no resolver assigned, the fixed Dialogue Tree works
as before.

The resolver checks its rules from top to bottom on each interaction. Each rule
has a label, tree, required condition, and blocking condition. The first eligible
tree wins. A required condition of `None` allows the rule; a blocking condition
of `None` does not block it. Put specific conversations before general ones.
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

## Story progression

1. Create a `StorySequenceDefinition`.
2. Assign a stable sequence ID and starting stage ID.
3. Add stages with unique IDs, entry/completion requirements, optional objectives,
   flags, scene connection IDs, and next-stage links.
4. Add `StorySequenceRunner` to a persistent narrative object and assign the asset.
5. Use stage events for local presentation; keep progression data in the definition.

Use the narrative browser and validation tools to catch duplicate IDs, broken links,
and invalid starting stages before Play Mode.
