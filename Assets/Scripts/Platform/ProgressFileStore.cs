using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Vampire
{
    // Only permanent progression belongs in Auto-Cloud. Authentication tokens,
    // leaderboard caches, graphics settings and unfinished runs stay on this device.
    public sealed class ProgressFileStore
    {
        [Serializable] public sealed class Entry { public string key; public int kind, number; public string text; }
        [Serializable] public sealed class Document
        {
            public int schema;
            public string owner, savedUtc;
            public long revision;
            public List<Entry> entries;
        }
        readonly Dictionary<string, Entry> values = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly string owner, file, previous;
        long revision;
        bool primaryValid;
        public bool Exists { get; private set; }
        public bool Recovered { get; private set; }
        public string FilePath => file;
        public const int MaxBytes = 262144;
        public const int MaxEntries = 2048;

        public static bool AllowedKey(string key) => !string.IsNullOrEmpty(key) && key.Length <= 200 &&
            (key == "LobbySilverCoins" || key == "Coins" || key == "EquippedRelicId" || key == CapsuleDoctorDialogue.SeenKey ||
             key.StartsWith("LobbyUnlock.", StringComparison.Ordinal) || key.StartsWith("RelicUnlocked_", StringComparison.Ordinal) ||
             key.StartsWith("October.StartingNeedle.", StringComparison.Ordinal) || key.StartsWith("October.NeedleEnabled.", StringComparison.Ordinal) ||
             key.StartsWith("tutorial.first-discovery.v1.", StringComparison.Ordinal));

        public ProgressFileStore(string directory, string account)
        {
            if (string.IsNullOrEmpty(account) || account.Length != 17 || !account.All(char.IsDigit)) throw new ArgumentException("Invalid Steam identity");
            owner = account; file = Path.Combine(directory, "progress.json"); previous = Path.Combine(directory, "progress.previous.json");
            Document data;
            if (TryRead(file, out data)) { primaryValid = true; Load(data); }
            else if (TryRead(previous, out data)) { Recovered = true; Load(data); }
            else if (File.Exists(file) || File.Exists(previous)) throw new InvalidDataException("No valid progression save; originals were preserved.");
        }
        bool TryRead(string path, out Document document)
        {
            document = null;
            if (!File.Exists(path)) return false;
            if (new FileInfo(path).Length > MaxBytes) return false;
            try { document = JsonUtility.FromJson<Document>(File.ReadAllText(path)); }
            catch (ArgumentException) { return false; }
            if (document == null) return false;
            // A newer game version must never be overwritten with an older backup.
            if (document.schema > 1) throw new NotSupportedException("This save requires a newer game version.");
            if (document.schema != 1 || document.owner != owner || document.revision < 0 || document.entries == null || document.entries.Count > MaxEntries) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document.entries)
                if (entry == null || !AllowedKey(entry.key) || !seen.Add(entry.key) || (entry.kind != 0 && entry.kind != 1) || (entry.text?.Length ?? 0) > 512) return false;
            return true;
        }
        void Load(Document document)
        {
            foreach (var entry in document.entries) values.Add(entry.key, entry);
            revision = document.revision; Exists = true;
        }
        static void RequireKey(string key) { if (!AllowedKey(key)) throw new ArgumentException("Not a cloud progression key: " + key); }
        public bool HasKey(string key) { RequireKey(key); return values.ContainsKey(key); }
        public int GetInt(string key, int fallback = 0) { RequireKey(key); return values.TryGetValue(key, out var e) && e.kind == 0 ? e.number : fallback; }
        public string GetString(string key, string fallback = "") { RequireKey(key); return values.TryGetValue(key, out var e) && e.kind == 1 ? e.text ?? "" : fallback; }
        public void SetInt(string key, int value) { RequireKey(key); values[key] = new Entry { key = key, number = value }; }
        public void SetString(string key, string value)
        {
            RequireKey(key); if ((value?.Length ?? 0) > 512) throw new ArgumentException("Progress string too long");
            values[key] = new Entry { key = key, kind = 1, text = value ?? "" };
        }
        public void DeleteKey(string key) { RequireKey(key); values.Remove(key); }
        public void Save()
        {
            if (values.Count > MaxEntries) throw new InvalidDataException("Too many progression entries");
            var data = new Document { schema = 1, owner = owner, revision = checked(revision + 1), savedUtc = DateTime.UtcNow.ToString("o"), entries = values.Values.OrderBy(x => x.key, StringComparer.Ordinal).ToList() };
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
            if (bytes.Length > MaxBytes) throw new InvalidDataException("Progress save exceeds its limit");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temporary = Path.Combine(Path.GetDirectoryName(file), "progress.tmp");
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(file))
            {
                if (!primaryValid) File.Copy(file, Path.Combine(Path.GetDirectoryName(file), "recovery-" + Guid.NewGuid().ToString("N") + ".bak"));
                // Preserve the good backup when recovering a corrupt primary.
                File.Replace(temporary, file, primaryValid ? previous : null);
            }
            else File.Move(temporary, file);
            revision = data.revision; primaryValid = Exists = true;
        }
    }
}
