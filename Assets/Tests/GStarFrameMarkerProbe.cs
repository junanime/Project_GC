#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace Vampire.Tests
{
    // Opt-in diagnostic run. Nested marker times are inclusive and must not be added.
    // Record only the main thread except for engine counters explicitly reporting other threads.
    public sealed class GStarFrameMarkerProbe : IDisposable
    {
        static readonly string[] CounterNames = { "GC Allocated In Frame", "GC Used Memory", "Draw Calls Count",
            "Total Batches Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Dynamic Bodies",
            "Active Dynamic Bodies", "Contacts", "Total Contacts", "Physics2D Total Contacts",
            "Particle Count", "Visible Skinned Meshes Count", "Shadow Casters Count" };
        readonly List<(string Name,string Unit,ProfilerRecorder Recorder)> counters=new List<(string,string,ProfilerRecorder)>();
        readonly UnityEngine.FrameTiming[] latest=new UnityEngine.FrameTiming[1];
        readonly List<double> gpuTimes=new List<double>(1024);
        readonly List<double> cpuTimes=new List<double>(1024);
        readonly string folder;
        readonly int stage;
        readonly bool live;
        bool paused;
        ulong priorStamp;

        const int MaxFrames = 1200;
        static readonly bool Enabled = Environment.GetCommandLineArgs().Contains("-perfProfile");
        static readonly string[] GameMarkers = { "Monster", "Collectable", "ExpGem", "Projectile",
            "Syringe", "MotionVfx", "Augment", "Compression", "Ability", "SpatialHash",
            "ZPositioner", "Oral", "Stomach", "Colon", "DamageText", "Pharmacy", "Character",
            "Vampire.", "BloodClot", "EntityManager", "StageEvent", "TMP", "TextMesh", "SpriteRenderer" };
        static readonly string[] EngineMarkers = { "ScriptRun", "PlayerLoop", "BehaviourUpdate",
            "DelayedCallManager", "FixedBehaviour", "Physics2D", "Physics.Processing", "Camera.Render",
            "RenderLoop", "DrawRenderers", "Render.Transparent", "Render.Opaque", "RenderPipeline",
            "CullResults", "UpdateRendererBoundingVolumes", "Animator", "Canvas.", "UI.", "GraphicRebuild",
            "LayoutRebuild", "BatchRenderer", "GC.Collect", "GarbageCollector", "Gfx.", "WaitForTargetFPS",
            "PresentAfterDraw", "PlayerRender", "Graphics.Present", "Mesh.Draw", "Physics2D", "Physics 2D", "TransformChanged", "Semaphore.Wait", "WaitForJobGroup", "UpdateCanvases", "UIElements", "VFX", "PostLateUpdate", "ParticleSystem", "DrawMesh", "UpdateAllRenderers" };
        static readonly string[] FrameCounters = { "Main Thread", "Render Thread", "GPU Frame Time",
            "CPU Total Frame Time", "CPU Main Thread Frame Time", "CPU Render Thread Frame Time" };

        string path;
        readonly int limit;
        readonly List<(string Name, string Category, bool Counter, ProfilerRecorder Recorder)> tracks =
            new List<(string, string, bool, ProfilerRecorder)>();
        int recorded;
        bool stopped;
        GStarFrameMarkerProbe(string folder, int stage, string scenario, int frames,bool live=false)
        {
            this.folder=folder;this.stage=stage;this.live=live;
            path = Path.Combine(folder, $"markers-stage{stage}-{scenario}.csv");
            limit = Math.Min(frames, MaxFrames);
        }
        public static GStarFrameMarkerProbe Prepare(string folder, int stage, string scenario, int frames) =>
            Enabled ? new GStarFrameMarkerProbe(folder, stage, scenario, frames) : null;

        public static GStarFrameMarkerProbe Native(string folder,int stage)
        {
            if (!Environment.GetCommandLineArgs().Contains("-qaProfile")) return null;
            var result=new GStarFrameMarkerProbe(folder,stage,"live",1200,true);
            result.Start();
            return result;
        }

        bool CrossThread(string name) => live && (name.StartsWith("Gfx.",StringComparison.Ordinal) ||
            name.StartsWith("RenderLoop",StringComparison.Ordinal) || name.StartsWith("ParticleSystem",StringComparison.Ordinal));
        public void Start()
        {
            // Called after the fixture has warmed up so generated script/renderer markers exist.
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var info = ProfilerRecorderHandle.GetDescription(handle);
                string name = info.Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (info.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds)
                {
                    if (!live || (info.Flags & MarkerFlags.Counter)==0 || !CounterNames.Contains(name) &&
                        !(info.Category.Name.IndexOf("Physics",StringComparison.OrdinalIgnoreCase)>=0 &&
                        (name.IndexOf("Contact",StringComparison.OrdinalIgnoreCase)>=0 ||
                         name.IndexOf("Bod",StringComparison.OrdinalIgnoreCase)>=0))) continue;
                    var stat=new ProfilerRecorder(handle,limit,ProfilerRecorderOptions.StartImmediately);
                    if (stat.Valid) counters.Add((name,info.UnitType.ToString(),stat)); else stat.Dispose();
                    continue;
                }
                bool counter = (info.Flags & MarkerFlags.Counter) != 0;
                if (counter ? !FrameCounters.Contains(name) :
                    !GameMarkers.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    !EngineMarkers.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                var options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame;
                if (!counter && !CrossThread(name)) options |= ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
                var recorder = new ProfilerRecorder(handle, limit, options);
                if (!recorder.Valid) { recorder.Dispose(); continue; }
                tracks.Add((name, info.Category.Name, counter, recorder));
            }
        }

        public void SetPaused(bool value)
        {
            if (!live || paused==value) return;
            paused=value;
            foreach (var entry in tracks) {var rec=entry.Recorder;if(value)rec.Stop();else rec.Start();}
            foreach (var entry in counters) {var rec=entry.Recorder;if(value)rec.Stop();else rec.Start();}
        }
        public void NativeFrame()
        {
            if (!live || paused) return;
            recorded++;
            UnityEngine.FrameTimingManager.CaptureFrameTimings();
            if (UnityEngine.FrameTimingManager.GetLatestTimings(1,latest)==0) return;
            var t=latest[0];
            if(t.frameStartTimestamp==priorStamp) return;
            priorStamp=t.frameStartTimestamp;
            if (t.gpuFrameTime>0) gpuTimes.Add(t.gpuFrameTime);
            if (t.cpuFrameTime>0) cpuTimes.Add(t.cpuFrameTime);
        }
        public void WriteInterval(int second)
        {
            SetPaused(true);
            path=Path.Combine(folder,$"markers-stage{stage}-live-{second:D3}s.csv");
            WriteTimes();
            var list=new List<string>{"counter,unit,valid_samples,mean,max"};
            foreach(var entry in counters)
            {
                var rec=entry.Recorder;int n=rec.Count;double sum=0;long max=0;
                for(int i=0;i<n;i++){long v=rec.GetSample(i).Value;sum+=v;max=Math.Max(max,v);}
                list.Add(FormattableString.Invariant($"{entry.Name},{entry.Unit},{n},{sum/Math.Max(1,n):F4},{max}"));
            }
            foreach(var r in new[]{(Name:"FrameTiming GPU",Values:gpuTimes),(Name:"FrameTiming CPU",Values:cpuTimes)})
            {
                int n=r.Values.Count;
                list.Add(FormattableString.Invariant($"{r.Name},Milliseconds,{n},{(n>0?r.Values.Average():0):F4},{(n>0?r.Values.Max():0):F4}"));
                r.Values.Clear();
            }
            File.WriteAllLines(Path.Combine(folder,$"counters-stage{stage}-live-{second:D3}s.csv"),list);
            foreach(var entry in tracks){var rec=entry.Recorder;rec.Reset();}
            foreach(var entry in counters){var rec=entry.Recorder;rec.Reset();}
            // Resume next game frame, after the diagnostic CSV writing has returned.
            recorded=0;
        }

        public void FrameCompleted()
        {
            if (stopped) return;
            if (++recorded == limit) Stop();
        }
        void Stop()
        {
            if (stopped) return;
            stopped = true;
            foreach (var entry in tracks) entry.Recorder.Stop();
        }
        void WriteTimes()
        {
            var rows = new List<(double Mean, string Row)>(tracks.Count);
            var ms = new double[Math.Max(recorded, 1)];
            int p95 = (int)Math.Ceiling(ms.Length * .95) - 1;
            foreach (var entry in tracks)
            {
                // Some event recorders omit inactive frames. Keep their zeros so the
                // distribution describes the whole interval, including pauses between hits.
                Array.Clear(ms, 0, ms.Length);
                int count = Math.Min(recorded, entry.Recorder.Count);
                long calls = 0;
                double total = 0;
                for (int i = 0; i < count; i++)
                {
                    var sample = entry.Recorder.GetSample(i);
                    ms[i] = sample.Value * .000001;
                    total += ms[i];
                    calls += sample.Count;
                }
                Array.Sort(ms);
                double mean = total / ms.Length;
                string scope = entry.Counter ? "engine-counter" : CrossThread(entry.Name) ? "all-threads" : "main-thread";
                rows.Add((mean, string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6:F5},{7:F5},{8:F5}",
                    Csv(entry.Name), Csv(entry.Category), scope, recorded, count, calls, mean, ms[p95], ms[ms.Length - 1])));
            }
            File.WriteAllLines(path, new[] { "marker,category,scope,measured_frames,recorded_frames,sample_calls,mean_inclusive_ms,p95_inclusive_ms,max_inclusive_ms" }
                .Concat(rows.OrderByDescending(x => x.Mean).Select(x => x.Row)));
        }
        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        public void Dispose()
        {
            Stop();
            try { if(!live) WriteTimes(); }
            finally
            {
                foreach (var entry in tracks) entry.Recorder.Dispose();
                foreach (var entry in counters) entry.Recorder.Dispose();
            }
        }
    }
}

#endif
