namespace QuietStatic.Toolkit.Saving
{
    /// <summary>
    /// Optional boundary for scene systems that own small pieces of persistent state.
    /// </summary>
    /// <remarks>
    /// Implementations are responsible for producing and consuming their own JSON payload.
    /// IDs must be stable and unique across every loaded scene.
    /// </remarks>
    public interface ISaveParticipant
    {
        /// <summary>Stable identifier stored in the save file.</summary>
        string SaveId { get; }

        /// <summary>Captures this participant's state as JSON.</summary>
        string CaptureSaveState();

        /// <summary>Restores a previously captured JSON payload.</summary>
        void RestoreSaveState(string json);
    }
}
