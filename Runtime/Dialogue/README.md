# Dialogue

`DialogueTree` stores branching content, `DialogueRunner` advances through it, and
`DialogueEventPlayer` provides UnityEvent-friendly playback entry points.

```text
NPC
├── DialogueRunner
└── Interactable
    └── On Succeeded → DialogueEventPlayer.Play

UI
└── Dialogue View
```

Create a tree through **Assets > Create > Quiet Static Toolkit > Dialogue**. Configure
node text, choices, next indexes, and optional flags. Presentation should subscribe to
runner events instead of being embedded in the tree or runner.

Dialogue JSON authoring is also supported. Select a version 1 dialogue JSON file and
choose **Tools > Narrative > Import Selected Dialogue JSON**. Generated trees are
written to `Assets/Generated/Dialogue` and updated in place on re-import. The JSON is
the source of truth; stable node IDs are resolved to the runtime's existing indexes.

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
