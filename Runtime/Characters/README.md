# Characters

Character code is divided into `Player` locomotion and modular `NPC` behavior. Shared
helpers such as `VelocityReporter` stay at this level.

Use the provided player prefabs as a starting point. Keep the visual model/Animator on a
child object and movement/collision/input components on the root so art can be replaced
without rebuilding control logic.
