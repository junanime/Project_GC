#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class BossContactSmoke
    {
        const string Key="BossContactSmoke", Output="Library/BossContactProof";
        static bool started; static double deadline;
        [Serializable] sealed class Pref { public string key,kind,text; public bool had; public int value; public float real; }
        [Serializable] sealed class Backup { public Pref[] prefs; public string settings,character,transfer; public string[] carry,selected; public float scale; public bool background; }
        static BossContactSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output); RestoreProfile();
            var keys=(IEnumerable<string>)typeof(ProgressPrefs).GetMethod("LegacyKeys",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            var backup=new Backup {
                prefs=keys.Concat(new[]{GamePreferences.SaveKey,"Apothecary.Volume","Apothecary.ReducedMotion"}).Distinct().Select(k=>{
                    string kind=k=="EquippedRelicId"||k==GamePreferences.SaveKey?"string":k=="Apothecary.Volume"?"float":"int";
                    return new Pref{key=k,kind=kind,had=PlayerPrefs.HasKey(k),text=kind=="string"?PlayerPrefs.GetString(k):"",value=kind=="int"?PlayerPrefs.GetInt(k):0,real=kind=="float"?PlayerPrefs.GetFloat(k):0};}).ToArray(),
                settings=JsonUtility.ToJson(GamePreferences.Current),character=AssetDatabase.GetAssetPath(CrossSceneData.CharacterBlueprint),
                transfer=CrossSceneData.PendingRunSceneTransfer==null?"":JsonUtility.ToJson(CrossSceneData.PendingRunSceneTransfer),
                carry=(CrossSceneData.StartingLobbyItems??Array.Empty<MerchantItemBlueprint>()).Select(AssetDatabase.GetAssetPath).ToArray(),
                selected=LobbyLoadoutData.SelectedCarryItems.Select(AssetDatabase.GetAssetPath).ToArray(),scale=Time.timeScale,background=Application.runInBackground };
            File.WriteAllText(Output+"/backup.json",JsonUtility.ToJson(backup,true));
            File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/samples.csv","boss,case,pickup_radius,hp_before,hp_after,damage_counter_delta\n");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key+"Checks",0);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();LobbyLoadoutData.Clear();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void RestoreProfile()
        {
            string path=Output+"/backup.json";if(!File.Exists(path))return;
            var b=JsonUtility.FromJson<Backup>(File.ReadAllText(path));
            foreach(var p in b.prefs)if(!p.had)PlayerPrefs.DeleteKey(p.key);else if(p.kind=="string")PlayerPrefs.SetString(p.key,p.text);else if(p.kind=="float")PlayerPrefs.SetFloat(p.key,p.real);else PlayerPrefs.SetInt(p.key,p.value);
            PlayerPrefs.Save();GamePreferences.Apply(JsonUtility.FromJson<GamePreferenceValues>(b.settings),false,false);
            CrossSceneData.CharacterBlueprint=string.IsNullOrEmpty(b.character)?null:AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(b.character);
            CrossSceneData.StartingLobbyItems=(b.carry??Array.Empty<string>()).Select(AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>).Where(x=>x!=null).ToArray();
            CrossSceneData.PendingRunSceneTransfer=string.IsNullOrEmpty(b.transfer)?null:JsonUtility.FromJson<RunSceneTransferSnapshot>(b.transfer);
            var selected=(List<MerchantItemBlueprint>)typeof(LobbyLoadoutData).GetField("selectedCarryItems",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            selected.Clear();selected.AddRange((b.selected??Array.Empty<string>()).Select(AssetDatabase.LoadAssetAtPath<MerchantItemBlueprint>).Where(x=>x!=null));
            Time.timeScale=b.scale;Application.runInBackground=b.background;File.Copy(path,Output+"/restored.json",true);File.Delete(path);
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",message+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false)){
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                RestoreProfile();bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+SessionState.GetInt(Key+"Checks",0));EditorApplication.Exit(failed?1:0);return;}
            if(!EditorApplication.isPlaying)return;
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Exercise(level));
        }
        static void Require(bool ok,string message){if(!ok)throw new Exception("CONTACT_SETUP "+message);}
        static void Expect(bool ok,string message){SessionState.SetInt(Key+"Checks",SessionState.GetInt(Key+"Checks",0)+1);File.AppendAllText(Output+"/checks.txt",(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)SessionState.SetBool(Key+"Failed",true);}
        static void Set(object o,string name,object value)=>ColonContentSmoke.Set(o,name,value);
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn||b is ConstipationKingDirector||b is ColonBossDirector){b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
        }
        static IEnumerator Exercise(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false,false);Application.runInBackground=true;
            RunScore.Exclude("Actual physics boss contact/pickup exclusion regression");Silence(level);
            yield return ColonContentSmoke.Enter(level);Silence(level);Time.timeScale=1;level.SetRunFlowPaused(false);
            var player=level.PlayerCharacter;player.Move(Vector2.zero);player.Skills.EndRun();player.Relics.Restore(new RelicRunState());OctoberItemRuntime.Get(player).ResetForStage();Set(player,"hasShield",false);
            var pickup=player.CollectableCollider as CircleCollider2D;Require(pickup!=null,"Actual player pickup circle exists");pickup.enabled=false;
            var body=player.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c!=pickup&&c.enabled&&c.gameObject.layer==LayerMask.NameToLayer("Player Full"));
            Require(body!=null,"Actual PlayerFull hurtbox exists");
            var map=FiniteStomachMap.Instance;Vector2 arena=map.ClampToFloor(map.UVToWorld(new Vector2(.3f,.25f)),8);
            Require(map.FloorClearance(arena,true)>6,"Actual Stage2 open floor fixture");
            player.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            foreach(bool king in new[]{true,false})
            {
                Set(player,"isInvincible",true);ColonContentSmoke.Position(player,arena+Vector2.right*5);
                ConstipationKingRuntime k=null;ColonBossRuntime t=null;MonoBehaviour boss;
                if(king){k=ConstipationKingRuntime.Spawn(level,arena);k.AutoPatterns=false;boss=k;}else{t=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,arena,null);t.AutoPatterns=false;boss=t;}
                float until=Time.realtimeSinceStartup+20;while((king?k.Arriving:t.Arriving)&&Time.realtimeSinceStartup<until)yield return null;
                Require(!(king?k.Arriving:t.Arriving),"Actual boss arrival completes");
                // Unity sends trigger messages to disabled behaviours. Freeze AI only; the live
                // Rigidbody2D/colliders remain simulated. The positive body control below proves delivery.
                boss.enabled=false;boss.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
                var hit=boss.GetComponent<Collider2D>();Require(hit!=null&&hit.enabled,"Actual boss hitbox active");
                Vector2 far=default;bool placed=false;
                for(int i=0;i<32;i++){
                    Vector2 p=(Vector2)hit.bounds.center+FoodBossRuntime.Dir(i*11.25f)*(hit.bounds.extents.magnitude+2);
                    if(map.FloorClearance(p,true)<.8f||!ColonAttackWall.Clear(hit.bounds.center,p))continue;
                    ColonContentSmoke.Position(player,p);Physics2D.SyncTransforms();
                    if(player.GetComponentsInChildren<Collider2D>().Where(c=>c!=pickup&&c.enabled).Any(c=>hit.Distance(c).isOverlapped))continue;
                    far=p;placed=true;break;}
                Require(placed,"Hurtboxes all outside boss while LOS remains clear");
                float radius=(Vector2.Distance(pickup.transform.TransformPoint(pickup.offset),hit.bounds.center)+hit.bounds.extents.magnitude+.3f)/Mathf.Max(.01f,Mathf.Abs(pickup.transform.lossyScale.x));
                for(int mode=0;mode<3;mode++){
                    pickup.enabled=mode!=0;pickup.radius=radius*(mode==2?1.5f:1);
                    Physics2D.SyncTransforms();Require(!hit.Distance(body).isOverlapped,"PlayerFull remains outside");
                    if(mode!=0)Require(hit.Distance(pickup).isOverlapped,"Only pickup circle overlaps actual boss");
                    player.GainHealth(player.MaxHealth);Set(player,"isInvincible",false);if(king)Set(k,"nextHit",0f);else Set(t,"nextContact",0f);
                    var stats=Object.FindObjectOfType<StatsManager>();float hp=player.CurrentHealth,taken=stats.DamageTaken;
                    yield return new WaitForSeconds(.8f);
                    string label=mode==0?"pickup-disabled":mode==1?"pickup-only":"larger-pickup-only";
                    File.AppendAllText(Output+"/samples.csv",$"{(king?"King":"Turtle")},{label},{pickup.radius},{hp},{player.CurrentHealth},{stats.DamageTaken-taken}\n");
                    Expect(Mathf.Approximately(hp,player.CurrentHealth)&&Mathf.Approximately(taken,stats.DamageTaken),(king?"King":"Turtle")+" no damage for "+label);
                }
                // Shrink/disable the collection trigger and overlap the genuine player body.
                pickup.enabled=false;ColonContentSmoke.Position(player,far);Physics2D.SyncTransforms();
                Vector2 bodyOffset=(Vector2)body.bounds.center-(Vector2)player.transform.position;
                ColonContentSmoke.Position(player,(Vector2)hit.bounds.center-bodyOffset);Physics2D.SyncTransforms();
                Require(hit.Distance(body).isOverlapped,"Positive control overlaps actual PlayerFull");
                player.GainHealth(player.MaxHealth);Set(player,"isInvincible",false);if(king)Set(k,"nextHit",0f);else Set(t,"nextContact",0f);
                float before=player.CurrentHealth;yield return new WaitForSeconds(.8f);
                Expect(player.CurrentHealth<before,(king?"King":"Turtle")+" real hurtbox contact still damages");
                Set(player,"isInvincible",true);Object.Destroy(boss.gameObject);yield return null;
            }
            Expect(RunScore.Current!=null&&!string.IsNullOrEmpty(RunScore.Current.Exclusion),"Contact QA remains excluded from ranking");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
#endif
