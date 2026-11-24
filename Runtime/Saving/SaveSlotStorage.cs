using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>Reads, validates, migrates, and writes the files for a save slot.</summary>
    internal sealed class SaveSlotStorage
    {
        private readonly string directory;
        private readonly string filePrefix;

        internal SaveSlotStorage(string directory, string filePrefix)
        {
            this.directory = directory;
            this.filePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "save_slot_" : filePrefix.Trim();
        }

        internal void Write(int slot, SaveGameData data)
        {
            string path = GetSlotPath(slot);
            string json = JsonUtility.ToJson(data, true);
            ReplaceAtomically(path, path + ".bak", temporary => File.WriteAllText(temporary, json));
        }

        internal void Delete(int slot)
        {
            string path = GetSlotPath(slot);
            string backupPath = path + ".bak";
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }

        internal bool TryRead(int slot, Func<SaveGameData, string> validateCheckpoint,
            out SaveGameData data, out string error, out bool recoveredFromBackup, out string warning)
        {
            data = null;
            recoveredFromBackup = false;
            warning = null;

            string path = GetSlotPath(slot);
            string backupPath = path + ".bak";
            if (!File.Exists(path) && !File.Exists(backupPath))
            {
                error = $"Save slot {slot} does not exist.";
                return false;
            }

            if (TryDeserializeFile(path, validateCheckpoint, out data, out string primaryError))
            {
                error = string.Empty;
                return true;
            }

            if (TryDeserializeFile(backupPath, validateCheckpoint, out data, out string backupError))
            {
                recoveredFromBackup = true;
                error = string.Empty;
                try
                {
                    // Repair the primary without replacing the known-good backup.
                    ReplaceAtomically(path, null, temporary => File.Copy(backupPath, temporary));
                }
                catch (Exception exception)
                {
                    warning = $"Loaded save slot {slot} from backup but could not repair the primary file: {exception.Message}";
                }
                return true;
            }

            error = $"Primary save could not be read ({primaryError}). Backup could not be read ({backupError}).";
            return false;
        }

        private static bool TryDeserializeFile(string path, Func<SaveGameData, string> validateCheckpoint,
            out SaveGameData data, out string error)
        {
            data = null;
            if (!File.Exists(path))
            {
                error = "file does not exist";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<SaveGameData>(json);
                if (data == null || string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                {
                    error = "file contains no readable save data";
                    return false;
                }
                if (data.version <= 0)
                {
                    error = $"save version {data.version} is invalid";
                    return false;
                }
                if (data.version > SaveGameData.CurrentVersion)
                {
                    error = $"save uses unsupported version {data.version}";
                    return false;
                }

                // Version one stores only Boolean flags. Typed flags start from catalog defaults.
                if (data.version == 1)
                {
                    data.flagValues = new();
                    data.version = 2;
                }
                data.flagValues ??= new();
                var flagValueIds = new HashSet<string>(StringComparer.Ordinal);
                if (data.flagValues.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.flagId) ||
                    !entry.value.IsValid || !flagValueIds.Add(entry.flagId.Trim())))
                {
                    error = "Saved flag values are invalid or duplicated.";
                    return false;
                }
                data.activeFlags ??= new List<string>();
                data.participants ??= new List<SaveParticipantData>();
                var participantIds = new HashSet<string>(StringComparer.Ordinal);
                if (data.participants.Any(participant => participant == null || string.IsNullOrWhiteSpace(participant.id) ||
                    !participantIds.Add(participant.id.Trim())))
                {
                    error = "Save participant IDs are missing or duplicated.";
                    return false;
                }
                error = validateCheckpoint(data) ?? string.Empty;
                return error.Length == 0;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private string GetSlotPath(int slot) => Path.Combine(directory, $"{filePrefix}{slot}.json");

        private static void ReplaceAtomically(string path, string backupPath, Action<string> stage)
        {
            string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                stage(temporaryPath);
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, backupPath);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
