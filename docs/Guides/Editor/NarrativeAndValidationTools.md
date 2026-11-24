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
- cutscene steps with only one side of a camera/pose pair assigned;
- missing or duplicate active command-channel receivers;
- unreadable or duplicate persistent readable overlays;
- invalid, ambiguous, or duplicate transition-fade paths;
- direct references to toolkit managers in another loaded scene;
- stale Authoring/Generated narrative synchronization, package-reference integrity,
  machine-local package paths, and development-only build scenes.

Select an issue to ping its asset or scene component. Closed scenes are not opened
automatically, so component validation covers currently loaded scenes only. Flag
reference detection is based on serialized property names containing `flag`; dynamic
code-generated IDs cannot be discovered.

## Content Workspace

Open **Tools > Quiet Static > Workspace**. The navigation groups views by task.

| Menu | Views |
| --- | --- |
| Scene | Scene Content and Scene Flow |
| Story | Dialogue, Dialogue Resolvers, Readables, Cinematics, and Story Sequences |
| Game Logic | Flags, Objectives, and Game States |
| Problems | Project and loaded-scene validation results |

Scene Content and Dialogue Resolvers work on components in loaded scenes. The Workspace
remembers each view's search. In asset views, **Create Asset** adds a supported asset
type. The selected asset's **Actions** menu locates, finds references to, duplicates,
or deletes it.

Flags, objective definitions, game-state entries, and cinematic definitions expose a
**Safe ID Rename** foldout. Rename scans serialized assets, prefabs, and scenes, then shows
an impact preview before applying an Undo-supported source and consumer update. Stale
previews cannot be applied. The **Problems** tab combines narrative, synchronization,
open-scene, package-reference, package-path, and build-configuration rules and links
results back to their owning Unity objects. Batch/release validation additionally loads
and evaluates every enabled build scene rather than only the scenes currently open.

The Flags tab searches IDs and descriptions, scans exact serialized flag usage inline,
and exports the selected catalog. Deletion never silently rewrites string references.

Fields marked with `[FlagId]` use a searchable selector in the Inspector. The selector
stores the exact string ID, supports clearing, retains legacy/missing values until the
author explicitly changes them, and displays descriptions or missing-ID warnings.
The flag selector merges all imported flag databases and shows each catalog name and
value kind. IDs must be globally unique. Assign every runtime catalog explicitly to
the persistent FlagManager; editor discovery does not add runtime catalogs automatically.
See the [typed flags guide](../../../Runtime/Flags/README.md) for values and comparisons.

## Scene Content

Open **Scene > Scene Content** when the editing question begins with “what belongs to
this scene or this object?” Choose a loaded scene, then optionally assign a GameObject
scope or click **Use Selection**. A scope includes the selected object and every child,
which makes it useful for a room, NPC, prop group, or encounter root. You can also
right-click the root in the Hierarchy and choose **Quiet Static > Inspect Scene Content**.

The tab exposes three focused groups: flavor interactions, `DialogueEventPlayer`
references, and other `Interactable` targets. Flavor prompts and text can be edited
inline. Dialogue rows expose their tree, speaker, and focus reference and can open the
tree in the Dialogue Workspace. Other targets expose their player-facing prompt. The
shared Workspace search filters by object name and hierarchy path after the scene and
GameObject scope are applied.

This is a scene-object view rather than an asset ownership database. It only scans
loaded scenes, does not infer runtime-created references, and does not claim that an
asset referenced by a scene is exclusive to that scene.

## Cutscene Explorer and Cinematic Launch Routes

In **Workspace > Scene Tools > Cutscene Explorer**, choose all loaded scenes, a
scene, or the Hierarchy selection. Search by scene, object, step, shot, character,
Animator state, or trigger. Select a sequence to reorder and edit its steps, check
missing references, and play the sequence or a single step in Play Mode. Use
**Dialogue Cues** to edit the camera and character actions attached to named
dialogue nodes.

Open **Cinematic Launch Routes** from Scene Tools or the Cutscene Explorer. Choose
a loaded **Cinematic Scene Player** and a definition. Each beat shows the scene
bindings it uses, including missing or duplicate IDs. Choose **Edit Steps** beside
a bound sequence to open it in the explorer. Use **Edit Scene Bindings** to edit
the existing player. If you use scene launchers, open the destination scene to
verify the location, channel, and cinematic ID together.

