using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Run with an audio-enabled editor, without -nographics/-noaudio/-quit.
    // Checks the actual scene/prefab and audio clocks; this is not a listening test.
    [InitializeOnLoad]
    public static class StageBgmPlaylistSmoke
    {
        const string Key = "StageBgmPlaylistSmoke";
        static bool started;
        static double deadline;
        static StageBgmPlaylistSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }

        public static void Run()
        {
            SessionState.SetString(Key + "Data", Path.GetFullPath("Library/GStarMusicQA/" + Guid.NewGuid().ToString("N")));
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Done", false);
            SessionState.SetInt(Key + "Passed", 0);
            SessionState.SetInt(Key + "Failed", 0);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach();
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateBoothRecords()
        {
            if (!SessionState.GetBool(Key, false)) return;
            GStarDemo.TestOverride = true;
            GStarDemo.TestDataDirectory = SessionState.GetString(Key + "Data", "");
            GStarSession.TestDirectory = GStarLeaderboard.TestDirectory = GStarDemo.TestDataDirectory;
            GStarSession.ReloadForTests();
            GStarLeaderboard.ReloadForTests();
        }

        static void Attach()
        {
            started = false;
            deadline = EditorApplication.timeSinceStartup + 480;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
        }

        static void Log(string message, string trace, LogType type)
        {
            if (message.StartsWith("BGM_PLAYLIST_FAIL")) return;
            if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
            {
                SessionState.SetInt(Key + "Failed", SessionState.GetInt(Key + "Failed", 0) + 1);
                SessionState.SetBool(Key + "Done", true);
            }
            if (message.Contains("Base BGM START | Ingame") && StageEntryLoading.IsLoading)
                Check(false, "field music must not start behind the stage loading overlay");
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline && !SessionState.GetBool(Key + "Done", false))
            {
                Check(false, "480-second test deadline");
                SessionState.SetBool(Key + "Done", true);
            }
            if (SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying)
                {
                    AudioListener.pause = false;
                    Time.timeScale = 1;
                    EditorApplication.ExitPlaymode();
                    return;
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                int failed = SessionState.GetInt(Key + "Failed", 0);
                Debug.Log($"BGM_PLAYLIST_DONE passed={SessionState.GetInt(Key + "Passed", 0)} failures={failed}");
                EditorApplication.Exit(failed == 0 ? 0 : 1);
                return;
            }
            if (started || !EditorApplication.isPlaying || ApothecaryUI.Instance == null || GameAudioManager.Instance == null) return;
            started = true;
            var host = new GameObject("Stage BGM playlist regression").AddComponent<StageBgmPlaylistRunner>();
            Object.DontDestroyOnLoad(host);
            host.StartCoroutine(Checks());
        }

        static void Check(bool ok, string message)
        {
            string count = Key + (ok ? "Passed" : "Failed");
            SessionState.SetInt(count, SessionState.GetInt(count, 0) + 1);
            if (ok) Debug.Log("BGM_PLAYLIST_PASS " + message);
            else Debug.LogError("BGM_PLAYLIST_FAIL " + message);
        }

        static T Field<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(instance.GetType().Name, name);
            return (T)field.GetValue(instance);
        }

        static AudioSource Base => Field<AudioSource>(GameAudioManager.Instance, "baseBgmSource");
        static AudioSource Override => Field<AudioSource>(GameAudioManager.Instance, "overrideBgmSource");
        static AudioSource Modal => Field<AudioSource>(GameAudioManager.Instance, "modalBgmSource");

        static void OneLayer(AudioSource expected, string label)
        {
            var playing = new[] { Base, Override, Modal }.Where(s => s != null && s.isPlaying && s.volume > 0).ToArray();
            Check(playing.Length == 1 && playing[0] == expected, label + " has exactly one audible BGM layer");
            Check(expected != null && Mathf.Approximately(expected.pitch, 1), label + " preserves pitch 1");
        }

        static IEnumerator Playing(AudioSource source, string label)
        {
            float until = Time.realtimeSinceStartup + 12;
            while (source != null && !source.isPlaying && Time.realtimeSinceStartup < until) yield return null;
            Check(source != null && source.isPlaying && source.clip != null && source.clip.loadState == AudioDataLoadState.Loaded,
                label + " decodes and starts");
        }

        static void Resumed(AudioSource source, AudioClip original, float position, string label)
        {
            Check(source.clip == original, label + " keeps the same clip");
            // Vorbis/streaming clocks are packet-quantized. A rewind to zero is not accepted.
            Check(source.time >= position - .2f && source.time <= position + .8f,
                label + $" resumes its saved position ({position:F2}s -> {source.time:F2}s)");
        }

        static IEnumerator LayerChecks(int stage)
        {
            var selected = Base.clip;
            Base.time = 4;
            yield return new WaitForSecondsRealtime(.3f);
            float position = Base.time;
            GameAudioManager.PlayIngameMusic();
            yield return new WaitForSecondsRealtime(.3f);
            Resumed(Base, selected, position, $"stage {stage} repeated field request");

            position = Base.time;
            GameAudioManager.EnterAugmentSelectionAudio();
            yield return Playing(Modal, "augment");
            yield return new WaitForSecondsRealtime(.3f);
            OneLayer(Modal, "augment");
            Check(Mathf.Abs(Base.time - position) < .2f, "field clock stays paused under augment");
            GameAudioManager.ExitAugmentSelectionAudio();
            yield return new WaitForSecondsRealtime(.3f);
            Resumed(Base, selected, position, "augment return");
            OneLayer(Base, "augment return");

            position = Base.time;
            GameAudioManager.EnterMiniStageAudio();
            yield return Playing(Override, "mini stage");
            OneLayer(Override, "mini stage");
            Override.time = 4;
            yield return new WaitForSecondsRealtime(.3f);
            float miniPosition = Override.time;
            var miniClip = Override.clip;
            GameAudioManager.EnterAugmentSelectionAudio();
            yield return Playing(Modal, "augment over mini stage");
            yield return new WaitForSecondsRealtime(.3f);
            OneLayer(Modal, "augment over mini stage");
            Check(Mathf.Abs(Base.time - position) < .2f, "field clock stays paused under nested mini/augment");
            GameAudioManager.ExitAugmentSelectionAudio();
            yield return new WaitForSecondsRealtime(.3f);
            Resumed(Override, miniClip, miniPosition, "nested augment returns to mini stage");
            OneLayer(Override, "nested augment return");
            GameAudioManager.ExitMiniStageAudio();
            yield return new WaitForSecondsRealtime(.3f);
            Resumed(Base, selected, position, "mini stage return");
            OneLayer(Base, "mini stage return");

            position = Base.time;
            GameAudioManager.StartBossAudio();
            yield return Playing(Override, "boss");
            OneLayer(Override, "boss");
            GameAudioManager.FinishStageBossAudio();
            yield return new WaitForSecondsRealtime(.3f);
            Resumed(Base, selected, position, "boss return");
            OneLayer(Base, "boss return");

            AudioListener.pause = true;
            position = Base.time;
            yield return new WaitForSecondsRealtime(.4f);
            Check(Mathf.Abs(Base.time - position) < .2f, "listener pause holds the field clock");
            AudioListener.pause = false;
            yield return new WaitForSecondsRealtime(.3f);
            Resumed(Base, selected, position, "listener pause return");

            var before = GamePreferences.Current.Copy();
            var quiet = before.Copy(); quiet.music = .23f;
            GamePreferences.PreviewAudio(quiet);
            Check(new[] { Base, Override, Modal }.All(s => Mathf.Abs(s.volume - .23f) < .001f), "music preference reaches all three layers");
            GamePreferences.PreviewAudio(before);

            GameAudioManager.EndRunAudio(false);
            yield return new WaitForSecondsRealtime(.7f);
            Check(new[] { Base, Override, Modal }.All(s => !s.isPlaying), "end-run stays silent through readiness retries");
        }

        static IEnumerator Checks()
        {
            Application.runInBackground = true;
            var prefs = GamePreferences.Current.Copy();
            prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; prefs.muted = false; prefs.master = 1; prefs.music = .55f;
            GamePreferences.Apply(prefs, false);
            var intro = Object.FindObjectOfType<IntroMovie>();
            if (intro != null) typeof(IntroMovie).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(intro, null);
            yield return new WaitForSecondsRealtime(.5f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Audio/GameAudioManager.prefab").GetComponent<GameAudioManager>();
            var original = Field<AudioClip>(prefab, "ingameBgm");
            var additions = Field<AudioClip[]>(prefab, "additionalIngameBgms");
            Check(original != null && original.name == "01_ingame_steady_hunt_loop", "existing gameplay music remains assigned");
            Check(additions == null || additions.Length == 0, "booth prefab has no additional field tracks");
            if (original == null) { SessionState.SetBool(Key + "Done", true); yield break; }
            original.LoadAudioData();
            float until = Time.realtimeSinceStartup + 12;
            while (original.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < until) yield return null;
            Check(original.loadState == AudioDataLoadState.Loaded && original.samples > 0 && original.channels == 2,
                "original field loop decodes as stereo");
            var character = ApothecaryUI.Instance.Config.characters[0];
            foreach (int stage in new[] { 0, 1, 2 })
            {
                if (stage == 2)
                    Check(GStarSession.FinishVisitor() && GStarSession.ResetVisitor(), "fresh QA visitor for the third field");
                CrossSceneData.CharacterBlueprint = character;
                Check(StageEntryLoading.Begin(character, stage: stage), "direct stage " + stage + " loading accepted");
                yield return null;
                while (StageEntryLoading.IsLoading) yield return null;
                var level = Object.FindObjectOfType<LevelManager>();
                Check(level != null, "stage " + stage + " level exists");
                if (level == null) { SessionState.SetBool(Key + "Done", true); yield break; }
                var flow = StageProgression.Ensure(level);
                while (flow.Travelling) yield return null;
                RunScore.Exclude("BGM playlist regression");
                if (TutorialGuide.IsOpen) TutorialGuide.Instance.Close();
                if (level.EntityManager.AbilitySelectionDialog.MenuOpen) level.EntityManager.AbilitySelectionDialog.Close();
                Time.timeScale = 1;
                Check(flow.StageNumber == stage && level.InitialFieldObjectsReady, "stage " + stage + " actual world prepared");
                yield return Playing(Base, "stage " + stage + " field");
                Check(Base.clip == original && Base.loop, "stage " + stage + " loops the original field clip");
                OneLayer(Base, "stage " + stage + " field");

                if (stage == 1)
                {
                    bool onlyOriginal = true;
                    for (int entry = 0; entry < 12; entry++)
                    {
                        GameAudioManager.BeginNextStageMusic();
                        onlyOriginal &= Base.clip == original;
                    }
                    Check(onlyOriginal, "repeat departures keep the original field track");
                    yield return Playing(Base, "post-draw field");
                }

                if (Environment.GetCommandLineArgs().Contains("-captureFieldMusic"))
                {
                    Directory.CreateDirectory("Library/GStarUIQA/field-music");
                    for (int frame = 0; frame < 15; frame++)
                    {
                        yield return new WaitForSecondsRealtime(.1f);
                        GStarUISmoke.Capture($"field-music/stage-{stage}-{frame:00}", 960, 540);
                    }
                    Check(Base.clip == original && Base.isPlaying, "field preview plays original track");
                }
                level.SetRunFlowPaused(true);
                yield return LayerChecks(stage);
                GStarAnalytics.Finish(false, "music_qa");
                Check(GStarSession.CompleteAttempt(false), "finish isolated QA attempt");
                yield return SceneManager.LoadSceneAsync(0);
                yield return new WaitForSecondsRealtime(.5f);
                yield return Playing(Base, "lobby return");
                Check(Base.clip == Field<AudioClip>(GameAudioManager.Instance, "lobbyBgm"), "returning to lobby restores lobby music");
            }
            SessionState.SetBool(Key + "Done", true);
        }
    }

    public sealed class StageBgmPlaylistRunner : MonoBehaviour { }
}
