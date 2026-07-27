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
