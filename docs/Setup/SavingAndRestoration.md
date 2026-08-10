# Saving and Restoration

1. Keep stable IDs for scenes, spawn targets, game states, flags, objectives, and story
   stages. Save data relies on identity rather than object references.
2. Put the save owner in the persistent systems scene.
3. Register or expose each system's save participant through the existing save
   interfaces/channels rather than making content objects find managers.
4. Restore persistent state before starting story evaluation or presenting objectives.
5. Restore the destination scene and spawn target before returning control to the
   player.

Test a round trip containing flags, current game state, objectives, narrative stage,
scene/spawn identity, and user settings. Before changing a shipped schema, add an
explicit version and migration path; never silently reinterpret an existing stable ID.
