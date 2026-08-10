using System;
using System.Collections.Generic;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>Serializable snapshot written to a save-game slot.</summary>
    [Serializable]
    public sealed class SaveGameData
    {
        /// <summary>Current save schema version.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Schema version used to validate and migrate this save.</summary>
        public int version = CurrentVersion;

        /// <summary>ISO-8601 UTC timestamp recorded when the save was written.</summary>
        public string savedAtUtc;

        /// <summary>Name of the active scene when the save was captured.</summary>
        public string activeScene;

        /// <summary>Optional spawn ID used to place the player after scene restoration.</summary>
        public string arrivalSpawnId;

        /// <summary>Flag IDs active when the save was captured.</summary>
        public List<string> activeFlags = new List<string>();

        /// <summary>Opaque payloads captured from registered save participants.</summary>
        public List<SaveParticipantData> participants = new List<SaveParticipantData>();
    }

    /// <summary>Serialized state owned by one registered save participant.</summary>
    [Serializable]
    public sealed class SaveParticipantData
    {
        /// <summary>Stable ID of the participant that owns this payload.</summary>
        public string id;

        /// <summary>Participant-owned JSON payload.</summary>
        public string json;

        public SaveParticipantData(string id, string json)
        {
            this.id = id;
            this.json = json;
        }
    }

    /// <summary>Lightweight information suitable for displaying a save slot in UI.</summary>
    public sealed class SaveSlotMetadata
    {
        public SaveSlotMetadata(
            int slot,
            int version,
            string savedAtUtc,
            string activeScene,
            string arrivalSpawnId,
            bool recoveredFromBackup)
        {
            Slot = slot;
            Version = version;
            SavedAtUtc = savedAtUtc;
            ActiveScene = activeScene;
            ArrivalSpawnId = arrivalSpawnId;
            RecoveredFromBackup = recoveredFromBackup;
        }

        /// <summary>Gets the zero-based slot index.</summary>
        public int Slot { get; }

        /// <summary>Gets the save schema version.</summary>
        public int Version { get; }

        /// <summary>Gets the stored ISO-8601 UTC timestamp.</summary>
        public string SavedAtUtc { get; }

        /// <summary>Gets the scene recorded by the save.</summary>
        public string ActiveScene { get; }

        /// <summary>Gets the optional arrival spawn ID.</summary>
        public string ArrivalSpawnId { get; }

        /// <summary>Gets whether metadata was recovered from the backup file.</summary>
        public bool RecoveredFromBackup { get; }

        /// <summary>Attempts to parse the stored UTC timestamp.</summary>
        public bool TryGetSavedAt(out DateTime savedAt)
        {
            return DateTime.TryParse(
                SavedAtUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out savedAt);
        }
    }
}
