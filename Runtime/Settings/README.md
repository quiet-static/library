# Settings

The settings service stores and applies reusable player preferences. The manager variant
coordinates project-wide settings and broadcasts changes.

Keep one settings owner in the persistent System scene. UI controls should call public
setter/apply methods and listen for change events rather than modifying render or input
components directly.
