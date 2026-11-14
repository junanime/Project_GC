#if UNITY_EDITOR
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
    public static class StageDirectEntrySmoke
    {
        const string Key="StageDirectEntrySmoke",Output="Library/StageDirectEntryProof";
        static bool started;static double deadline;static int checks;
        [Serializable]class Entry{public string id;}
        [Serializable]class Catalog{public Entry[] entries;}
        [Serializable]class Pref{public string key;public bool had;public int value;}
        [Serializable]class Backup{public Pref[] prefs;}
        static StageDirectEntrySmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);Restore();checks=0;
            File.WriteAllText(Output+"/result.txt","RUNNING");
            File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/patterns.csv","kind,index,seconds,completed,health,width,height\n");
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var fixtureNeedles=new[]{"WindNeedle","Homing","AcupunctureFormation"};
            var keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{CapsuleDoctorDialogue.SeenKey,"Coins","LobbySilverCoins"})
                .Concat(fixtureNeedles.SelectMany(n=>new[]{"LobbyUnlock.Needle."+n,"October.NeedleEnabled."+n})).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            LobbyLoadoutData.Clear();CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+1800;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.timeScale=1;SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Colon boss integration QA");Object.DontDestroyOnLoad(host);
                host.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Checks());
            }
        }
        static void Restore()
        {
            string path=Output+"/prefs.json";if(!File.Exists(path))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)
                if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(path);
        }
        static void Check(bool value,string message)
        {
            if(!value)throw new Exception("COLON_BOSS_FAIL "+message);
            checks++;File.AppendAllText(Output+"/checks.txt",message+"\n");Debug.Log("COLON_BOSS_PASS "+message);
        }
        static FieldInfo Field(object o,string name)
        {
            for(var t=o.GetType();t!=null;t=t.BaseType)
            {var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new MissingFieldException(o.GetType().Name,name);
        }
        static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static void Position(Character p,Vector2 at)=>ColonContentSmoke.Position(p,at);
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn)
                {b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())
                if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
        }
        static void ClearProjectiles()
        {
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
        }
        static Bounds VisibleBounds(ColonBossRuntime boss)
        {
            var renderers=boss.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null&&r.color.a>.01f).ToArray();
            if(renderers.Length==0)return new Bounds(boss.transform.position,Vector3.zero);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        static UnityEngine.UI.Button Button(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<UnityEngine.UI.Button>().Last(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text==text));
        static IEnumerator WaitField(int stage)
        {
            float until=Time.realtimeSinceStartup+180;
            while(Time.realtimeSinceStartup<until)
            {
                var level=Object.FindObjectOfType<LevelManager>();
                if(level!=null&&level.InitialFieldObjectsReady&&!StageEntryLoading.IsLoading&&StageProgression.Ensure(level).StageNumber==stage&&!StageProgression.Ensure(level).Travelling)yield break;
                yield return null;
            }
            Check(false,"entry timeout stage "+stage);
        }
        static IEnumerator Checks()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");yield return null;
            var ui=ApothecaryUI.Instance;ui.Show("prepare");yield return null;
            var carry=ui.Config.items.First(i=>i.octoberId>0&&ItemOfferPolicy.CanBuyInLobby(i));
            Check(LobbyLoadoutData.TryBuyAndEquip(carry,0,out _),"fixture equips one real preparation item");
            ui.Show("prepare");yield return null;Button("출전하기").onClick.Invoke();yield return null;
            Check(!StageEntryLoading.IsLoading&&Button("스테이지 1 · 위장").interactable&&Button("스테이지 2 · 대장").interactable,"departure opens stage picker without loading");
            Capture("stage-picker");Button("돌아가기").onClick.Invoke();yield return null;
            Check(LobbyLoadoutData.SelectedCarryItems.Contains(carry),"cancel preserves selected preparation item");
            StageProgression.PreviewWebDemoEnding=true;ui.StartRun();yield return null;
            Check(!Button("스테이지 2 · 대장").interactable&&!StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint,null,2),"stage-one-only demo blocks stage-two direct entry");
            Button("돌아가기").onClick.Invoke();StageProgression.PreviewWebDemoEnding=false;yield return null;
            ui.StartRun();yield return null;Button("스테이지 1 · 위장").onClick.Invoke();
            Check(!StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"double launch is rejected while loading");
            yield return WaitField(1);var stageOne=Object.FindObjectOfType<LevelManager>();
            TutorialGuide.Instance?.Close();
            Check(!StageProgression.Ensure(stageOne).IsDirectColonStart&&ColonMonsterRoster.Active==null,"stage-one selection uses original profile");
            Check(string.IsNullOrEmpty(RunScore.Current.Exclusion),"stage-one choice remains ranking eligible");RunScore.Exclude("direct entry regression stage-one baseline");
            var character=stageOne.PlayerCharacter;var baseline=stageOne.CurrentLevelBlueprint;
            float hp=character.MaxHealth,damage=character.DamageMultiplier,attack=character.AttackSpeedMultiplier;
            var equipment=character.GetComponent<OctoberItemRuntime>();
            Check(equipment!=null&&equipment.Has(carry.octoberId),"stage-one receives selected carry item");
            var op=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(0);while(!op.isDone)yield return null;yield return null;
            intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");yield return null;
            Check(LobbyLoadoutData.TryBuyAndEquip(carry,0,out _),"same loadout prepared for direct comparison");
            ui=ApothecaryUI.Instance;ui.Show("prepare");yield return null;Button("출전하기").onClick.Invoke();yield return null;Button("스테이지 2 · 대장").onClick.Invoke();
            bool sawOpening=false,sawEject=false,sawSettle=false,paused=true;float until=Time.realtimeSinceStartup+180;
            while(Time.realtimeSinceStartup<until)
            {
                var candidate=Object.FindObjectOfType<LevelManager>();var flow=candidate==null?null:candidate.GetComponent<StageProgression>();
                if(flow!=null&&flow.StageNumber==2)
                {
                    if(flow.Travelling){paused&=candidate.IsRunFlowPaused&&candidate.CurrentLevelTime<.05f;sawOpening|=flow.TransitionPhase=="opening";sawEject|=flow.TransitionPhase=="eject";sawSettle|=flow.TransitionPhase=="settle";}
                    else if(!StageEntryLoading.IsLoading)break;
                }
                yield return null;
            }
            yield return WaitField(2);var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;
            TutorialGuide.Instance?.Close();if(TutorialGuide.Instance!=null)TutorialGuide.Instance.enabled=false;
            Check(sawOpening&&sawEject&&sawSettle&&paused,"direct entry reuses paused inlet-opening, ejection and one-second settle");
            Check(Mathf.Approximately(player.MaxHealth,hp)&&Mathf.Approximately(player.DamageMultiplier,damage)&&Mathf.Approximately(player.AttackSpeedMultiplier,attack),"direct entry retains same base stats and selected equipment effects");
            Check(player.GetComponent<OctoberItemRuntime>()!=null&&player.GetComponent<OctoberItemRuntime>().Has(carry.octoberId),"direct entry retains preparation carry item");
            Check(StageProgression.Ensure(level).IsDirectColonStart&&ColonMonsterRoster.Active.DirectStart,"direct profile is active only on this run");
            Check(!string.IsNullOrEmpty(RunScore.Current.Exclusion),"temporary direct run cannot submit to normal ranking");
            Check(level.GetComponent<FieldObjectLayout>().SpawnedObjects.Count==86&&level.GetComponent<ColonBossDirector>().Habitats.Count==6,"direct entry loading completes all 86 props and six habitats");
            Check(level.EntityManager.LivingMonsters.OfType<BloodClotMonster>().Count()==14,"fourteen clot entrances present in direct entry");
            var roster=ColonMonsterRoster.Active;var flat=baseline.monsters.SelectMany(p=>p.monsterBlueprints).ToArray();
            File.WriteAllText(Output+"/balance.csv","kind,sourceHP,directHP,contact,speed\n");
            for(int i=0;i<8;i++)
            {
                var source=flat[ColonMonsterRoster.DirectNormalSources[i]];var entry=roster.Normals[i];float ratio=entry.hp/source.hp;
                Check(Mathf.Approximately(ratio,1.10f),"normal tier "+i+" HP is stage-one baseline +10%: "+entry.hp);
                Check(entry.blueprint.atk==source.atk&&entry.blueprint.movespeed==source.movespeed,"normal tier "+i+" preserves stage-one speed and contact damage");
                Check(entry.blueprint.gemLootTable.Equals(source.gemLootTable),"normal tier "+i+" uses basic-start experience rewards");
                var elite=(EliteMonsterBlueprint)roster.Elites[i].blueprint;
                Check(elite.hp==entry.hp&&elite.hpMultiplier>=2,"elite tier "+i+" uses matching direct HP baseline");
                File.AppendAllText(Output+"/balance.csv",entry.name+","+source.hp+","+entry.hp+","+entry.blueprint.atk+","+entry.speed+"\n");
            }
            File.WriteAllText(Output+"/time-balance.csv","seconds,sourceMeanHP,directMeanHP,hpRatio,spawnRatio\n");
            for(int i=0;i<61;i++)
            {
                float t=i/60f;float ratio=roster.Level.monsterSpawnTable.GetSpawnRate(t)/baseline.monsterSpawnTable.GetSpawnRate(t);
                float sourceMean=0,directMean=0;
                for(int n=0;n<8;n++)
                {
                    float sourceChance=baseline.monsterSpawnTable.GetProbability(t,ColonMonsterRoster.DirectNormalSources[n]);
                    float directChance=roster.Level.monsterSpawnTable.GetProbability(t,roster.NormalFlatStart+n);
                    Check(Mathf.Abs(sourceChance-directChance)<.001f,"tier mix matches stage-one clock t="+t.ToString("F2")+" tier="+n);
                    Check(roster.EliteUnlocked(n,t*baseline.levelTime)==(directChance>0),"elite unlock follows direct time table");
                    sourceMean+=sourceChance*flat[ColonMonsterRoster.DirectNormalSources[n]].hp;directMean+=directChance*roster.Normals[n].hp;
                }
                Check(Mathf.Approximately(directMean/sourceMean,1.10f),"same-time mean HP is baseline +10%");
                Check(Mathf.Abs((directMean/sourceMean)*ratio-1.188f)<.001f,"nominal HP throughput rises 18.8%, independent of entry route");
                File.AppendAllText(Output+"/time-balance.csv",(t*900)+","+sourceMean+","+directMean+","+(directMean/sourceMean)+","+ratio+"\n");
                Check(Mathf.Approximately(ratio,1.08f),"spawn-rate ratio +8% at "+(t*900).ToString("F0")+"s: "+ratio.ToString("F3"));
            }
            Check(Mathf.Approximately(roster.Level.monsterSpawnTable.GetSpawnRate(0),.756f),"initial spawn rate is 0.756 per second");
            Check(flat[0].hp==25&&flat[32].hp==260,"serialized stage-one monster values remain unchanged");
            Check(ColonBossDefinition.Get(ColonBossKind.Flamingo).Health==3000,"shared mini-boss baseline is 3000");
            float directSpecials=0;for(int i=0;i<20000;i++)directSpecials+=ColonSpecialDirector.LegacyCount(2,false,false);
            Check(Mathf.Abs(directSpecials/20000-2f)<.03f,"legacy special schedule uses baseline count before new additions");
            Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            foreach(var behaviour in Object.FindObjectsOfType<MonoBehaviour>())if(behaviour is StageEventDirector||behaviour is TimedSpecialMonsterSpawner||behaviour is AcidToadSpawn||behaviour is NPCSpawner||behaviour is SnailBossDebugSpawn){behaviour.enabled=false;behaviour.StopAllCoroutines();}
            var map=FiniteStomachMap.Instance;Position(player,map.ClampToFloor(map.UVToWorld(new Vector2(.72f,.68f)),5));
            float clock=level.CurrentLevelTime;int initialSpawns=level.NormalMonstersSpawned;yield return new WaitForSeconds(12);
            Check(level.CurrentLevelTime>clock+10&&level.NormalMonstersSpawned>initialSpawns,"direct run clock and actual spawn loop operate");
            Capture("direct-stage2-play");level.enabled=false;
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            int kills=RunScore.Current.Data.normal;float combatStart=Time.time;
            for(int i=0;i<5;i++)level.EntityManager.SpawnMonster(roster.PoolIndex,FiniteStomachMap.SafeSpawn((Vector2)player.transform.position+new Vector2(1.5f+i*.12f,.4f-i*.2f),1),roster.Normals[0].blueprint,0,false);
            while(Time.time-combatStart<20&&RunScore.Current.Data.normal-kills<5)yield return null;
            int killed=RunScore.Current.Data.normal-kills;Check(killed>0,"unupgraded selected weapon kills early direct-stage normals: "+killed+" in "+(Time.time-combatStart).ToString("F1")+" seconds");
            File.WriteAllText(Output+"/combat.txt","Controlled stationary/invulnerable weapon check; not a human win-rate estimate. Early normal kills="+killed+" seconds="+(Time.time-combatStart));
            Silence(level);ClearProjectiles();
            foreach(var home in level.GetComponent<ColonBossDirector>().Habitats)
            {
                Position(player,map.ClampToFloor((Vector2)home.transform.position+Vector2.down*2.5f,1));yield return new WaitForSeconds(.2f);InteractionFocus.SelectTouched(home);home.TryInteract();yield return null;
                var boss=ColonBossRuntime.Active.FirstOrDefault(b=>b!=null&&!b.Dead&&b.Kind==home.Kind);Check(boss!=null,"direct habitat interaction creates "+home.Kind);boss.AutoPatterns=false;
                until=Time.time+25;while(boss.Arriving&&Time.time<until)yield return null;
                Check(!boss.Arriving&&boss.MaxHealth==3000,"direct mini-boss is chameleon HP x1.2: "+home.Kind);
                Check(boss.Definition.ContactDamage>=12&&boss.Definition.ContactDamage<=14&&boss.Definition.AttackDamage>=16&&boss.Definition.AttackDamage<=20,"direct pattern damage profile applied: "+home.Kind);
                Check(boss.Definition.Size==ColonBossDefinition.Get(home.Kind).Size,"difficulty does not change art size: "+home.Kind);
                int before=boss.CompletedPatterns;Check(boss.PlayPattern(0),"direct dash starts "+home.Kind);until=Time.time+35;while(boss.Busy&&Time.time<until)yield return null;
                Check(!boss.Busy&&boss.CompletedPatterns==before+1,"direct dash completes "+home.Kind);
                File.AppendAllText(Output+"/balance.csv",boss.Definition.Name+",2500,"+boss.MaxHealth+","+boss.Definition.ContactDamage+","+boss.Definition.MoveSpeed+"\n");
                foreach(var gem in Object.FindObjectsOfType<ExpGem>())gem.DissolveInAcidLake();
                foreach(var coin in Object.FindObjectsOfType<Coin>())coin.DissolveInAcidLake();yield return null;
                int scored=RunScore.Current.Data.miniBoss;boss.TakeDamage(100000);yield return new WaitForSeconds(2);
                int xp=Object.FindObjectsOfType<ExpGem>().Sum(g=>(int)(GemType)Field(g,"gemType").GetValue(g));
                int gold=Object.FindObjectsOfType<Coin>().Sum(c=>(int)c.CoinType);
                Check(xp==60&&gold==40,"actual direct mini-boss rewards total 60 XP / 40 gold: "+home.Kind);
                Check(RunScore.Current.Data.miniBoss==scored+1,"direct mini-boss scores once locally "+home.Kind);
            }
            Check(!string.IsNullOrEmpty(RunScore.Current.Exclusion)&&!level.IsLevelEnded,"direct score remains local; optional kills do not end stage");
            op=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(0);while(!op.isDone)yield return null;yield return null;
            intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");yield return null;
            Check(ColonMonsterRoster.Active==null,"direct profile destroyed on return to main menu");
            Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"fresh normal run launches after direct run");yield return WaitField(1);
            level=Object.FindObjectOfType<LevelManager>();RunScore.Exclude("normal-after-direct regression");TutorialGuide.Instance?.Close();Silence(level);Set(level.PlayerCharacter,"isInvincible",true);
            yield return ColonContentSmoke.Enter(level);Silence(level);
            Check(!StageProgression.Ensure(level).IsDirectColonStart&&!ColonMonsterRoster.Active.DirectStart,"normal capsule transition does not inherit direct mode");
            Check(ColonMonsterRoster.Active.Normals[0].hp==27.5f&&Mathf.Approximately(level.CurrentLevelBlueprint.monsterSpawnTable.GetSpawnRate(0),.756f),"normal transition shares direct-entry HP and spawn rate");
            foreach(ColonBossKind kind in Enum.GetValues(typeof(ColonBossKind)))Check(ReferenceEquals(ColonBossDefinition.Get(kind),ColonBossDefinition.ForCurrentRun(kind)),"normal boss stats restored after direct run "+kind);
            SessionState.SetBool(Key+"Done",true);
        }
        static void Capture(string name)
        {
            var camera=Camera.main;if(camera==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();
            var orders=overlays.Select(c=>c.sortingOrder).ToArray();var overrideSort=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=overrideSort[i];}
                camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