## NPC Animation & Behaviors

Open **Workspace > Scene Tools > NPC Animation & Behaviors**. Choose all loaded
scenes, one scene, or a Hierarchy subtree. In Prefab Mode, the view scopes to the
prefab being edited. Expand an NPC to inspect its Animator, startup and movement
behaviors, dialogue settings, and dialogue reactions that reference it. A reaction
can live on a separate trigger volume, as Sam's does in House.

Select the NPCs and choose one field in **Batch Edit**. Use **Preview Changes** to
show current and proposed values and see which targets are compatible. **Apply
Compatible Changes** applies the valid edits together with Undo. Animator names
are checked against each target's controller. Startup modes are matched by name
on each NPC. Locomotion settings skip motors with existing animation callbacks
to the same Animator. Expand the component on a row for individual edits.

## Clutter Builder

Open **Tools > Quiet Static > Workspace > Scene Tools > Clutter Builder**.

1. In **Build Prefab**, drop Hierarchy props or Project prefabs and models into the
   queue. You can also select objects and choose **Queue Selection**. **Create
   Assembly** groups scene props at their existing world poses and lays out assets
   as linked instances. Arrange the children with Unity's transform tools. Choose
   **Save as Prefab** to write a new asset under `Assets` and keep the staging group
   available for editing.
2. In **Batch Interactions**, add the assembly root for a group inspection or add
   individual props from the scene or an editable project prefab. Choose **Inspect
   (Flavor Text)** or **Pick Up**, edit the rows, and **Preview Setup** before applying.
   Flavor text uses the selected Interaction UI Channel when a prop needs one.
   **Fill Blank Fields** applies shared text to rows with empty fields. Existing
   gameplay targets and conflicting hierarchies appear as skipped. Scene changes
   share one Undo action. Project prefab edits save to the asset and affect its
   instances. Use a project-owned prefab or variant for package and model sources.
3. In **Place and Snap**, choose the prefab and **Start Placement**, then hover over
   a surface in the Scene view. Use **Q** or **E** to rotate, click to place, or press
   **Escape** to cancel. The group stays upright by default. Turn on **Align to
   Surface** to follow a slope. Assign **Mesh Surface** to use rendered geometry
   such as a mattress when the collider does not represent the intended support.
   The preview flags uneven or missing support at sampled points and preserves the
   layout of the group. **Snap Hierarchy Selection** seats existing objects using
   the same placement rules.

## Clutter Interactables

Choose **Scene Tools > Clutter Interactables** in the Workspace to open the clutter authoring tool. It
indexes `FlavorTextInteraction` components separately from story and gameplay
interactables. Select scene objects and choose **Set Up Selected Objects as Clutter
Interactables** to add or repair the standard interaction, highlight, trigger-collider,
layer, UI-channel, and flavor-text setup.

Use clutter interactions for optional one-message observations. They intentionally do
not own dialogue nodes, flags, objectives, or story state. Existing non-flavor targets
are detection results only unless the author explicitly chooses **Convert**.

## Dialogue catalog

Open **Story > Dialogue** in the Content Workspace or the **Browse** tab in the Dialogue
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

Edit node **Presentation Tags** in the Dialogue catalog or Graph tab. The tags are
stored on the node and travel with it through Dialogue JSON import and export.

## Objective catalog

Open **Game Logic > Objectives** in the Content Workspace, or select an `ObjectiveDatabase`
asset and click **Open in Content Workspace** in its Inspector.

The explorer searches objective asset names, stable IDs, titles, and descriptions. Each
database row edits its referenced `ObjectiveDefinition` asset directly, including its
flag-based completion requirement. Summary and inline messages identify missing definitions,
empty or duplicate IDs, and objectives without player-facing text.

Use **Create Asset** to create databases or definition assets. Database membership remains
an ordinary serialized list in the Inspector. **Actions > Find Asset References** scans direct serialized
dependencies and displays matching assets inline; runtime-created references cannot be
detected.

Use **Export JSON** to migrate the selected database, its ordered definitions, and both
activation and completion requirements to the narrative authorer. Original Unity asset
paths are recorded so importing the edited JSON updates the same assets and preserves GUIDs.

## Narrative authoring migration

