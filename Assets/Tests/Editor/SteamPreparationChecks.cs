using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class SteamPreparationChecks
    {
        static int checks;
        const string Account = "76561198000000001";
        static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Debug.Log("[SteamPreparationChecks] PASS " + message); }
        static void Throws<T>(Action action, string message) where T : Exception
        { try { action(); } catch (T) { Check(true, message); return; } throw new Exception(message); }
        public static void Run()
        {
            int result = 1;
            string legacyKey = "LobbyUnlock.SyncTest." + Guid.NewGuid().ToString("N");
            try
            {
                string directory = Path.Combine(Environment.GetEnvironmentVariable("PROJECT_GC_STEAM_TEST_OUTPUT") ?? Path.GetTempPath(), "progress-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var store = new ProgressFileStore(directory, Account);
                Check(!store.Exists && !File.Exists(store.FilePath), "fresh account has no writes before its import decision");
                Check(store.GetInt("LobbySilverCoins", 7) == 7, "missing key uses default");
                store.SetInt("LobbySilverCoins", 123); store.SetInt("LobbyUnlock.Character.아시", 1);
                store.SetString("EquippedRelicId", "산들날개"); store.Save();
                var reloaded = new ProgressFileStore(directory, Account);
                Check(reloaded.GetInt("LobbySilverCoins") == 123 && reloaded.GetString("EquippedRelicId") == "산들날개", "UTF-8 progress survives restart");
                Check(reloaded.GetInt("LobbyUnlock.Character.아시") == 1, "character unlock survives restart");
                Check(reloaded.GetString("LobbySilverCoins", "fallback") == "fallback", "typed values cannot be misread");
                Throws<ArgumentException>(() => store.SetString("Unity.Services.Authentication.sessionToken", "secret"), "auth credentials cannot enter cloud save");
                Throws<ArgumentException>(() => store.SetString(GamePreferences.SaveKey, "settings"), "machine settings cannot enter cloud save");
                Throws<ArgumentException>(() => store.SetString("Leaderboard.qa_s1_v1.pending", "score"), "pending ranking cannot enter cloud save");
                Throws<ArgumentException>(() => store.SetString("EquippedRelicId", new string('a', 513)), "oversized strings rejected");
                store.SetInt("LobbySilverCoins", 77); store.Save();
                string previous = Path.Combine(directory, "progress.previous.json");
                Check(File.Exists(previous) && File.ReadAllText(previous).Contains("123"), "second save retains last good version");
                File.WriteAllText(store.FilePath, "{truncated");
                reloaded = new ProgressFileStore(directory, Account);
                Check(reloaded.Recovered && reloaded.GetInt("LobbySilverCoins") == 123, "truncated primary recovers from backup");
                reloaded.SetInt("LobbySilverCoins", 88); reloaded.Save();
                Check(new ProgressFileStore(directory, Account).GetInt("LobbySilverCoins") == 88, "recovered profile can save again");
                Check(Directory.GetFiles(directory, "recovery-*.bak").Length == 1, "corrupt original preserved outside cloud file pattern");
                Check(File.ReadAllText(previous).Contains("123"), "recovery does not replace healthy backup with corrupt data");
                byte[] original = File.ReadAllBytes(store.FilePath);
                Throws<InvalidDataException>(() => new ProgressFileStore(directory, "76561198000000002"), "another Steam account cannot read this profile");
                Check(original.SequenceEqual(File.ReadAllBytes(store.FilePath)), "rejected account never overwrites original");
                File.WriteAllText(store.FilePath, File.ReadAllText(store.FilePath).Replace("\"schema\": 1", "\"schema\": 2"));
                Throws<NotSupportedException>(() => new ProgressFileStore(directory, Account), "future schema blocks downgrade despite older backup");
                File.WriteAllBytes(store.FilePath, original);
                File.WriteAllText(Path.Combine(directory, "progress.tmp"), "interrupted write");
                Check(new ProgressFileStore(directory, Account).GetInt("LobbySilverCoins") == 88, "leftover temporary write never replaces completed save");
                reloaded = new ProgressFileStore(directory, Account); reloaded.DeleteKey("EquippedRelicId"); reloaded.Save();
                Check(!new ProgressFileStore(directory, Account).HasKey("EquippedRelicId"), "deletions survive restart");
                string badDir = Path.Combine(directory, "invalid"); Directory.CreateDirectory(badDir);
                File.WriteAllText(Path.Combine(badDir, "progress.json"), "{}");
                Throws<InvalidDataException>(() => new ProgressFileStore(badDir, Account), "missing schema/owner cannot reset a save");
                File.WriteAllText(Path.Combine(badDir, "progress.json"), new string('x', ProgressFileStore.MaxBytes + 1));
                Throws<InvalidDataException>(() => new ProgressFileStore(badDir, Account), "oversized cloud file rejected");
                Throws<ArgumentException>(() => new ProgressFileStore(badDir, "../escape"), "invalid account path rejected");
                var full = new ProgressFileStore(Path.Combine(directory, "limits"), Account);
                full.SetInt("Coins", 7); full.Save();
                for (int i = 0; i < ProgressFileStore.MaxEntries; i++) full.SetInt("LobbyUnlock." + i, 1);
                Throws<InvalidDataException>(full.Save, "entry count limit enforced before writing");
                Check(new ProgressFileStore(Path.Combine(directory, "limits"), Account).GetInt("Coins") == 7, "rejected oversized save preserves prior progress");
                Check(!SteamRuntime.IsSteamBuild, "editor never starts Steam or migrates existing saves");
                PlayerPrefs.SetInt(legacyKey, 4);
                Check(ProgressPrefs.GetInt(legacyKey) == 4, "editor and web retain existing PlayerPrefs behavior");
                ProgressPrefs.SetInt(legacyKey, 5); ProgressPrefs.Save();
                Check(PlayerPrefs.GetInt(legacyKey) == 5, "non-Steam writes remain backward compatible");
                Check(ProgressPrefs.Error == null && !ProgressPrefs.NeedsImportChoice, "non-Steam startup has no Steam gate");
                Throws<InvalidOperationException>(() => SteamRuntime.GetTicketAsync().GetAwaiter().GetResult(), "non-Steam runtime cannot issue Steam auth tickets");
                var catalog = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
                Check(catalog != null && catalog.characters.Length >= 4 && catalog.relics.Length > 0, "migration catalog contains characters and relics");
                Check(Resources.Load<TextAsset>("TutorialClips/catalog") != null, "migration tutorial manifest available");
                var candidates = (System.Collections.Generic.IEnumerable<string>)typeof(ProgressPrefs).GetMethod("LegacyKeys", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                Check(candidates.All(ProgressFileStore.AllowedKey), "every migrated key is permitted progression data");
                Check(!File.Exists("Assets/Resources/SteamReleaseSettings.json"), "no placeholder App ID embedded in project");
                Check(!EditorSteamworksNETSettings.Instance.CanManageDefineSymbols, "SDK cannot trigger a delayed domain reload during play or build");
                Debug.Log("[SteamPreparationChecks] COMPLETE checks=" + checks + " artifacts=" + directory);
                result = 0;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { PlayerPrefs.DeleteKey(legacyKey); PlayerPrefs.Save(); }
            EditorApplication.Exit(result);
        }
    }
}
