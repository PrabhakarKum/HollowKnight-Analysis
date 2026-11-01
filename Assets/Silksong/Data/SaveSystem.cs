using System;
using System.IO;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // One JSON file per slot. Loading never throws and never destroys a file it cannot understand:
    // a player's save is worth more than a tidy folder. Saving never overwrites a file this build
    // cannot vouch for (newer version, or unreadable right now).
    public static class SaveSystem
    {
        static string root;

        // Tests point this at a temp folder; the default is a subfolder of persistentDataPath.
        public static string Root
        {
            get => root ?? Path.Combine(Application.persistentDataPath, "Saves");
            set => root = value;
        }

        enum FileState { Missing, Valid, Corrupt, Newer, Unreadable }

        static string PathFor(int slot) => Path.Combine(Root, "user" + slot + ".json");

        public static bool TrySave(PlayerData data, int slot)
        {
            if (data == null) return false;
            string path = PathFor(slot);
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Root);

                PlayerData existing;
                FileState state = Inspect(path, out existing);
                if (state == FileState.Newer || state == FileState.Unreadable)
                {
                    // A "defaults" session must not destroy a save it could not understand.
                    Debug.LogWarning($"[Save] Slot {slot} holds a {state} file; refusing to overwrite it.");
                    return false;
                }
                if (state == FileState.Corrupt) Quarantine(path);

                // Serialise a copy so stamping the version does not mutate the caller's object.
                PlayerData copy = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
                copy.version = PlayerData.CurrentVersion;

                // Write the whole new file beside the old one first, so a crash mid-write
                // leaves the previous save untouched.
                File.WriteAllText(temp, JsonUtility.ToJson(copy));
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
            PlayerData data;
            switch (Inspect(path, out data))
            {
                case FileState.Valid:
                    return data;
                case FileState.Corrupt:
                    Quarantine(path);
                    break;
                case FileState.Newer:
                    // Written by a newer build. Leave the file alone so that build can still read it.
                    Debug.LogWarning($"[Save] Slot {slot} is from a newer version; using defaults and not overwriting.");
                    break;
                case FileState.Unreadable:
                    Debug.LogWarning($"[Save] Slot {slot} could not be read; using defaults and leaving the file alone.");
                    break;
            }
            return PlayerData.CreateNew();
        }

        static FileState Inspect(string path, out PlayerData data)
        {
            data = null;
            string json;
            try
            {
                if (!File.Exists(path)) return FileState.Missing;
                json = File.ReadAllText(path);
            }
            catch (Exception)
            {
                // Could be a transient lock; the file may be perfectly good, so do not touch it.
                return FileState.Unreadable;
            }

            PlayerData parsed = null;
            try { parsed = JsonUtility.FromJson<PlayerData>(json); }
            catch (Exception) { /* treated as corrupt below */ }

            if (parsed == null) return FileState.Corrupt;
            if (parsed.version > PlayerData.CurrentVersion) return FileState.Newer;
            if (!IsValid(parsed)) return FileState.Corrupt;

            data = parsed;
            return FileState.Valid;
        }

        // "{}" or a truncated-but-parseable object would otherwise load as health 0; reject those.
        static bool IsValid(PlayerData d)
        {
            return d.version >= 1
                && d.maxHealth > 0
                && d.health >= 0 && d.health <= d.maxHealth
                && d.silk >= 0 && d.silk <= d.maxSilk
                && !string.IsNullOrEmpty(d.respawnScene);
        }

        // Keeps the unreadable bytes for recovery under a unique name; never deletes an earlier backup.
        static void Quarantine(string path)
        {
            try
            {
                string target = path + ".corrupt";
                for (int n = 1; File.Exists(target); n++)
                    target = path + "." + n + ".corrupt";
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
