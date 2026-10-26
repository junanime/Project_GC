using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public sealed class FestivalRecord
    {
        public string runId, name, department, character, utc, rules = FestivalDemo.Rules;
        public int score;
        public float seconds;
        public bool success;
    }

    // A shared festival PC must retain separate participants, not overwrite one anonymous account.
    // Reuses RunScore kill accounting; never submits festival scores to the ordinary online board.
    public static class FestivalLeaderboard
    {
        [Serializable] sealed class Ledger { public int version = 1; public List<FestivalRecord> rows = new List<FestivalRecord>(); }
#if UNITY_EDITOR
        public static string TestFilePath;
#endif
        public static string FilePath =>
#if UNITY_EDITOR
            TestFilePath ??
#endif
            Path.Combine(Application.persistentDataPath, "Festival", "leaderboard-v1.json");
        public static int Score(RunScoreData data) => data == null ? 0 : data.Total + Mathf.FloorToInt(Mathf.Clamp(data.seconds,0,FestivalDemo.Settings.duration)*10) + (data.success ? 9000 : 0);
        public static List<FestivalRecord> Read(string path = null)
        {
            path = path ?? FilePath;
            if (!File.Exists(path) && !File.Exists(path+".bak")) return new List<FestivalRecord>();
            foreach (var candidate in new[]{path,path+".bak"})
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var ledger = JsonUtility.FromJson<Ledger>(File.ReadAllText(candidate));
                    if (ledger == null || ledger.version != 1 || ledger.rows == null) continue;
                    return Order(ledger.rows.Where(r => r != null && !string.IsNullOrEmpty(r.runId))).ToList();
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { }
            }
            throw new IOException("행사 기록 파일을 읽을 수 없습니다. 기존 기록을 보존했습니다.");
        }
        static IOrderedEnumerable<FestivalRecord> Order(IEnumerable<FestivalRecord> rows) => rows
            .OrderByDescending(r => r.success).ThenByDescending(r => r.score).ThenByDescending(r => r.seconds).ThenBy(r => r.utc, StringComparer.Ordinal);
        public static string Clean(string text) => new string((text ?? "").Trim().Where(c => !char.IsControl(c)).ToArray());
        public static bool Save(FestivalRecord record, out string error, string path = null)
        {
            error = null;
            if (record == null || string.IsNullOrEmpty(record.runId)) { error = "플레이 기록이 없습니다."; return false; }
            record.name = Clean(record.name); record.department = Clean(record.department);
            if (record.name.Length < 1 || record.name.Length > 24 || record.department.Length < 1 || record.department.Length > 40)
            { error = "이름(1~24자)과 학과(1~40자)를 입력해 주세요."; return false; }
            path = path ?? FilePath;
            try
            {
                var rows = Read(path);
                if (rows.Any(r => r.runId == record.runId)) return true;
                rows.Add(record);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".pending";
                File.WriteAllText(temp, JsonUtility.ToJson(new Ledger { rows = Order(rows).ToList() },true));
                if (File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { error = "기록을 저장하지 못했습니다. 입력값을 유지했으니 다시 등록해 주세요."; Debug.LogWarning("[Festival] Score save failed: " + e.GetType().Name); return false; }
        }
    }
}
