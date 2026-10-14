# Editor tools

These scripts add Unity Editor-only workflow commands and are excluded from builds.

- Game-facing authoring, setup, sample, and validation commands live under
  `Tools > Quiet Static > Workspace` and task-oriented companion entries.
- The Workspace **Scene Content** tab filters interactions and dialogue references by
  loaded scene or GameObject subtree. Its **Clutter** toolbar action opens the dedicated
  flavor-text interaction setup and repair tool.
- `BatchExtractMaterials` extracts embedded materials from selected model assets under
  `Tools > Quiet Static > Asset Utilities > Materials`.
- `BatchMaterialCreator` creates materials in bulk for selected textures under the same
  material-utilities menu.

Select the source assets in the Project window, then use the matching Quiet Static menu
command. Run tools on a small selection first and keep generated assets beside their
source art so references remain understandable.
