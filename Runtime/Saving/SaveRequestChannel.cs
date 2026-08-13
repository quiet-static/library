using UnityEngine;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>Operation carried by a <see cref="SaveCommand"/>.</summary>
    public enum SaveCommandType
    {
        Save,
        Load,
        Delete
    }

    /// <summary>Typed cross-scene save-slot command.</summary>
    public readonly struct SaveCommand
    {
        /// <summary>Creates a save-slot command.</summary>
        public SaveCommand(
            SaveCommandType type,
            int slot,
            string arrivalSpawnId = "")
        {
            Type = type;
            Slot = slot;
            ArrivalSpawnId = arrivalSpawnId ?? string.Empty;
        }

        /// <summary>Requested save-slot operation.</summary>
        public SaveCommandType Type { get; }

        /// <summary>Zero-based slot supplied by the caller.</summary>
        public int Slot { get; }

        /// <summary>Optional spawn used after restoring this save.</summary>
        public string ArrivalSpawnId { get; }
    }

    /// <summary>
    /// Relays save-slot requests from scene content to the persistent save manager.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SaveRequestChannel",
        menuName = "Quiet Static Toolkit/Saving/Save Request Channel")]
    public sealed class SaveRequestChannel :
        CrossSceneCommandChannel<SaveCommand>
    {
        /// <summary>Requests a save in the supplied slot.</summary>
        public void RequestSave(int slot, string arrivalSpawnId = "")
        {
            Dispatch(new SaveCommand(
                SaveCommandType.Save,
                slot,
                arrivalSpawnId));
        }

        /// <summary>Requests that the supplied slot be loaded.</summary>
        public void RequestLoad(int slot)
        {
            Dispatch(new SaveCommand(SaveCommandType.Load, slot));
        }

        /// <summary>Requests deletion of the supplied slot.</summary>
        public void RequestDelete(int slot)
        {
            Dispatch(new SaveCommand(SaveCommandType.Delete, slot));
        }
    }
}
