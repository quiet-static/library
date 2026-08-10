# Input-sequence minigames

Start with `InputSequenceMinigameRunner.prefab` when a persistent runner and basic
screen-space sequence view are needed. Assign the project's request channel and optional
cancel action after placing it in the persistent UI or Systems scene.

`InputSequenceDefinition` assets contain reusable ordered Input System actions.
`InputSequenceMinigame` owns one active session, displays it through an optional
`InputSequenceView`, and raises separate completion, failure, and cancellation events.

Use a dedicated Input Action map for minigame buttons. The runner can block the
registered gameplay input group while enabling only the actions needed by the sequence,
so actions taken from the gameplay map may be disabled before the minigame can read them.

## Persistent runner and content scenes

1. Create an **Input Sequence Request Channel** asset.
2. Put one `InputSequenceMinigame` in the persistent UI or Systems scene and assign
   the channel, view, and an optional cancel action.
3. Add `InputSequenceMinigameActivator` to a content object. Assign the same channel
   and the sequence it requests.
4. Connect the activator's result events to focused handlers such as `FlagHandler`,
   `ObjectiveHandler`, or `ObjectStateHandler`.
5. Invoke `Activate` from an interaction UnityEvent, or reference the activator from
   `InputSequenceMinigameTrigger` for collider-driven activation.

The activator tracks whether its request was accepted, so only the object that started
the session receives its result. A one-shot trigger is consumed only after successful
startup.

Keep the view's display root on a child UI object. Hiding a display root that also owns
the runner would disable the runner itself.
