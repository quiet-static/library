namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Represents an activity that a cinematic sequence can start and await.
    /// </summary>
    /// <remarks>
    /// Project-specific dialogue, animation, or presentation systems can
    /// implement this interface without becoming dependencies of the toolkit.
    /// </remarks>
    public interface ICinematicWaitSource
    {
        /// <summary>
        /// Gets whether the activity is still running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Starts the activity.
        /// </summary>
        void Play();
    }
}
