using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class FestivalQaSmoke
    {
        public static int Checks { get; private set; }
        public static void Check(bool value, string label)
        {
            if (!value) throw new Exception("FESTIVAL_QA_FAIL " + label);
            Checks++; Debug.Log("FESTIVAL_QA_PASS " + label);
        }
        public static void DataChecks()
        {
            Checks = 0;
            string folder = Path.GetFullPath("Library/FestivalQA/minimal-" + Guid.NewGuid().ToString("N"));
            string station = FestivalQaStore.Station(folder);
            Check(FestivalQaStore.Station(folder) == station, "anonymous station survives restart");
            var sample = new FestivalQaRun(); var meter = new FestivalQaFrameMeter(sample);
            meter.Observe(0, true); meter.Observe(.02, true); meter.Observe(.12, true); meter.Observe(.37, true);
            Check(sample.frames == 3 && sample.stalls100 == 2 && sample.stalls250 == 1, "exact hitch thresholds and inclusive severe count");
            Check(Math.Abs(sample.AverageFps - 3/.37) < .0001 && Math.Abs(sample.worstFrameMs - 250) < .0001, "FPS is frames per wall time");
            meter.Observe(10, false); meter.Observe(100, true); meter.Observe(100.02, true);
            Check(sample.frames == 4 && sample.stalls100 == 2 && sample.worstFrameMs < 251, "pause and loading boundaries do not create stalls");
            meter.ResetBoundary(); meter.Observe(500, true); meter.Observe(500.02, true);
            Check(sample.frames == 5 && sample.stalls100 == 2, "focus return discards inactive gap");

            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i=0; i<1000; i++)
            {
                var r = new FestivalQaRun { station = station, run = Guid.NewGuid().ToString("N"), character = new[]{"Ashi","Ari","Hyuki","Shini"}[i%4],
                    utc = DateTime.UtcNow.ToString("o"), rules = FestivalDemo.Rules, build = "test", frames = i%2 == 0 ? 2700 : 1350,
                    measuredSeconds = 45, stalls100 = 2, stalls250 = 1, worstFrameMs = 250, closed = true };
                if (!FestivalQaStore.Save(r, folder)) throw new Exception("Could not store QA sample " + i);
                if (i == 0)
                {
                    Check(FestivalQaStore.Save(r, folder), "same run checkpoint replaces rather than appends");
                    File.WriteAllText(Path.Combine(folder,"run-"+r.run+".json"), "broken");
                    Check(FestivalQaStore.ReadAll(folder).runs.Count == 1, "corrupt primary recovers previous checkpoint");
                }
            }
            var all = FestivalQaStore.ReadAll(folder);
            Check(all.runs.Count == 1000 && all.unreadable == 0, "1000 sequential runs plus backup count exactly once");
            Check(all.runs.GroupBy(r=>r.character).All(g=>g.Count()==250), "four character choice ratios remain exact");
            Check(Math.Abs(all.AverageFps - 45) < .001, "mixed FPS populations aggregate by measured time");
            Check(all.runs.Sum(r=>r.stalls100)==2000 && all.runs.Sum(r=>r.stalls250)==1000, "1000 run hitch totals");
            var error = FestivalQaRecorder.Describe("Exception", "NullReferenceException: ParticipantName secret@example.com", "Vampire.Character.Update () (at C:/Users/PrivateUser/Project/Character.cs:99)\nat Vampire.Player.Save (System.String name)", "gameplay", all.runs[0].run);
            string json = JsonUtility.ToJson(error);
            Check(error.category == "NullReferenceException" && error.methods.Contains("Vampire.Character.Update"), "error type and method retained");
            Check(!json.Contains("ParticipantName") && !json.Contains("PrivateUser") && !json.Contains("secret@") && !json.Contains("System.String"), "error payload strips personal values and paths");
            var session = new FestivalQaErrors { station=station,session=Guid.NewGuid().ToString("N"),build="test",utc=DateTime.UtcNow.ToString("o") };
            error.count=7;session.errors.Add(error);
            Check(FestivalQaStore.Save(session,folder) && FestivalQaStore.ReadAll(folder).Errors==7, "error groups retain occurrence count");
            File.WriteAllText(Path.Combine(folder,"run-"+Guid.NewGuid().ToString("N")+".json"), "broken");
            Check(FestivalQaStore.ReadAll(folder).unreadable==1, "unrecoverable file reported without hiding intact runs");
            string blocker=Path.Combine(folder,"not-a-directory");File.WriteAllText(blocker,"test");
            Check(!FestivalQaStore.Save(all.runs[0],blocker), "storage failure does not throw into gameplay");
            var oldCulture=CultureInfo.CurrentCulture;
            string zipPath;
            try { CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");zipPath=FestivalQaStore.Export(folder,Path.Combine(folder,"exports")); }
            finally { CultureInfo.CurrentCulture=oldCulture; }
            using(var zip=ZipFile.OpenRead(zipPath))
            {
                string Read(string name) { using(var reader=new StreamReader(zip.GetEntry(name).Open()))return reader.ReadToEnd(); }
                string csv=Read("플레이별_QA.csv"), summary=Read("캐릭터별_요약.csv"), errors=Read("오류.csv");
                Check(csv.Split('\n').Length==1002, "export contains all 1000 run rows and header");
                Check(summary.Contains("250,25,") && summary.Split('\n').Length==6, "summary CSV has four characters and correct percentages");
                Check(zip.Entries.Count(e=>e.FullName.StartsWith("raw/run-"))==1000, "ZIP preserves normalized raw samples for future merge");
                Check(!errors.Contains("PrivateUser") && !errors.Contains("secret@"), "ZIP does not export raw log content");
                Check(!csv.Contains("department") && !csv.Contains("score") && !csv.Contains("dashes") && !csv.Contains("activeUses"), "export is limited to selected QA fields");
                Check(Read("읽어주세요.txt").Contains("파일: 1"), "export documents unreadable file count");
            }
            Check(Directory.GetFiles(folder,"run-*.json").Length==1001, "export leaves source records intact");
            string second=FestivalQaStore.Export(folder,Path.Combine(folder,"exports"));
            Check(second!=zipPath && File.Exists(second), "repeated export never overwrites previous ZIP");
            timer.Stop();
            File.WriteAllText("Library/FestivalQA/minimal-data-result.txt",$"checks={Checks}\n1000_save_read_export_ms={timer.ElapsedMilliseconds}\nzip_bytes={new FileInfo(zipPath).Length}\nexport={zipPath}\n");
        }
        public static void BufferChecks()
        {
            var recorder=UnityEngine.Object.FindObjectOfType<FestivalQaRecorder>();
            Check(recorder!=null, "festival QA recorder installed");
            var receive=typeof(FestivalQaRecorder).GetMethod("OnLog",BindingFlags.Instance|BindingFlags.NonPublic);
            long before=FestivalQaStore.ReadAll(FestivalQaStore.Folder).Errors;
            receive.Invoke(recorder,new object[]{"ignored warning","",LogType.Warning});
            for(int i=0;i<300;i++)receive.Invoke(recorder,new object[]{"NullReferenceException: test", "Vampire.Test.Frame () (at C:/Users/Secret/File.cs:9)",LogType.Exception});
            FestivalQaRecorder.Checkpoint();
            var result=FestivalQaStore.ReadAll(FestivalQaStore.Folder);
            Check(result.Errors-before==300, "error burst is bounded while total occurrences remain accurate");
            Check(result.sessions.Any(s=>s.overflow>=44), "overflow explicitly reported");
        }
    }
}
