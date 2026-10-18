using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ChestLootSmoke
    {
        const string Key="ChestLootSmoke",Output="Library/ChestLootProof";
        static double deadline;static bool started;
        [Serializable] class Entry{public string id;}
        [Serializable] class Catalog{public Entry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}
        [Serializable] class Backup{public Saved[] values;}
        static ChestLootSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if(File.Exists(Output+"/errors.txt"))File.Delete(Output+"/errors.txt");

            if(File.Exists(Output+"/prefs.json"))Restore();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Restore()
        {
            foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs.json")).values)
            {if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}
            PlayerPrefs.Save();File.Delete(Output+"/prefs.json");
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+360;started=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
            {File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");Debug.Log("[ChestLoot] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!started&&EditorApplication.isPlaying&&level!=null&&level.PlayerCharacter!=null&&level.CurrentLevelTime>.1f)
            {started=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception("[ChestLoot] FAIL "+message);Debug.Log("[ChestLoot] PASS "+message);}
        static FieldInfo Field(object obj,string name)
        {for(var t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new MissingFieldException(name);}
        static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
        static object Call(object o,string name,params object[] args)
        {for(var t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args);}throw new MissingMethodException(name);}
        static Button Button(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>()?.text==text);
        static IEnumerator Capture(string name){yield return null;ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return null;yield return null;}
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;Time.timeScale=1;Time.captureDeltaTime=1f/60;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SupplyCapsuleDirector){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;var entities=level.EntityManager;entities.enabled=false;entities.StopAllCoroutines();
            foreach(var m in entities.LivingMonsters.ToArray())m.gameObject.SetActive(false);
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.gameObject.SetActive(false);
            var player=level.PlayerCharacter;Set(player,"isInvincible",true);
            var needle=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());needle.enabled=false;needle.StopAllCoroutines();
            var blueprint=AssetDatabase.LoadAssetAtPath<ChestBlueprint>("Assets/Blueprints/Chests/Default Chest.asset");
            Check(level.CurrentLevelBlueprint.chestBlueprint==blueprint,"Level 1 uses updated normal chest");
            var drops=blueprint.lootTable.lootTable;
            Check(Mathf.Abs(drops.Sum(d=>d.dropChance)-1f)<.00001f,"loot probabilities total 100 percent");
            var randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(417);
            var hits=new int[drops.Length];
            for(int i=0;i<100000;i++)hits[Array.IndexOf(drops,blueprint.lootTable.DropLootObject())]++;
            UnityEngine.Random.state=randomState;
            for(int i=0;i<drops.Length;i++)
                Check(Mathf.Abs(hits[i]/100000f-drops[i].dropChance)<.006f,"100000 draws: "+drops[i].item.name+" / "+drops[i].coinType+" = "+hits[i]/1000f+" percent");
            var origin=player.transform.position;var stats=Object.FindObjectOfType<StatsManager>();
            var bombSlot=entities.Inventory.Slots.Single(s=>s.IsBomb);
            var forced=Object.Instantiate(blueprint);
            // Exercise the production open/drop/physics/pickup path for every reward.
            foreach(var drop in drops)
            {
                player.transform.position=origin;player.CollectableCollider.enabled=true;Set(player,"autoCollectItems",false);
                Physics2D.SyncTransforms();
                var beforeObjects=new HashSet<Collectable>(Object.FindObjectsOfType<Collectable>());
                int beforeGold=stats.CoinsGained;
                var position=(Vector2)origin+Vector2.right*5;
                forced.lootTable=new LootTable<GameObject>{lootTable=new[]{new Loot<GameObject>{item=drop.item,coinType=drop.coinType,dropChance=1}}};
                var chest=entities.SpawnChest(forced,position);
                yield return new WaitForSeconds(.6f);chest.OpenChest();yield return new WaitForSeconds(.2f);
                var item=Object.FindObjectsOfType<Collectable>().Single(c=>!beforeObjects.Contains(c));
                var renderer=item.GetComponentInChildren<SpriteRenderer>();
                Check(renderer!=null&&renderer.sprite!=null&&renderer.enabled&&renderer.color.a>.9f,drop.item.name+" has visible drop sprite");
                Check(item.transform.position.y>position.y+.05f&&!item.IsBeingCollected&&!item.GetComponent<Collider2D>().enabled,drop.item.name+" pops upward before pickup unlocks");
                if(item is Bomb)yield return Capture("bomb-pop-out");
                Set(player,"autoCollectItems",true);
                yield return new WaitForSeconds(1.3f);
                Check(item.gameObject.activeSelf&&!item.IsBeingCollected&&Mathf.Abs(item.transform.position.y-position.y)<.03f,drop.item.name+" waits on ground outside pickup range, including auto-collect relic");
                if(item is Bomb)yield return Capture("bomb-on-ground");
                Set(player,"autoCollectItems",false);
                InventorySlot slot=null;entities.Inventory.TryGetInventorySlot(item,out slot);
                int beforeCount=slot!=null?slot.Count:0;
                Vector2 pickupCenter=player.CollectableCollider.bounds.center;
                player.transform.position+=(Vector3)((Vector2)item.transform.position-pickupCenter);
                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();yield return null;
                Check(item.IsBeingCollected,drop.item.name+" is collected automatically by pickup collider");
                // Follow every visible frame: destination must stay near the character, never travel to HUD.
                float timeout=Time.time+3;
                while(item.gameObject.activeSelf&&Time.time<timeout)
                {
                    Check(Vector2.Distance(item.transform.position,player.CenterTransform.position)<4f,drop.item.name+" pickup stays in world near character");
                    yield return null;
                }
                Check(!item.gameObject.activeSelf,drop.item.name+" pickup completes");
                if(slot!=null)
                {
                    Check(slot.Count==beforeCount+1,drop.item.name+" adds exactly one stored item without using it");
                    Check(Vector2.Distance(item.transform.position,player.CenterTransform.position)<.05f,drop.item.name+" finishes at character rather than inventory UI");
                }
                else Check(stats.CoinsGained-beforeGold==(int)drop.coinType,"gold reward grants "+(int)drop.coinType);
            }
            // Three overlapping bombs may reserve only two slots; full inventory leaves the third on ground.
            var bombPrefab=drops.Single(d=>d.item.GetComponent<Bomb>()!=null).item;
            var extra=new List<Bomb>();
            Set(player,"autoCollectItems",true);
            for(int i=0;i<2;i++)
            {
                var b=Object.Instantiate(bombPrefab).GetComponent<Bomb>();b.Init(entities,player);
                b.transform.position=player.CollectableCollider.bounds.center;b.Setup(true,false,true);extra.Add(b);
            }
            Check(extra.All(b=>!b.IsBeingCollected),"nearby rewards cannot skip pop-out with auto-collect active");
            yield return new WaitForSeconds(2.5f);
            Check(bombSlot.Count==2&&extra.Count(b=>b.gameObject.activeSelf&&!b.IsBeingCollected)==1,"concurrent pickup reserves capacity and leaves excess bomb on ground");
            var remaining=extra.Single(b=>b.gameObject.activeSelf);
            bombSlot.ThrowBomb((Vector2)player.transform.position+Vector2.right*5);
            yield return new WaitForSeconds(1.5f);
            Check(bombSlot.Count==2&&!remaining.gameObject.activeSelf,"remaining item is picked up after freeing inventory capacity");
            // Unrelated pooled coins must retain normal auto-collection after a chest coin is recycled.
            player.transform.position=origin;
            var coin=entities.SpawnCoin((Vector2)origin+Vector2.right*15,CoinType.Bronze1,false);
            Check(coin.IsBeingCollected,"pooled coin resets chest range restriction for ordinary drops");
            Object.Destroy(forced);yield return Capture("collected-inventory");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}