using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vampire;

// Isolated playback check: does not open, save or modify gameplay scenes/preferences.
[InitializeOnLoad]
public static class FoodBossAudioSmoke
{
    const string Key="FoodBossAudioSmoke";
    static double deadline,next,audioDeadline;
    static bool originalEditorMute,originalRunInBackground;
    static int bossIndex,eventIndex,checkedCount,stage;
    static FoodBossRuntime boss;
    static AudioSource voice;
    static GamePreferenceValues original;
    static FoodBossSound[] events;
    static FoodBossAudioSmoke(){if(SessionState.GetBool(Key,false))Attach();}
    public static void Run()
    {
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Attach();EditorApplication.EnterPlaymode();
    }
    static void RestoreSettings()
    {
        if(original==null)return;
        GamePreferences.Apply(original,false,false);EditorUtility.audioMasterMute=originalEditorMute;Application.runInBackground=originalRunInBackground;
    }
    static void Attach(){deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static int ChanceSeed(float low,float high)
    {
        for(int seed=0;seed<10000;seed++){UnityEngine.Random.InitState(seed);float roll=UnityEngine.Random.value;if(roll>=low&&roll<high)return seed;}
        throw new Exception("Could not select deterministic probability boundary seed");
    }
    static void CheckCryRules(FoodBossRuntime actor)
    {
        var originalRandom=UnityEngine.Random.state;
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var cooldown=typeof(FoodBossAudio).GetField("nextHurtCry",flags);
        var chase=typeof(FoodBossRuntime).GetMethod("SetChasing",flags);
        var cancel=typeof(FoodBossRuntime).GetMethod("Cancel",flags);
        var audio=actor.Audio;
        try
        {
            int hurtYes=ChanceSeed(.18f,.19f),hurtNo=ChanceSeed(.19f,.20f);
            int chaseYes=ChanceSeed(.34f,.35f),chaseNo=ChanceSeed(.35f,.36f);
            Require(FoodBossAudio.HurtCryChance==.19f&&FoodBossAudio.HurtCryInterval==2&&FoodBossAudio.ChaseCryChance==.35f,"Configured cry rules");
            int count=audio.PlayedCount;
            UnityEngine.Random.InitState(hurtYes);actor.TakeDamage(0);actor.TakeDamage(float.NaN);
            Require(audio.PlayedCount==count,"Rejected damage must not cry");
            UnityEngine.Random.InitState(hurtNo);actor.TakeDamage(1);
            Require(audio.PlayedCount==count,"19 percent upper boundary");
            UnityEngine.Random.InitState(hurtYes);actor.TakeDamage(1);
            Require(audio.PlayedCount==count+1,"Actual damage triggers selected hurt roll");
            Require(Mathf.Abs((float)cooldown.GetValue(audio)-Time.time-2)<.001f,"Two second cooldown begins after played hurt cry");
            audio.Stop();UnityEngine.Random.InitState(hurtYes);actor.TakeDamage(1);
            Require(audio.PlayedCount==count+1,"Rapid repeat hit stays silent even after clip stopped");
            cooldown.SetValue(audio,Time.time+.001f);UnityEngine.Random.InitState(hurtYes);
            Require(!audio.TryHurtCry(),"Cooldown remains closed before deadline");
            cooldown.SetValue(audio,Time.time);UnityEngine.Random.InitState(hurtYes);
            Require(audio.TryHurtCry(),"Cooldown opens exactly at deadline");audio.Stop();
            chase.Invoke(actor,new object[]{false});count=audio.PlayedCount;
            UnityEngine.Random.InitState(chaseNo);chase.Invoke(actor,new object[]{true});
            Require(audio.PlayedCount==count,"35 percent upper boundary");
            UnityEngine.Random.InitState(chaseYes);
            for(int i=0;i<60;i++)chase.Invoke(actor,new object[]{true});
            Require(audio.PlayedCount==count,"Continuous chase must not reroll each frame");
            chase.Invoke(actor,new object[]{false});chase.Invoke(actor,new object[]{true});
            Require(audio.PlayedCount==count+1,"New movement start gets a fresh 35 percent roll");
            UnityEngine.Random.InitState(hurtYes);cooldown.SetValue(audio,Time.time);
            Require(!audio.TryHurtCry(),"Cries cannot overlap");
            audio.Stop();audio.Play(FoodBossSound.Beam); // absent on Rolpangi: no effect
            audio.Play(FoodBossSound.Skill1);count=audio.PlayedCount;
            UnityEngine.Random.InitState(hurtYes);Require(!audio.TryHurtCry(),"Pattern sound has priority over hurt cry");
            chase.Invoke(actor,new object[]{false});UnityEngine.Random.InitState(chaseYes);chase.Invoke(actor,new object[]{true});
            Require(audio.PlayedCount==count,"Movement cry cannot displace active pattern audio");
            audio.Stop();chase.Invoke(actor,new object[]{false});Time.timeScale=0;UnityEngine.Random.InitState(chaseYes);chase.Invoke(actor,new object[]{true});
            Require(audio.PlayedCount==count,"Paused movement cannot cry");Time.timeScale=1;
            chase.Invoke(actor,new object[]{true});Require(audio.PlayedCount==count,"Resume is not a new movement start");
            chase.Invoke(actor,new object[]{false});UnityEngine.Random.InitState(chaseYes);chase.Invoke(actor,new object[]{true});
            Require(actor.Play(FoodBossAttack.Basic),"Pattern starts while cry is active");
            var fades=(float[])typeof(FoodBossAudio).GetField("fadeLeft",flags).GetValue(audio);
            Require(fades.Any(f=>f>0&&f<=.041f),"Pattern startup fades current cry");cancel.Invoke(actor,null);
            Debug.Log("FOOD_CRY_RULES_PASS probability boundaries, damage, cooldown, movement edges, pause, priority");
        }
        finally{Time.timeScale=1;cancel.Invoke(actor,null);audio.Stop();UnityEngine.Random.state=originalRandom;}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(SessionState.GetBool(Key+"Done",false))
        {
            if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
            else if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);}
            return;
        }
        try
        {
            Require(EditorApplication.timeSinceStartup<deadline,"Playback check timed out");
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(stage==0)
            {
                originalEditorMute=EditorUtility.audioMasterMute;EditorUtility.audioMasterMute=false;
                originalRunInBackground=Application.runInBackground;Application.runInBackground=true;
                original=GamePreferences.Current.Copy();var settings=original.Copy();settings.effects=1;settings.master=1;settings.muted=false;settings.pauseOnFocusLoss=false;settings.muteOnFocusLoss=false;
                GamePreferences.Apply(settings,false,false);Time.timeScale=1;AudioListener.pause=false;
                var camera=new GameObject("Playback test camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.gameObject.AddComponent<AudioListener>();
                stage=1;next=EditorApplication.timeSinceStartup+2;return;
            }
            if(EditorApplication.timeSinceStartup<next)return;
            if(stage==1)
            {
                if(boss==null)
                {
                    boss=FoodBossRuntime.Spawn((FoodBossKind)bossIndex,null,Vector2.zero);boss.AutoPatterns=false;boss.transform.position=Vector3.zero;
                    Require(boss.Audio.HasAllClips,"Missing resource for boss "+bossIndex);
                    if(bossIndex==0)CheckCryRules(boss);
                    events=Enum.GetValues(typeof(FoodBossSound)).Cast<FoodBossSound>().Where(s=>s!=FoodBossSound.Beam&&s!=FoodBossSound.Consume||s==FoodBossSound.Beam&&bossIndex==3||s==FoodBossSound.Consume&&bossIndex==0).ToArray();eventIndex=0;
                }
                var sound=events[eventIndex];boss.Audio.Stop();boss.Audio.Play(sound);
                voice=boss.GetComponents<AudioSource>().FirstOrDefault(v=>v.isPlaying);
                Require(voice!=null,"Not playing "+bossIndex+"-"+sound);
                // Loop only inside this test so a slow editor frame cannot outlast a short one-shot.
                voice.loop=true;
                Require(voice.clip.loadState==AudioDataLoadState.Loaded&&voice.volume>0,"Unloaded/muted clip");
                var pcm=new float[voice.clip.samples*voice.clip.channels];Require(voice.clip.GetData(pcm,0)&&pcm.Any(v=>Mathf.Abs(v)>.001f),"Silent decoded clip");
                stage=2;next=EditorApplication.timeSinceStartup+.06;audioDeadline=EditorApplication.timeSinceStartup+3;
            }
            else if(stage==2)
            {
                if(voice.isPlaying&&voice.timeSamples==0&&EditorApplication.timeSinceStartup<audioDeadline)return;
                Require(voice.isPlaying&&voice.timeSamples>0,"Audio clock did not advance: "+bossIndex+"-"+events[eventIndex]+" playing="+voice.isPlaying+" samples="+voice.timeSamples);
                Debug.Log("FOOD_AUDIO_PASS "+bossIndex+"-"+events[eventIndex]+" samples="+voice.timeSamples);
                checkedCount++;voice.loop=false;boss.Audio.Stop();eventIndex++;stage=1;
                if(eventIndex>=events.Length){UnityEngine.Object.DestroyImmediate(boss.gameObject);boss=null;bossIndex++;}
                if(bossIndex==5){Require(checkedCount==67,"Wrong clip count");RestoreSettings();Debug.Log("FOOD_AUDIO_SMOKE_PASS clips=67");SessionState.SetBool(Key+"Done",true);}
            }
        }
        catch(Exception ex)
        {
            Debug.LogError("FOOD_AUDIO_SMOKE_FAIL "+ex);RestoreSettings();
            SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);
        }
    }
}
