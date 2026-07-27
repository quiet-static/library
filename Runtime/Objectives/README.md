# Objectives

`ObjectiveResolver` selects the current objective from ordered flag-based rules.
`ObjectiveVisibilityController` shows or hides objective UI in response to state.

```text
UI
└── Objective Panel
    ├── ObjectiveResolver
    ├── ObjectiveVisibilityController
    └── Objective Text
```

Order rules from most specific/latest to fallback. Assign the same flag database used by
`FlagManager`, then listen to the resolver or `ToolkitEvents.ObjectiveChanged` to update UI.
