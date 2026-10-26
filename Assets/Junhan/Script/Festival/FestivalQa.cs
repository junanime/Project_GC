using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Vampire
{
    // Separate from participant identity, leaderboard scores and combat statistics.
    [Serializable] public sealed class FestivalQaRun
    {
        public int schema = 1;
        public string station, run, character, utc, rules, build;
        public bool closed;
        public long frames, stalls100, stalls250;
        public double measuredSeconds, worstFrameMs;
        public double AverageFps => measuredSeconds > 0 ? frames / measuredSeconds : 0;
    }
    [Serializable] public sealed class FestivalQaError
    {
        public string kind, category, methods, context, run;
        public int count;
    }
    [Serializable] public sealed class FestivalQaErrors
    {
        public int schema = 1;
        public string station, session, utc, build;
        public long overflow;
        public List<FestivalQaError> errors = new List<FestivalQaError>();
    }
    public sealed class FestivalQaSnapshot
    {
        public readonly List<FestivalQaRun> runs = new List<FestivalQaRun>();
        public readonly List<FestivalQaErrors> sessions = new List<FestivalQaErrors>();
        public int unreadable;
        public long Frames => runs.Sum(r => r.frames);
        public double Seconds => runs.Sum(r => r.measuredSeconds);
        public double AverageFps => Seconds > 0 ? Frames / Seconds : 0;
        public long Errors => sessions.Sum(s => s.overflow + s.errors.Sum(e => (long)e.count));
    }
    // Monotonic wall time; paused/loading/focus transitions reset the frame boundary.
    public sealed class FestivalQaFrameMeter
    {
        readonly FestivalQaRun data;
        double previous;
        bool primed;
        public FestivalQaFrameMeter(FestivalQaRun data) { this.data = data; }
        public void ResetBoundary() { primed = false; }
        public void Observe(double now, bool active)
        {
            if (!active || double.IsNaN(now) || double.IsInfinity(now)) { primed = false; return; }
            if (!primed) { previous = now; primed = true; return; }
            double seconds = now - previous; previous = now;
            if (seconds <= 0) return;
            data.frames++; data.measuredSeconds += seconds;
            data.worstFrameMs = Math.Max(data.worstFrameMs, seconds * 1000);
            if (seconds >= .1 - 1e-9) data.stalls100++;
            if (seconds >= .25 - 1e-9) data.stalls250++;
        }
    }
    public static class FestivalQaStore
    {
        public static string LastError { get; private set; }
        public static string Folder =>
#if UNITY_EDITOR
            Path.GetFullPath("Library/FestivalQaTelemetry");
#else
            Path.Combine(Application.persistentDataPath, "Festival", "QA");
#endif
        public static string ExportFolder =>
#if UNITY_EDITOR
            Path.GetFullPath("Library/FestivalQaExports");
#else
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FestivalQA_exports");
#endif
        static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        public static string Station(string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "station-id.txt");
            if (File.Exists(path) && Guid.TryParse(File.ReadAllText(path), out var old)) return old.ToString("N");
            string id = Guid.NewGuid().ToString("N"); AtomicWrite(path, id); return id;
        }
        static string Id(string value) => Guid.Parse(value).ToString("N");
        static void AtomicWrite(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = path + ".pending";
            using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(pending, path, path + ".bak"); else File.Move(pending, path);
        }
        public static bool Save(FestivalQaRun run, string folder)
            => TryWrite(() => AtomicWrite(Path.Combine(folder, "run-" + Id(run.run) + ".json"), JsonUtility.ToJson(run)));
        public static bool Save(FestivalQaErrors errors, string folder)
            => TryWrite(() => AtomicWrite(Path.Combine(folder, "errors-" + Id(errors.session) + ".json"), JsonUtility.ToJson(errors)));
        static bool TryWrite(Action write)
        {
            try { write(); LastError = null; return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException)
            { LastError = "QA 저장에 실패했습니다. 저장 공간과 폴더 권한을 확인해 주세요."; return false; }
        }
        static T Read<T>(string path, Func<T, bool> valid) where T : class
        {
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try { var value = JsonUtility.FromJson<T>(File.ReadAllText(candidate)); if (value != null && valid(value)) return value; }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { }
            }
            return null;
        }
        static IEnumerable<string> Sources(string folder, string prefix) => Directory.GetFiles(folder, prefix + "-*.json*")
            .Where(p => p.EndsWith(".json") || p.EndsWith(".json.bak"))
            .Select(p => p.EndsWith(".bak") ? p.Substring(0, p.Length - 4) : p).Distinct();
        public static FestivalQaSnapshot ReadAll(string folder)
        {
            var result = new FestivalQaSnapshot();
            if (!Directory.Exists(folder)) return result;
            var seen = new HashSet<string>();
            foreach (string path in Sources(folder, "run"))
            {
                var run = Read<FestivalQaRun>(path, r => r.schema == 1 && Guid.TryParse(r.run, out _) && Guid.TryParse(r.station, out _)
                    && new[] { "Ashi", "Ari", "Hyuki", "Shini" }.Contains(r.character) && r.frames >= 0 && r.measuredSeconds >= 0
                    && !double.IsNaN(r.measuredSeconds) && !double.IsInfinity(r.measuredSeconds));
                if (run == null) result.unreadable++; else if (seen.Add(run.station + run.run)) result.runs.Add(run);
            }
            foreach (string path in Sources(folder, "errors"))
            {
                var errors = Read<FestivalQaErrors>(path, s => s.schema == 1 && s.errors != null && Guid.TryParse(s.session, out _));
                if (errors == null) result.unreadable++; else result.sessions.Add(errors);
            }
            return result;
        }
        static string Csv(string text)
        {
            text = text ?? "";
            if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        static string Number(double value) => value.ToString("0.###", Culture);
        public static string CharacterName(string key)
            => key == "Ashi" ? "아시" : key == "Ari" ? "아리" : key == "Hyuki" ? "혁이" : key == "Shini" ? "신이" : "";
        static void Entry(ZipArchive zip, string name, string text, bool bom = false)
        {
            var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(bom))) writer.Write(text);
        }
        public static string Export(string folder, string destination)
        {
            var data = ReadAll(folder); Directory.CreateDirectory(destination);
            string station = Station(folder);
            string path = Path.Combine(destination, "FestivalQA_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "_" + station.Substring(0, 8) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".zip");
            using (var file = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                var runs = new StringBuilder("PC번호,플레이번호,시작UTC,버전,캐릭터,측정종료,측정프레임,측정초,평균FPS,100ms이상프레임,250ms이상프레임,최장프레임ms\r\n");
                foreach (var r in data.runs.OrderBy(r => r.utc))
                {
                    runs.AppendLine(string.Join(",", Csv(r.station), Csv(r.run), Csv(r.utc), Csv(r.build), Csv(CharacterName(r.character)), r.closed ? "1" : "0",
                        r.frames.ToString(Culture), Number(r.measuredSeconds), Number(r.AverageFps), r.stalls100.ToString(Culture), r.stalls250.ToString(Culture), Number(r.worstFrameMs)));
                    Entry(zip, "raw/run-" + Id(r.run) + ".json", JsonUtility.ToJson(r));
                }
                Entry(zip, "플레이별_QA.csv", runs.ToString(), true);
                var summary = new StringBuilder("캐릭터,시작횟수,선택비율퍼센트,측정프레임,측정초,평균FPS,100ms이상프레임,250ms이상프레임\r\n");
                foreach (string key in new[] { "Ashi", "Ari", "Hyuki", "Shini" })
                {
                    var group = data.runs.Where(r => r.character == key).ToArray();
                    long frames = group.Sum(r => r.frames); double seconds = group.Sum(r => r.measuredSeconds);
                    summary.AppendLine(string.Join(",", Csv(CharacterName(key)), group.Length.ToString(Culture), Number(data.runs.Count > 0 ? 100d * group.Length / data.runs.Count : 0),
                        frames.ToString(Culture), Number(seconds), Number(seconds > 0 ? frames / seconds : 0), group.Sum(r => r.stalls100).ToString(Culture), group.Sum(r => r.stalls250).ToString(Culture)));
                }
                Entry(zip, "캐릭터별_요약.csv", summary.ToString(), true);
                var errorCsv = new StringBuilder("PC번호,실행세션,버전,플레이번호,화면구분,오류종류,오류분류,발생횟수,호출함수\r\n");
                foreach (var session in data.sessions)
                {
                    foreach (var e in session.errors) errorCsv.AppendLine(string.Join(",", Csv(session.station), Csv(session.session), Csv(session.build), Csv(e.run), Csv(e.context), Csv(e.kind), Csv(e.category), e.count.ToString(Culture), Csv(e.methods)));
                    if (session.overflow > 0) errorCsv.AppendLine(string.Join(",", Csv(session.station), Csv(session.session), Csv(session.build), "\"\"", "\"\"", "\"overflow\"", "\"추가 오류 집계\"", session.overflow.ToString(Culture), "\"\""));
                    Entry(zip, "raw/errors-" + Id(session.session) + ".json", JsonUtility.ToJson(session));
                }
                Entry(zip, "오류.csv", errorCsv.ToString(), true);
                Entry(zip, "읽어주세요.txt", "페스티벌 최소 QA\r\n캐릭터는 실제 출전 횟수 기준입니다. 같은 사람이 재도전해도 별도 한 판입니다.\r\n" +
                    "FPS = 측정 프레임 수 / 실제 경과 시간. 전투만 측정하며 로딩, 일시정지, 창 전환, 메뉴, 결과 연출은 제외합니다.\r\n" +
                    "100ms(0.1초) 이상을 끊김, 250ms(0.25초) 이상을 심한 끊김으로 기록합니다. 250ms 기록은 100ms에도 포함됩니다. 연속된 느린 프레임은 각각 셉니다.\r\n" +
                    "측정종료 0은 진행 중 또는 비정상 종료된 부분 기록입니다. 평균FPS 0/측정프레임 0은 측정 자료가 없다는 뜻입니다.\r\n" +
                    "캐릭터별 평균FPS는 총 프레임/총 측정시간으로 계산합니다. 선택 비율은 참가자 수가 아닌 출전 횟수 비율입니다.\r\n" +
                    "성명, 학과, 점수, 처치, 대시/액티브 사용량, 키 입력, PC 계정명/하드웨어 식별자는 수집하지 않습니다. PC번호는 무작위 코드입니다.\r\n" +
                    "오류는 Unity Error/Exception/Assert의 종류와 호출함수만 저장합니다. 메시지 본문, 경로, 입력값, 경고 로그는 제외합니다. 하드 크래시 원인까지 보장하지는 않습니다.\r\n" +
                    "PC번호+플레이번호 또는 실행세션으로 중복 내보내기를 제거해 합칠 수 있습니다. 이전 ZIP은 누적 데이터를 포함합니다.\r\n" +
                    "읽지 못해 제외된 파일: " + data.unreadable + "\r\n원본은 행사 PC에 남습니다. 자동 업로드는 하지 않습니다.\r\n", true);
            }
            File.Move(path + ".pending", path); return path;
        }
    }
}
