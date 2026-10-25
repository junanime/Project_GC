using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vampire
{
    public static class ProgressPrefs
    {
        [Serializable] sealed class TutorialCatalog { public TutorialGuide.Entry[] entries; }
        const string ImportOwnerKey = "Steam.ProgressImport.Owner.v1"; // device-only receipt
        static ProgressFileStore store;
        static bool initialized, prompt;
        static string error;
        public static string Error { get { Ensure(); return error; } }
        public static bool NeedsImportChoice { get { Ensure(); return prompt; } }
        public static bool Recovered { get { Ensure(); return store != null && store.Recovered; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { store = null; initialized = prompt = false; error = null; }
        static void Ensure()
        {
            if (!SteamRuntime.IsSteamBuild || initialized) return;
            initialized = true;
            SteamRuntime.EnsureInitialized();
            if (!SteamRuntime.Ready) { error = SteamRuntime.Error ?? "Steam을 통해 다시 실행해 주세요."; return; }
            try
            {
                store = new ProgressFileStore(Path.Combine(Application.persistentDataPath, "SteamProfiles", SteamRuntime.AccountId), SteamRuntime.AccountId);
                if (!store.Exists)
                {
                    if (!PlayerPrefs.HasKey(ImportOwnerKey)) foreach (var key in LegacyKeys()) if (PlayerPrefs.HasKey(key)) { prompt = true; break; }
                    if (!prompt) store.Save();
                }
            }
            catch (Exception e) { Fail(e); }
        }
        static void Fail(Exception e)
        {
            error = e is NotSupportedException ? "저장 기록을 읽으려면 게임을 최신 버전으로 업데이트해 주세요." : "저장 기록을 안전하게 열거나 저장하지 못했습니다.\n저장 공간과 파일 접근 권한을 확인한 뒤 다시 실행해 주세요.";
            Debug.LogError("[Progress] " + e.GetType().Name + ": " + e.Message);
        }
        static bool Writable => store != null && error == null && !prompt && SteamRuntime.Ready;
        static IEnumerable<string> LegacyKeys()
        {
            yield return "LobbySilverCoins"; yield return "Coins"; yield return "EquippedRelicId"; yield return CapsuleDoctorDialogue.SeenKey;
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            if (config != null)
            {
                foreach (var character in config.characters) if (character != null)
                { yield return "LobbyUnlock.Character." + character.name; yield return StartingNeedleSelection.SaveKey(character); }
                foreach (var relic in config.relics) if (relic != null) yield return "RelicUnlocked_" + relic.relicId;
            }
            foreach (SyringeSpecialAugmentAbility.SpecialAugmentType type in Enum.GetValues(typeof(SyringeSpecialAugmentAbility.SpecialAugmentType)))
            { yield return "LobbyUnlock.Needle." + type; yield return StartingNeedleSelection.EnabledKey(type); }
            var manifest = Resources.Load<TextAsset>("TutorialClips/catalog");
            if (manifest != null)
            {
                var catalog = JsonUtility.FromJson<TutorialCatalog>(manifest.text);
                if (catalog?.entries != null) foreach (var item in catalog.entries) yield return TutorialGuide.SeenKey(item.id);
            }
        }
        public static void ChooseLegacyImport(bool import)
        {
            Ensure(); if (!prompt || store == null || error != null) return;
            try
            {
                if (import)
                {
                    foreach (var key in LegacyKeys()) if (PlayerPrefs.HasKey(key))
                    { if (key == "EquippedRelicId") store.SetString(key, PlayerPrefs.GetString(key)); else store.SetInt(key, PlayerPrefs.GetInt(key)); }
                }
                store.Save(); // Commit first. Never consume the receipt if saving failed.
                if (import) { PlayerPrefs.SetString(ImportOwnerKey, SteamRuntime.AccountId); PlayerPrefs.Save(); }
                prompt = false;
            }
            catch (Exception e) { Fail(e); }
        }
        public static bool HasKey(string key) { if (!SteamRuntime.IsSteamBuild) return PlayerPrefs.HasKey(key); Ensure(); return store != null && store.HasKey(key); }
        public static int GetInt(string key, int fallback = 0) { if (!SteamRuntime.IsSteamBuild) return PlayerPrefs.GetInt(key, fallback); Ensure(); return store?.GetInt(key, fallback) ?? fallback; }
        public static string GetString(string key, string fallback = "") { if (!SteamRuntime.IsSteamBuild) return PlayerPrefs.GetString(key, fallback); Ensure(); return store?.GetString(key, fallback) ?? fallback; }
        public static void SetInt(string key, int value) { if (!SteamRuntime.IsSteamBuild) { PlayerPrefs.SetInt(key, value); return; } Ensure(); if (Writable) store.SetInt(key, value); }
        public static void SetString(string key, string value) { if (!SteamRuntime.IsSteamBuild) { PlayerPrefs.SetString(key, value); return; } Ensure(); if (Writable) store.SetString(key, value); }
        public static void DeleteKey(string key) { if (!SteamRuntime.IsSteamBuild) { PlayerPrefs.DeleteKey(key); return; } Ensure(); if (Writable) store.DeleteKey(key); }
        public static void Save()
        {
            if (!SteamRuntime.IsSteamBuild) { PlayerPrefs.Save(); return; }
            Ensure(); if (!Writable) return;
            try { store.Save(); } catch (Exception e) { Fail(e); }
        }
    }
}
