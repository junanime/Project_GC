using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class BloodClotMissionSmoke
    {
        const string Key="BloodClotMissionSmoke";
        static bool started;
        static double deadline;
        static int checks;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Saved { public string key;public bool existed;public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static BloodClotMissionSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            RestorePrefs();
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).Distinct();
            SessionState.SetString(Key+"Prefs",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,existed=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters.First(c=>OctoberArt.CharacterKey(c)=="Ashi");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Exception||type==LogType.Assert){SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("CLOT_MISSION_TIMEOUT");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                RestorePrefs();bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("CLOT_MISSION_FINISHED checks="+checks+" failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||!EditorApplication.isPlaying||level==null||level.PlayerCharacter==null||!level.InitialFieldObjectsReady||StageEntryLoading.IsLoading)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool value,string message)
        {if(!value)throw new Exception("CLOT_MISSION_FAIL "+message);checks++;Debug.Log("CLOT_MISSION_PASS "+message);}
        static void RestorePrefs()
        {
            var saved=SessionState.GetString(Key+"Prefs","");if(string.IsNullOrEmpty(saved))return;
            foreach(var value in JsonUtility.FromJson<Backup>(saved).values)
                if(value.existed)PlayerPrefs.SetInt(value.key,value.value);else PlayerPrefs.DeleteKey(value.key);
            SessionState.EraseString(Key+"Prefs");PlayerPrefs.Save();
        }
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;RunScore.Exclude("Blood clot mission verification");
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
            Time.timeScale=1;
            var entities=level.EntityManager;var player=level.PlayerCharacter;var skills=player.Skills;
            foreach(var needle in Object.FindObjectsOfType<SyringeDartAbility>())needle.enabled=false;
            level.SetRunFlowPaused(true);
            void ClearEnemies(){foreach(var m in entities.LivingMonsters.ToArray())if(m!=null&&!(m is BloodClotMonster))m.gameObject.SetActive(false);}
            ClearEnemies();
            var counts=new int[3];for(int i=0;i<100;i++)counts[(int)BloodClotOvercharge.PickChallenge(i)]++;
            Check(counts.SequenceEqual(new[]{38,31,31}),"exact 38/31/31 draw buckets");
            var center=player.transform.position+Vector3.right;
            void Move(Vector3 p){player.transform.position=p;player.GetComponent<Rigidbody2D>().position=p;}
            BloodClotOvercharge New(BloodClotOvercharge.Challenge kind)
            {
                var obj=new GameObject("Mission verification");obj.transform.position=center;
                var charge=obj.AddComponent<BloodClotOvercharge>();
                typeof(BloodClotOvercharge).GetProperty("Kind").SetValue(charge,kind);
                Move(center);Check(charge.Begin(),kind+" starts");ClearEnemies();return charge;
            }
            void Kill(bool byPlayer=true)
            {
                var monster=entities.SpawnOverchargeElite((Vector2)center+Vector2.right*12);
                Check(monster!=null,"existing roster supplies a target");
                // Run the actual synchronous death notification; stop later loot/animation
                // so generated XP cannot open unrelated level-up menus in this focused test.
                monster.StartCoroutine(monster.Killed(byPlayer));monster.gameObject.SetActive(false);
            }
            var charge=New(BloodClotOvercharge.Challenge.Kills);
            Check(charge.Target==25,"existing 25-kill target retained");
            Kill(false);Check(charge.Progress==0,"environmental death excluded");
            Move(center+Vector3.right*8);Kill();Check(charge.Progress==0,"outside player excluded even for valid kill");
            Move(center);Time.timeScale=0;Kill();Check(charge.Progress==0,"paused kills excluded");Time.timeScale=1;
            for(int i=0;i<24;i++)Kill();Check(charge.Progress==24&&!charge.Enhanced,"24 kills do not finish");
            Kill();Check(charge.Enhanced&&charge.Progress==25,"25th death completes");
            Kill();Check(charge.Progress==25,"completed mission cannot count again");Object.Destroy(charge.gameObject);yield return null;

            charge=New(BloodClotOvercharge.Challenge.ActiveKills);
            skills.Restore(0,0,0);Kill();Check(charge.Progress==0,"inactive ordinary kill excluded");
            Check(skills.TryActivate(),"actual Ashi active begins");
            for(int i=0;i<4;i++)Kill();Check(charge.Progress==4,"buff-active kills count without a direct spell projectile");
            skills.Restore(0,0,0);Kill();Check(charge.Progress==4,"active ending preserves accumulated kills");
            Check(skills.TryActivate(),"second active begins");
            for(int i=0;i<4;i++)Kill();Check(charge.Enhanced&&charge.Progress==8,"active kills accumulate across casts to eight");
            skills.Restore(0,0,0);Object.Destroy(charge.gameObject);yield return null;

            int sequence=skills.DashSequence;
            player.AddDashCharge(3);
            Check(player.TryDash(),"actual dash input succeeds before mission");
            Check(skills.DashSequence==sequence+1,"actual character dash updates sequence");
            while(player.IsDashing)yield return null;
            charge=New(BloodClotOvercharge.Challenge.DashKills);
            Kill();Check(charge.Progress==0,"dash before mission start is not reused");
            // Trigger the same skill callback as Character.Dash, independent of charge refill.
            skills.DashStarted();Kill();Check(charge.Progress==1,"first death in fresh dash window counts immediately");
            Time.timeScale=0;float remaining=charge.DashRemaining;yield return new WaitForSecondsRealtime(.12f);
            Check(Mathf.Abs(charge.DashRemaining-remaining)<.001f,"pause freezes five-second clock");Time.timeScale=1;
            yield return new WaitForSeconds(5.1f);
            Kill();Check(charge.Progress==0&&!charge.Enhanced,"expired attempt resets and late death cannot count");
            skills.DashStarted();for(int i=0;i<3;i++)Kill();
            skills.DashStarted();Kill();Check(charge.Progress==1,"another dash starts a new attempt rather than extending old kills");
            Move(center+Vector3.left*8);Kill();Check(charge.Progress==1,"dash kills require player inside ring");Move(center);
            for(int i=0;i<6;i++)Kill();Check(charge.Progress==7&&!charge.Enhanced,"seven within window not enough");
            Kill();Check(charge.Enhanced&&charge.Progress==8,"eight within one window completes");
            yield return new WaitForSeconds(5.1f);Check(charge.Enhanced&&charge.Progress==8,"success survives window expiry");
            Object.Destroy(charge.gameObject);
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
