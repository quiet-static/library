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
