using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Contract used by <see cref="Interactor"/> to target both toolkit and
    /// project-owned interaction components.
    /// </summary>
    public interface IInteractionTarget
    {
        /// <summary>Player-facing name displayed while this target is focused.</summary>
        string DisplayName { get; }

        /// <summary>Transform that owns the interaction and its optional highlighter.</summary>
        Transform InteractionTransform { get; }

        /// <summary>
        /// Returns whether this target can currently be focused and attempted by
        /// the supplied interactor.
        /// </summary>
        bool IsInteractionAvailable(Interactor interactor);

        /// <summary>Attempts the interaction and returns whether it succeeded.</summary>
        bool TryInteract(Interactor interactor);
    }

    /// <summary>
    /// Optional focus callback for targets that own project-specific highlighting
    /// or prompt presentation.
    /// </summary>
    public interface IInteractionFocusReceiver
    {
        /// <summary>Notifies the target when crosshair focus changes.</summary>
        void SetInteractionFocused(bool focused);
    }
}
