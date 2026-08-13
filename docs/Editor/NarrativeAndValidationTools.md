# Narrative and Validation Tools

These editor-only tools inspect the existing Quiet Static runtime data. They do not
introduce new runtime managers, rewrite flag IDs, reorder dialogue nodes, or modify
content during validation.

## Narrative validation

Open **Tools > Quiet Static > Validation > Validate Narrative** and click **Scan**.

The scan checks:

- empty and duplicate flag identifiers;
- missing flag references in serialized assets, prefabs, and currently open scenes;
- empty dialogue text and choices;
- invalid dialogue start/transition indexes and unreachable nodes;
- empty objective text and requirements with no flags;
- cutscene steps with only one side of a camera/pose pair assigned.

Select an issue to ping its asset or scene component. Closed scenes are not opened
automatically, so component validation covers currently loaded scenes only. Flag
reference detection is based on serialized property names containing `flag`; dynamic
code-generated IDs cannot be discovered.

## Flag Database editor

Open **Tools > Quiet Static > Flags > Flag Database**.

Choose a database, search by ID or description, then edit entries with normal Unity
Undo support. **Refresh Usage** counts exact serialized references. **Find** opens the
matching assets and open-scene components. Deletion always asks for confirmation and
never rewrites references.

Fields marked with `[FlagId]` use a searchable selector in the Inspector. The selector
stores the exact string ID, supports clearing, retains legacy/missing values until the
author explicitly changes them, and displays descriptions or missing-ID warnings.
When multiple databases exist, the selector deterministically uses the first database
by asset GUID; projects should normally configure one authoritative database.

## Dialogue Browser

Open **Tools > Quiet Static > Dialogue > Dialogue Browser**.

The browser provides a read-only, project-wide view of every `DialogueTree`. Search
matches asset names, dialogue text, speaker names, choice text, and flags. Separate
speaker and flag fields narrow results, while **Issues only** shows assets and nodes
that need attention.

Each node reports empty dialogue or choice text, missing speakers, broken transition
indexes, unreachable nodes, null entries, and unknown flags. Missing speakers are
warnings because narrator-only nodes can be intentional. The references section uses
Unity's Asset Database dependency information to list definite references from scenes,
prefabs, and ScriptableObjects without opening closed scenes.

The current data model has no portrait, tag, or event fields, so the browser does not
invent parallel metadata or claim to validate those concepts.

## Objective Database explorer

Open **Tools > Quiet Static > Objectives > Objective Database**, or select an `ObjectiveDatabase` asset
and click **Open Objective Database Explorer** in its Inspector.

The explorer searches objective asset names, stable IDs, titles, and descriptions. Each
database row edits its referenced `ObjectiveDefinition` asset directly, including its
flag-based completion requirement. Summary and inline messages identify missing definitions,
empty or duplicate IDs, and objectives without player-facing text.

Use **Create Objective** to create a separate definition asset and append it to the selected
database. **Add Existing** appends an existing definition without duplicating it. **Remove**
only removes the database reference and never deletes the definition asset. **Refresh
References** scans direct serialized dependencies in project assets and the **Refs** button
opens the matching paths; runtime-created references cannot be detected.

## Interactable Explorer

Open **Tools > Quiet Static > Interactions > Interactable Explorer**.

The explorer indexes one-shot, hold, and autonomous-progress interaction targets without
creating a runtime database or duplicating scene state. Choose loaded scenes, project
prefabs, or both; search by object, hierarchy, prompt, requirement flags, completion flags,
or conditional message text. Each result shows its location, UI text, requirement summary,
flags it sets, and ordered `ConditionalInteractionMessage` rules.

**Issues only** finds targets without a collider or without the project's usual
`InteractionHighlighter`. Select a result to ping the real scene or prefab object, which
remains the source of truth.

## Dialogue Graph

Open **Tools > Quiet Static > Dialogue > Dialogue Graph**, or select a `DialogueTree` and use
**Assets > Open in Dialogue Graph**.

The initial graph is deliberately read-only. It loads the existing index-linked
runtime model, highlights the entry node, colors unreachable nodes, displays broken
transitions, supports node selection, and pans by dragging the background. Existing
stable node IDs are shown when present; older assets receive an in-memory
`legacy-index-N` label and are not migrated or reserialized.

Node positions currently use deterministic automatic layout and are not saved.
Before graph editing is enabled, layout will be stored as editor-only metadata keyed
by the dialogue asset GUID and stable node ID. A separate preview-and-apply migration
will be required for legacy nodes without IDs. Layout metadata will never control
runtime ordering or transitions.

## Scene setup validation

Open **Tools > Quiet Static > Validation > Validate Open Scenes**.

The scan checks missing scripts, duplicate Quiet Static manager types, AudioListener
count, EventSystem presence, game-state database duplicates and references, and
enabled build-scene GUIDs that no longer resolve.
An absent EventSystem is informational because persistent UI may be loaded additively.

Fields marked with `[GameStateId]` use the same searchable, string-preserving
Inspector workflow as flag IDs. The selector uses the first database by deterministic
asset GUID when more than one exists; projects should keep one authoritative
`GameStateDatabase`.

## Existing Play Mode debugging

The toolkit does not add another story-state source of truth. The consuming project
can use its existing Debug Dashboard to inspect and mutate `FlagManager` state and
view the chronological narrative trace.

## Build separation

All tools are in `QuietStatic.Core.Editor`, restricted to the Editor platform. The
runtime assembly contains only the lightweight `FlagIdAttribute`; its property drawer
and asset postprocessor live in the editor assembly and are excluded from players.
