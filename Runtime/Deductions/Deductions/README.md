# Deductions

The reusable API lives in `QuietStatic.Toolkit.Deductions`; it has no dependency on a
specific game's characters, evidence, endings, or scene names.

Use the existing `DialogueTree` for questions and choices. Let each choice set one
answer flag. A scene-level `DeductionCategoryController` observes those flags and clears
other answers in the same category through `FlagManager`.

Create prioritized `DeductionResultDefinition` assets and assign them to a
`DeductionEvaluator`. Each definition contains one or more existing `FlagRequirement`
rules; all rules must pass. Wire the deduction `DialogueRunner`'s **On Dialogue Ended**
event to `DeductionEvaluator.Evaluate`, then wire **On Result Evaluated** to
`DeductionResultPresenter.Present` and any game-specific ending handler.

Keep story flags, dialogue trees, result assets, and scene transitions in the game
project. The toolkit module owns only category exclusivity, result matching, and UI
binding.

`DeductionCategoryController` is optional. Use it when one answer flag per category must
remain selected. `DeductionEvaluator.FindResult` can also be called directly for projects
that provide their own flow or UI. Higher priority wins, and array order breaks ties.
