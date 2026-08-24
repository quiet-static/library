# Workspace and menu contract

## Approved editor entry points

`Tools > Quiet Static` intentionally contains only:

- **Workspace** — content catalogs, visual graphs, Problems, narrative transfer, and
  operational explorers;
- **Validate Project** — opens the Workspace Problems tab;
- **Project Setup** — scene-flow setup and optional maintained generators;
- **Play Mode Isolation** — project-local isolated-scene Play Mode preference;
- **Debug Dashboard** — creates the runtime debugging surface;
- **Asset Utilities > Materials** — selection-oriented material operations.

Selection-specific narrative commands live under **Assets > Quiet Static**. Ordinary
ScriptableObject creation stays under Unity's **Assets > Create** menu. New database,
feature, importer, exporter, builder, or validation submenus must not be added.

The EditMode menu inventory test enforces this surface.

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
never registered implicitly.

## Graph ownership

Dialogue and Scene Flow graph positions are stored in
`ProjectSettings/QuietStaticGraphLayouts.asset`, keyed by asset GUID and stable node ID.
Layout never changes runtime ordering or behavior. Graph mutations resolve through stable
IDs and rewrite index-backed runtime links atomically.

Generated dialogue assets are read-only because JSON is authoritative. Their source path
is shown in the Dialogue Workspace; use **Editable Copy** to create a detached local tree.
The copy clears generated ownership metadata while preserving dialogue semantics.
