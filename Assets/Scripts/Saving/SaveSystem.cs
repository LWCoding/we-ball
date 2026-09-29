using System;
using System.IO;
using UnityEngine;

namespace WeBall.Saving
{
    /// <summary>
    /// Everything stored in a save slot. Add world progress here as the game grows.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int slot;
        public string createdUtc;
        public string lastPlayedUtc;
    }

    /// <summary>
    /// Reads and writes save slots as JSON files in the host's persistent data folder.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        public static bool Exists(int slot) => File.Exists(PathFor(slot));

        public static SaveData Load(int slot) =>
            Exists(slot) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot))) : null;

        public static void Save(SaveData data) =>
            File.WriteAllText(PathFor(data.slot), JsonUtility.ToJson(data, true));

        /// <summary>Loads the slot, or starts a new save in it if it's empty.</summary>
        public static SaveData LoadOrCreate(int slot)
        {
            string now = DateTime.UtcNow.ToString("o");
            SaveData data = Load(slot) ?? new SaveData { slot = slot, createdUtc = now };
            data.lastPlayedUtc = now;
            Save(data);
            return data;
        }

        public static string Describe(int slot)
        {
            SaveData data = Load(slot);
            if (data == null) return $"Slot {slot + 1} - New Game";

            DateTime lastPlayed = DateTime.Parse(data.lastPlayedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
            return $"Slot {slot + 1} - Last played {lastPlayed.ToLocalTime():g}";
        }

        static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");
    }

    /// <summary>
    /// The save the host is currently playing. Only set on the host.
    /// </summary>
    public static class GameSession
    {
        public static SaveData CurrentSave { get; private set; }

        public static void Begin(int slot) => CurrentSave = SaveSystem.LoadOrCreate(slot);
    }
}
