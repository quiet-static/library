# Flags

Flags are stable string IDs representing progression. `FlagDatabase` documents allowed
IDs, `FlagManager` owns active state, `FlagRequirement` evaluates conditions, and the
drawer/attribute provide database-backed Inspector selection.

```text
GameplayManagers
└── FlagManager
    ├── Database: FlagsDB
    ├── Starting Flags
    └── Dependencies

Door
└── Interactable
    └── Requirement: HasKey
```

Add IDs to the database before using them. Set flags through `FlagHandler`, `FlagSetter`,
dialogue nodes, or interactions. Do not use flags for rapidly changing values such as
health or movement state.

## Narrative authorer handoff

Open **Tools > Quiet Static > Flags > Flag Database** to create or edit a database.
Use **Export JSON** in that window, or select a `FlagDatabase` asset and choose
**Tools > Quiet Static > Flags > Export Selected Flag Database JSON...**, to create a
version-one flag catalog for the narrative authorer. Empty IDs, surrounding whitespace,
or duplicate IDs stop the export so the authoring JSON preserves exact runtime semantics.

The JSON can be linked from a dialogue author's Tree Settings and can later be imported
back into Unity with the narrative content or narrative-authorer batch import command.
