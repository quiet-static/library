# Workspace and menu contract

## Approved editor entry points

The primary `Tools > Quiet Static` entries are

- **Workspace** — content catalogs, visual graphs, Problems, narrative transfer,
  scene-scoped content editing, and operational explorers;
- **Validate Project** — opens the Workspace Problems tab;
- **Project Setup** — scene-flow setup and optional maintained generators;
- **Debug Dashboard** — creates the runtime debugging surface;
- **Asset Utilities > Materials** — selection-oriented material operations;
- **Samples > Import and Open Executable Example** — imports and configures the runnable sample;
- **Cinematics > Cinematic Trees** — opens the tree authoring workspace;
- **Importers > Import Dialogue JSON...** — imports a dialogue source file.

In the Project window, right-click an asset and use **Quiet Static > Import JSON**,
**Export JSON**, or **Open Graph**. Each command is enabled for supported selections.
Use **Assets > Create** for ScriptableObjects. In the Hierarchy, use **Quiet Static >
Choose Setup…** for guided recipes, **Attach Setup** for direct application, or
**Inspect Scene Content** to scope the Workspace to a GameObject and its children.
Add new content authoring workflows to the Workspace when they fit an existing view.

## Content tab extension

Workspace tabs implement `IContentWorkspaceTab` in an editor-only assembly and carry
`[ContentWorkspaceTab]`. The stable `Id` persists selection and filters, `DisplayName`
labels the tab, and `Order` establishes deterministic placement. Implementations must:

1. have a public parameterless constructor;
2. keep runtime databases independent rather than merging schemas;
3. use `SerializedObject`, Undo, dirty tracking, and explicit saves for mutations;
4. implement deterministic, case-insensitive search;
5. reuse canonical validation and reference services;
6. require an impact preview for stable-ID changes or destructive operations;
7. release editor resources in `OnDeselected`.

Duplicate or blank tab IDs fail discovery. Unmarked types—including test fixtures—are
never registered implicitly. Discovered tabs outside the built-in **Scene**, **Story**,
**Game Logic**, and **Problems** navigation appear under **More**.

## Graph ownership

Dialogue and Scene Flow graph positions are stored in
`ProjectSettings/QuietStaticGraphLayouts.asset`, keyed by asset GUID and stable node ID.
Layout never changes runtime ordering or behavior. Graph mutations resolve through stable
IDs and rewrite index-backed runtime links atomically.

Generated dialogue assets are read-only because JSON is authoritative. Their source path
is shown in the Dialogue Workspace; use **Editable Copy** to create a detached local tree.
The copy clears generated ownership metadata while preserving dialogue semantics.