Export selected flags, objectives, readables, and dialogue trees using the matching
**Assets > Quiet Static > Export JSON** commands. The Dialogue Workspace and contextual
Flags/Objectives tab actions also expose **Export JSON**. Dialogue export preserves both its linear fallback and
conditional choices; missing legacy node IDs receive deterministic authoring IDs without
changing the source asset.

Choose **Narrative JSON > Export Project Snapshot…** in the Content Workspace
to gather the project's narrative assets into one folder. Existing JSON is preserved by
default to protect authorer-side edits. The public bulk API and command-line entry point can
explicitly overwrite existing files when refreshing from Unity is intentional.

Migration metadata may target only canonical `.asset` paths below `Assets`. Import preflight
rejects traversal, wrong existing asset types, duplicate targets, and cross-document target
collisions before changing assets. JSON without metadata continues to import below the
normal generated folders. Batch import is available through **Narrative JSON > Preview Batch Import…** in the Content
Workspace. The command opens a read-only preview grouped by
source document. Review every asset that will be created, updated in place, regenerated, or
deleted, then explicitly confirm the import. Regenerated and deleted objective definitions
are highlighted because their GUIDs can change. Confirmation runs preflight again; if a
source or affected Unity asset changed while the window was open, the preview refreshes and
must be reviewed again.

## Interactable Explorer

Choose **Scene Tools > Interaction Explorer** in the Content Workspace.

The explorer indexes one-shot, hold, and autonomous-progress interaction targets without
creating a runtime database or duplicating scene state. Choose loaded scenes, project
prefabs, or both; search by object, hierarchy, prompt, requirement flags, completion flags,
or conditional message text. Results show their location and any completion flags.
Edit the prompt, progress label, and flag requirements inline. Use **Add Success Message**
or **Add Failure Message** for constant channel-backed text, or the conditional buttons
for ordered flag-driven messages. Choose an existing **Interaction UI Channel** with a
receiver configured in the UI scene. Conditional rules use the first matching non-empty
message, then the fallback. Removing a message callback preserves the other callbacks.

**Issues only** finds targets without a collider or without the project's usual
`InteractionHighlighter`. Select a result to ping the real scene or prefab object, which
remains the source of truth.

## Dialogue Workspace

Open **Story > Dialogue** in the Content Workspace, or select a
`DialogueTree` and use **Assets > Quiet Static > Open Graph > Dialogue**.

Use **Add Node**, the node picker, **Set as Entry Node**, and **Save Tree** in the catalog
for inline authoring. Choose **Open Graph** for visual layout, edge editing, duplication,
delete-impact previews, and export. In either editor you can edit the speaker, line,
presentation tags, and flags, and add or remove choices with named destinations.
Choose **End Dialogue** to finish the conversation, and use **Next Node / Fallback**
for the path taken when no choice is available. In the graph you can also connect outputs
to a node's **In** port. **Path Preview** accepts simulated flags and typed values and
shows which choices are available without modifying runtime state. Generated JSON trees
are read-only. Use **Editable Copy** to create a detached local asset.

The Browse tab replaces the former standalone Dialogue Browser. It searches all dialogue
assets by text, speaker, and flags, filters validation issues, and reports references without
adding another nested tool window.

## Scene setup validation

Open **Tools > Quiet Static > Validate Project**, then select **Problems**.

The scan checks missing scripts, duplicate Quiet Static manager types, AudioListener
count, EventSystem presence, game-state database duplicates and references, required
command receivers, readable/fade capability, cross-scene manager references, package
serialization and paths, and enabled build-scene GUIDs that no longer resolve.
An absent EventSystem is informational because persistent UI may be loaded additively.

Fields marked with `[GameStateId]` use the same searchable, string-preserving
Inspector workflow as flag IDs. Unlike flag catalogs, the game-state selector uses the first database by deterministic
asset GUID when more than one exists; projects should keep one authoritative
`GameStateDatabase`.

## Existing Play Mode debugging

The toolkit does not add another story-state source of truth. The consuming project
can use its existing Debug Dashboard to inspect and mutate `FlagManager` state and
view the chronological narrative trace.

## Build separation

These tools are in `QuietStatic.Core.Editor`, restricted to the Editor platform.
Runtime components can use `FlagIdAttribute` on serialized fields. Its property drawer
and the flag database asset postprocessor live in the editor assembly.
