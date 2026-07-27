# Project Guidelines

This is a reusable Unity toolkit for horror and narrative games.

## Architecture preferences

- Prefer simple, modular components.
- Simplify existing systems before adding new ones.
- Managers live in a persistent Systems scene.
- Scene objects use handlers or events rather than directly referencing managers.
- Use ScriptableObjects for reusable definitions and databases.
- Runtime flags are strings, but Inspector-facing fields should use database-backed dropdowns.
- Preserve existing functionality when extending components.
- Avoid duplicating responsibilities across scripts.

## Coding style

- Use clear XML documentation and Inspector tooltips.
- Keep classes focused on one responsibility.
- Prefer configurable Inspector fields.
- Do not introduce unnecessary abstractions.
- Explain significant architectural changes before implementing them.
