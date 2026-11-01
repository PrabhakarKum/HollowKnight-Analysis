using System;
using System.IO;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // One JSON file per slot. Loading never throws and never destroys a file it cannot understand:
    // a player's save is worth more than a tidy folder.
    public static class SaveSystem
    {
        static string root;

        // Tests point this at a temp folder; the default is a subfolder of persistentDataPath.
        public static string Root
        {
            get => root ?? Path.Combine(Application.persistentDataPath, "Saves");
            set => root = value;
        }

        static string PathFor(int slot) => Path.Combine(Root, "user" + slot + ".json");

        public static bool TrySave(PlayerData data, int slot)
        {
            if (data == null) return false;
            string path = PathFor(slot);
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Root);
                data.version = PlayerData.CurrentVersion;
                // Write the whole new file beside the old one first, so a crash mid-write
                // leaves the previous save untouched.
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not save slot {slot}: {e.Message}");
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
                return false;
            }
        }

        public static PlayerData LoadOrDefault(int slot)
        {
            string path = PathFor(slot);
            try
            {
                if (!File.Exists(path)) return PlayerData.CreateNew();

                string json = File.ReadAllText(path);
                PlayerData data = null;
                try { data = JsonUtility.FromJson<PlayerData>(json); }
                catch (Exception) { /* treated as corrupt below */ }

                if (data == null)
                {
                    Quarantine(path);
                    return PlayerData.CreateNew();
                }

                if (data.version > PlayerData.CurrentVersion)
                {
                    // Written by a newer build. Leave the file alone so that build can still read it.
                    Debug.LogWarning($"[Save] Slot {slot} is from a newer version ({data.version}); using defaults and not overwriting.");
                    return PlayerData.CreateNew();
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not load slot {slot}: {e.Message}");
                return PlayerData.CreateNew();
            }
        }

        // Keeps the unreadable bytes for recovery and frees the slot name for a fresh save.
        static void Quarantine(string path)
        {
            try
            {
                string target = path + ".corrupt";
                if (File.Exists(target)) File.Delete(target);
                File.Move(path, target);
                Debug.LogWarning($"[Save] Corrupt save moved to {target}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not back up corrupt save: {e.Message}");
            }
        }
    }
}
