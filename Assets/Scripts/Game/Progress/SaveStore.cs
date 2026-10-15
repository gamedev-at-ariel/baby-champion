using System;
using System.Collections.Generic;
using System.IO;
using BabyChampion.Simulation;
using UnityEngine;

namespace BabyChampion.Game
{
    /// <summary>Where save files live: text by key.</summary>
    public interface ISaveStorage
    {
        /// <summary>The stored text, or null when there is none.</summary>
        string Read(string key);
        void Write(string key, string text);
        void Delete(string key);
    }

    /// <summary>JSON files in a folder (Application.persistentDataPath on Android and in the Editor).</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        readonly string _folder;

        public FileSaveStorage(string folder) => _folder = folder;

        string PathOf(string key) => Path.Combine(_folder, key + ".json");

        public string Read(string key) => File.Exists(PathOf(key)) ? File.ReadAllText(PathOf(key)) : null;

        public void Write(string key, string text)
        {
            Directory.CreateDirectory(_folder);
            // Write then move, so a crash mid-write never leaves half a save.
            string temp = PathOf(key) + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(PathOf(key))) File.Delete(PathOf(key));
            File.Move(temp, PathOf(key));
        }

        public void Delete(string key)
        {
            if (File.Exists(PathOf(key))) File.Delete(PathOf(key));
        }
    }

    /// <summary>
    /// PlayerPrefs, for Web: the browser keeps them in IndexedDB, and PlayerPrefs.Save() flushes them
    /// right away (files under persistentDataPath would need an explicit sync).
    /// </summary>
    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        const string Prefix = "BabyChampion.";

        public string Read(string key) => PlayerPrefs.HasKey(Prefix + key) ? PlayerPrefs.GetString(Prefix + key) : null;

        public void Write(string key, string text)
        {
            PlayerPrefs.SetString(Prefix + key, text);
            PlayerPrefs.Save();
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>In memory only (tests).</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

        public string Read(string key) => _texts.TryGetValue(key, out var text) ? text : null;
        public void Write(string key, string text) => _texts[key] = text;
        public void Delete(string key) => _texts.Remove(key);
    }

    /// <summary>The 3 save slots (design Section 19.6): one baby per slot.</summary>
    public static class SaveStore
    {
        public const int SlotCount = 3;

        static ISaveStorage _storage;

        /// <summary>The storage in use; tests replace it with a <see cref="MemorySaveStorage"/>.</summary>
        public static ISaveStorage Storage
        {
            get => _storage ?? (_storage = Application.platform == RuntimePlatform.WebGLPlayer
                ? (ISaveStorage)new PlayerPrefsSaveStorage()
                : new FileSaveStorage(Path.Combine(Application.persistentDataPath, "saves")));
            set => _storage = value;
        }

        static string Key(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Save slots are 0-{SlotCount - 1}");
            return $"slot{slot + 1}";
        }

        /// <summary>The slot's save, or null when it is empty or unreadable.</summary>
        public static SaveSlot Load(int slot)
        {
            string json = Storage.Read(Key(slot));
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return SaveSlot.FromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] slot {slot + 1} is unreadable and is shown as empty: {e.Message}");
                return null;
            }
        }

        public static void Save(int slot, SaveSlot save)
        {
            Storage.Write(Key(slot), save.ToJson());
            Debug.Log($"[Save] slot {slot + 1}: {save.babyName}, {save.TotalStars} stars, money {save.household.money}");
        }

        public static void Delete(int slot) => Storage.Delete(Key(slot));
    }
}
