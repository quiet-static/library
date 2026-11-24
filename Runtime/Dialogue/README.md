# Dialogue

`DialogueTree` stores branching content, `DialogueRunner` owns traversal state,
`DialogueManager` coordinates the persistent runner with UI, and
`DialogueEventPlayer` provides scene-local UnityEvent entry points.

```text
Systems
|-- DialogueManager
|   `-- DialogueRunner
`-- DialogueUIManager

Scene object
`-- DialogueEventPlayer
```

Create a tree through **Assets > Create > Quiet Static Toolkit > Dialogue**. Configure
node text, choices, next indexes, and optional flags. Presentation should subscribe to
runner events instead of being embedded in the tree or runner.

Dialogue JSON authoring is also supported. Select a version 1 dialogue JSON file and
choose **Assets > Quiet Static > Import Dialogue JSON**. Generated trees are
written to `Assets/Generated/Dialogue` by default or to the JSON's `unityAssetPath`
when supplied. Reimport updates the asset at that path in place. The importer resolves
stable node IDs to runtime indexes.

Choices may include an optional flag-backed availability requirement. Inspector-authored
choices use `FlagRequirement`; JSON choices use a readable condition object:

```json
{
  "text": "Ask about the letter",
  "next": "letter",
  "condition": { "mode": "All", "flags": ["FoundLetter"] },
  "flagsToSet": ["AskedAboutLetter"]
}
```

Supported modes are `None`, `All`, `Any`, `NotAll`, and `NotAny`. The UI displays only
available choices and maps visible buttons back to authored choice indexes. If no authored
choice is available, the node behaves as a linear node and uses its normal next-node path.

## Presentation during a visual interlude

Use `DialogueManager.SuspendPresentation(owner)` to hide the dialogue UI and defer node
presentation. `NodeChanged` still fires, so scene cues can respond to the active node.
Call `ResumePresentation(owner)` with the same owner when the interlude finishes.
After the final owner releases presentation, the manager presents the current node if
its session is still active. Manage input separately with `SuspendInput(owner)` and
`ResumeInput(owner)` if the interlude also needs to block dialogue input.
