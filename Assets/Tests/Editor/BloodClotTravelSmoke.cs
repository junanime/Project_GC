using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class BloodClotTravelSmoke
    {
        const string Key="BloodClotTravelSmoke",Output="Library/BloodClotPreview";
        static bool started;static double deadline;
        [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
        [Serializable] class Saved { public string key;public bool exists;public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static BloodClotTravelSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            RestorePrefs();
            Vampire.Editor.BloodClotTravelInstaller.Install();
            BloodClotTravelPreview.Render();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"});
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);Debug.Log("TRAVEL_TIMEOUT");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                RestorePrefs();
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("TRAVEL_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!started&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null){started=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("TRAVEL_FAIL "+text);Debug.Log("TRAVEL_PASS "+text);}
        static void RestorePrefs()
        {
            string path=Output+"/prefs-backup.json";if(!File.Exists(path))return;
            var backup=JsonUtility.FromJson<Backup>(File.ReadAllText(path));
            foreach(var s in backup.values){if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}PlayerPrefs.Save();
            File.Move(path,Output+"/prefs-restored-"+DateTime.UtcNow.Ticks+".json");
        }
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception(n);}
        static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static void Capture(string key)
        {
            var camera=Camera.main;if(camera==null)return;
            var rt=RenderTexture.GetTemporary(960,600,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(960,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();
            File.WriteAllBytes(Output+"/"+key+"-live.png",image.EncodeToPNG());Object.Destroy(image);
            camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            foreach(var scheduler in Object.FindObjectsOfType<StageEventDirector>())scheduler.enabled=false;
            foreach(var scheduler in Object.FindObjectsOfType<TimedSpecialMonsterSpawner>())scheduler.enabled=false;
            foreach(var scheduler in Object.FindObjectsOfType<AcidToadSpawn>())scheduler.enabled=false;
            TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;Time.timeScale=1;
            var player=level.PlayerCharacter;player.Move(Vector2.zero);
            var director=Object.FindObjectOfType<MiniStageDirector>();
            var travel=BloodClotTravel.Ensure(player);var body=player.GetComponent<Rigidbody2D>();
            Vector3 origin=player.transform.position;var blueprint=player.Blueprint;
            var portal=new GameObject("Travel test portal");portal.transform.position=origin+Vector3.right;
            var sr=portal.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/mini_open.png");
            foreach(var key in new[]{"Shini","Hyuki","Ari","Ashi"})
            {
                var frames=BloodClotTravel.Frames(key);
                Check(frames.Length==(key=="Hyuki"?5:16),key+" art frame count");
                var bp=Object.Instantiate(blueprint);bp.name=key=="Shini"?"신이":key=="Hyuki"?"혁이":key=="Ari"?"아리":"아시";
                Set(player,"characterBlueprint",bp);player.transform.position=origin;body.position=origin;player.Move(Vector2.zero);
                float begin=Time.time;
                var dive=level.StartCoroutine(travel.Dive(portal.transform));
                yield return null;
                Check(travel.Busy&&!body.simulated&&Time.timeScale==1,key+" actor-only lock");
                Check(!player.TryDash()&&!player.Skills.CanActivate,key+" input blocked during travel");
                var damage=player.CurrentHealth;player.TakeDamage(10);Check(player.CurrentHealth==damage,key+" travel damage immunity");
                yield return dive;
                Check(Time.time-begin>=1.8f&&Time.time-begin<2.05f,key+" dive timing");
                begin=Time.time;yield return travel.Eject(portal.transform,origin);
                Check(Time.time-begin>=1.8f&&Time.time-begin<2.05f,key+" eject timing");
                Check(!travel.Busy&&travel.HoldingPose&&body.simulated,key+" final pose and restored physics");
                var finalSprite=travel.Visual.sprite;yield return new WaitForSeconds(.12f);
                Check(travel.HoldingPose&&travel.Visual.sprite==finalSprite,key+" stationary pose retained");
                Capture(key);
                player.Move(Vector2.left);yield return null;yield return null;
                Check(!travel.HoldingPose,key+" movement releases pose");player.Move(Vector2.zero);
                Set(player,"characterBlueprint",blueprint);Object.Destroy(bp);
            }
            Object.Destroy(portal);
            bool fixedPose=true;
            for(int i=0;i<=100;i++)
            {
                var p=BloodClotTravel.Sample("Hyuki",true,i/100f,.6f,1);
                fixedPose&=p.frame==BloodClotTravel.HyukiCurledFrame&&p.angle==0;
            }
            Check(fixedPose,"Hyuki fixed upright ejection sprite throughout 101 sampled times");
            Check(BloodClotTravel.Sample("Hyuki",false,.1f,.6f,1).frame<2&&BloodClotTravel.Sample("Hyuki",false,.1f,.6f,1).lift==0,"Hyuki walks on ground first");
            var crouch=BloodClotTravel.Sample("Hyuki",false,.48f,.6f,1);
            Check(crouch.frame==2&&crouch.progress==.72f&&crouch.lift==0,"Hyuki bends knees at clot before takeoff");
            Check(BloodClotTravel.Sample("Hyuki",false,.65f,.6f,1).frame==3,"Hyuki unfolds on upward hop");
            var apex=BloodClotTravel.Sample("Hyuki",false,BloodClotTravel.HyukiApex,.6f,1);
            Check(apex.frame==BloodClotTravel.HyukiCurledFrame&&apex.lift>BloodClotTravel.Sample("Hyuki",false,.72f,.6f,1).lift&&apex.lift>BloodClotTravel.Sample("Hyuki",false,.76f,.6f,1).lift,"Hyuki curls exactly at jump maximum");
            Check(Mathf.Abs(BloodClotTravel.Frames("Hyuki")[4].bounds.size.y/BloodClotTravel.Frames("Hyuki")[0].bounds.size.y-.72f)<.001f,"Hyuki compact size preserved");
            Check(BloodClotTravel.Sample("Hyuki",true,.57f,.6f,1).lift>BloodClotTravel.Sample("Hyuki",true,.79f,.6f,1).lift,"Second rebound lower than first");
            player.transform.position=origin;body.position=origin;
            director.OpenEntrancePortal(origin+Vector3.right);
            var entry=Object.FindObjectsOfType<BloodClotMiniStagePortal>().First(p=>!p.Reserved);
            var charge=entry.GetComponent<BloodClotOvercharge>();
            typeof(BloodClotOvercharge).GetProperty("Kind").SetValue(charge,BloodClotOvercharge.Challenge.Stay);
            Check(charge.Begin(),"Begin overcharge");charge.Advance(charge.Target);
            Check(charge.Enhanced&&entry.GetComponentInChildren<SpriteRenderer>().sprite.name=="Overcharged blood clot","Overcharge swaps purple-vein art");
            var monster=level.EntityManager.LivingMonsters.FirstOrDefault(m=>m!=null&&m.HP>0);
            director.EnterMiniStageFromPortal(entry);
            Check(director.IsTransitioning&&entry!=null&&entry.Reserved,"Portal retained and reserved during dive");
            yield return new WaitForSeconds(.3f);
            Check(monster==null||!monster.IsFieldRuntimeSuspended,"Field monster remains active during dive");
            float limit=Time.realtimeSinceStartup+8;
            while(director.CurrentRoom==null&&Time.realtimeSinceStartup<limit)yield return null;
            Check(director.CurrentRoom!=null&&!director.CurrentRoom.RoomStarted,"Room mission not started during eject");
            Check(monster==null||!monster.IsFieldRuntimeSuspended,"Field monster remains active during eject");
            limit=Time.realtimeSinceStartup+8;
            while(director.IsTransitioning&&Time.realtimeSinceStartup<limit)yield return null;
            Check(director.CurrentRoom.RoomStarted&&director.IsInsideMiniStage,"Room begins only after eject");
            Check(player.transform.position.x<director.CurrentRoom.ReturnInteractable.transform.position.x,"Mini stage spawn left of clot");
            var room=director.CurrentRoom;Set(room,"optionalReturnUnlocked",true);room.ReturnInteractable.SetUnlocked(true);
            director.ReturnToFieldFromInteractable(room.ReturnInteractable);
            Check(director.IsTransitioning,"Return uses dive");
            limit=Time.realtimeSinceStartup+8;
            while(director.IsInsideMiniStage&&Time.realtimeSinceStartup<limit)yield return null;
            Check(travel.Busy&&(monster==null||!monster.IsFieldRuntimeSuspended),"Field enemies resume before return eject");
            limit=Time.realtimeSinceStartup+8;
            while(director.IsTransitioning&&Time.realtimeSinceStartup<limit)yield return null;
            Check(!MiniStageRuntimeState.IsInsideMiniStage&&!level.IsRunFlowPaused&&body.simulated,"Round trip releases state");
            Check(player.transform.position.x<origin.x+1,"Return spawn left of original clot");
            director.OpenEntrancePortal(player.transform.position+Vector3.right);
            entry=Object.FindObjectsOfType<BloodClotMiniStagePortal>().First(p=>!p.Reserved);
            director.EnterMiniStageFromPortal(entry);yield return new WaitForSeconds(.15f);
            director.enabled=false;yield return null;
            Check(!travel.Busy&&body.simulated&&!director.IsTransitioning&&!entry.Reserved,"Cancel restores physics, portal and transition");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
