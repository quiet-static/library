using UnityEngine;

namespace QuietStatic.Toolkit.Pause
{
    /// <summary>Operations accepted by the persistent pause service.</summary>
    public enum PauseCommandType
    {
        Toggle,
        Pause,
        Resume,
        ForceResume
    }

    /// <summary>Typed request for changing the global paused state.</summary>
    public readonly struct PauseCommand
    {
        /// <summary>Creates a pause command.</summary>
        public PauseCommand(PauseCommandType type) => Type = type;

        /// <summary>Requested pause operation.</summary>
        public PauseCommandType Type { get; }
    }

    /// <summary>Relays pause requests across scene lifetime boundaries.</summary>
    [CreateAssetMenu(
        fileName = "PauseRequestChannel",
        menuName = "Quiet Static Toolkit/Pause/Pause Request Channel")]
    public sealed class PauseRequestChannel :
        CrossSceneCommandChannel<PauseCommand>
    {
        /// <summary>Requests a pause-state toggle.</summary>
        public void Toggle() => Dispatch(new PauseCommand(PauseCommandType.Toggle));

        /// <summary>Requests entering the paused state.</summary>
        public void Pause() => Dispatch(new PauseCommand(PauseCommandType.Pause));

        /// <summary>Requests resuming gameplay.</summary>
        public void Resume() => Dispatch(new PauseCommand(PauseCommandType.Resume));

        /// <summary>Requests fail-safe restoration of time and cursor state.</summary>
        public void ForceResume() => Dispatch(new PauseCommand(PauseCommandType.ForceResume));
    }
}
