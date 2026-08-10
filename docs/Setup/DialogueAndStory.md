# Dialogue, Conditional Choices, and Story Sequences

## Dialogue

1. Create a `DialogueTree` asset or import supported dialogue JSON.
2. Add nodes with stable authoring IDs, speaker, line, and either a linear next index
   or choices.
3. For a conditional choice, configure its `FlagRequirement`. `All` and `Any` reveal
   choices when flags exist; `NotAll` and `NotAny` support choices that disappear.
4. Put `DialogueManager` and `DialogueUIManager` in the systems/UI scenes.
5. Start dialogue from a `DialogueEventPlayer`, NPC behavior, or interaction event.

## Story progression

1. Create a `StorySequenceDefinition`.
2. Assign a stable sequence ID and starting stage ID.
3. Add stages with unique IDs, entry/completion requirements, optional objectives,
   flags, scene connection IDs, and next-stage links.
4. Add `StorySequenceRunner` to a persistent narrative object and assign the asset.
5. Use stage events for local presentation; keep progression data in the definition.

Use the narrative browser and validation tools to catch duplicate IDs, broken links,
and invalid starting stages before Play Mode.
