# Quiet Static Toolkit Guides

Open [index.html](../index.html) locally in a browser. The **Code** tab indexes
standalone module pages, **Assets** catalogs bundled Unity assets and samples, and
**Guides** contains the rendered setup and authoring documentation.

To refresh the offline reference, run `pwsh -File docs/Reference/BuildReference.ps1`
from the package root with PowerShell 7. When a sibling `stolen` Git checkout is
available, the builder uses call sites from its unchanged, tracked C# files and links to the
corresponding lines at the checked-out Git revision. Otherwise, it builds examples from
the package sources, samples, and tests. Regenerate the HTML after editing Markdown,
public APIs, or bundled assets.

These guides describe reusable setup patterns rather than one game's scene names,
flags, input maps, or art. Start with the persistent systems guide, then follow the
recipe for the feature being built.

## Setup

- [Making a game with the toolkit](QuietStaticGameGuide.md)
- [Persistent systems and bootstrap](Setup/PersistentSystemsAndBootstrap.md)
- [Scene transitions and spawning](Setup/SceneTransitionsAndSpawning.md)
- [Dialogue, choices, and story sequences](Setup/DialogueAndStory.md)
- [Interactions and objectives](Setup/InteractionsAndObjectives.md)
- [Horror tension and jumpscares](Setup/HorrorPresentation.md)
- [Settings, pause, and accessibility](Setup/SettingsPauseAccessibility.md)
- [Saving and restoration](Setup/SavingAndRestoration.md)

Use **Tools > Quiet Static > Samples > Import and Open Executable Example** for the
runnable `Samples~/ExecutableToolkitExample` vertical slice. The separate
`Samples~/CompositionReferences` scenes are instructional patterns that should be
adapted in a game's `Assets` folder before project-specific editing.

## Runtime guides

- [Common component recipes](Runtime/CommonComponentRecipes.md)
- [Cinematics, fades, and readables](Runtime/CinematicsAndReadablesSetup.md)
- [Cinematic trees](Runtime/CinematicTreesSetup.md)
- [Third-person movement](Runtime/Characters/ThirdPersonMotor.md)
- [Movement state](Runtime/Characters/MovementStateController.md)
- [Character animation](Runtime/Characters/AnimationController.md)
- [Movement input sources](Runtime/Interfaces/Input/IMoveInput.md)
- [Look input sources](Runtime/Interfaces/Input/ILookInput.md)

## Editor and maintenance

- [Narrative, scene-content, and validation tools](Editor/NarrativeAndValidationTools.md)
- [Hierarchy component setups](Editor/GameObjectSetupMenu.md)
- [Workspace and menu contract](Editor/WorkspaceAndMenuContract.md)
- [Batch material creation](Editor/BatchMaterialCreator.md)
- [Codebase gap audit](CodebaseGapAudit.md)
