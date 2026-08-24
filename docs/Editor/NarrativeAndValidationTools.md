# Narrative and Validation Tools

These editor-only tools inspect the existing Quiet Static runtime data. They do not
introduce new runtime managers, rewrite flag IDs, reorder dialogue nodes, or modify
content during validation.

## Narrative validation

Open **Tools > Quiet Static > Validate Project**, then select **Problems**.

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

## Content Workspace

Open **Tools > Quiet Static > Workspace**. The Flags, Objectives,
Game States, Cinematics, Dialogue, Scene Flow, Readables, and Story Sequences tabs replace
the former separate database and reference windows. Each tab searches serialized content,
remembers its filter and selected asset, edits through the normal Unity Inspector, and
supports create, duplicate, guarded delete, dependency references, and contextual actions.

Flags, objective definitions, game-state entries, and cinematic definitions expose a
**Safe ID Rename** panel. Rename scans serialized assets, prefabs, and scenes, then shows
an impact preview before applying an Undo-supported source and consumer update. Stale
previews cannot be applied. The **Problems** tab runs the same narrative and open-scene
rules as batch validation and links results back to their owning Unity objects.

The Flags tab searches IDs and descriptions, scans exact serialized flag usage inline,
and exports the selected catalog. Deletion never silently rewrites string references.

Fields marked with `[FlagId]` use a searchable selector in the Inspector. The selector
stores the exact string ID, supports clearing, retains legacy/missing values until the
author explicitly changes them, and displays descriptions or missing-ID warnings.
When multiple databases exist, the selector deterministically uses the first database
by asset GUID; projects should normally configure one authoritative database.

## Dialogue catalog

Open the **Dialogue** tab in the Content Workspace or the **Browse** tab in the Dialogue
Workspace.

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

## Objective catalog

Open the **Objectives** tab in the Content Workspace, or select an `ObjectiveDatabase`
asset and click **Open in Content Workspace** in its Inspector.

The explorer searches objective asset names, stable IDs, titles, and descriptions. Each
database row edits its referenced `ObjectiveDefinition` asset directly, including its
flag-based completion requirement. Summary and inline messages identify missing definitions,
empty or duplicate IDs, and objectives without player-facing text.

Use **Create Asset** to create databases or definition assets. Database membership remains
an ordinary serialized list in the Inspector. **References** scans direct serialized
dependencies and displays matching assets inline; runtime-created references cannot be
detected.

Use **Export JSON** to migrate the selected database, its ordered definitions, and both
activation and completion requirements to the narrative authorer. Original Unity asset
paths are recorded so importing the edited JSON updates the same assets and preserves GUIDs.

## Narrative authoring migration

Selected flags, objectives, readables, and dialogue trees can be exported from their
the matching **Assets > Quiet Static** commands. The Dialogue Workspace and contextual
Flags/Objectives tab actions also expose **Export JSON**. Dialogue export preserves both its linear fallback and
conditional choices; missing legacy node IDs receive deterministic authoring IDs without
changing the source asset.

Choose **Export Snapshot** in the Content Workspace
to gather the project's narrative assets into one folder. Existing JSON is preserved by
default to protect authorer-side edits. The public bulk API and command-line entry point can
explicitly overwrite existing files when refreshing from Unity is intentional.

Migration metadata may target only canonical `.asset` paths below `Assets`. Import preflight
rejects traversal, wrong existing asset types, duplicate targets, and cross-document target
collisions before changing assets. JSON without metadata continues to import below the
normal generated folders. Batch import is available through **Import Batch** in the Content
Workspace. The command opens a read-only preview grouped by
source document. Review every asset that will be created, updated in place, regenerated, or
deleted, then explicitly confirm the import. Regenerated and deleted objective definitions
are highlighted because their GUIDs can change. Confirmation runs preflight again; if a
source or affected Unity asset changed while the window was open, the preview refreshes and
must be reviewed again.

## Interactable Explorer

Choose **Interactions** in the Content Workspace toolbar.

The explorer indexes one-shot, hold, and autonomous-progress interaction targets without
creating a runtime database or duplicating scene state. Choose loaded scenes, project
prefabs, or both; search by object, hierarchy, prompt, requirement flags, completion flags,
or conditional message text. Each result shows its location, UI text, requirement summary,
flags it sets, and ordered `ConditionalInteractionMessage` rules.

**Issues only** finds targets without a collider or without the project's usual
`InteractionHighlighter`. Select a result to ping the real scene or prefab object, which
remains the source of truth.

## Dialogue Workspace

Open the Dialogue catalog from **Tools > Quiet Static > Workspace**, or select a
`DialogueTree` and use **Assets > Open in Dialogue Workspace**.

The Graph tab edits node flow visually while preserving the runtime model's index links by
resolving every structural change through stable node IDs. It supports persisted editor-only
layout, edge reconnect and removal, entry selection, compact node fields, delete-impact
previews, export, save, and revert. Generated JSON trees are read-only; use **Editable Copy**
to create a detached local asset without modifying the generated source.

The Browse tab replaces the former standalone Dialogue Browser. It searches all dialogue
assets by text, speaker, and flags, filters validation issues, and reports references without
adding another nested tool window.

## Scene setup validation

Open **Tools > Quiet Static > Validate Project**, then select **Problems**.

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
