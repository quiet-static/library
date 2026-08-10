namespace QuietStatic.Input
{
    /// <summary>Provides the continuous state of the gameplay interaction action.</summary>
    public interface IHoldInteractInputSource
    {
        /// <summary>Gets whether the interaction action is currently held.</summary>
        bool InteractHeld { get; }
    }
}
