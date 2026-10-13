using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class DiscoveryUpdateSmoke
    {
        const string Key="DiscoveryUpdateSmoke",Output="Library/DiscoveryProof";
        static bool started;static double deadline;
        [Serializable] class Catalog { public TutorialGuide.Entry[] entries; }
        [Serializable] class Saved { public string key;public bool exists;public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static DiscoveryUpdateSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Vampire.Editor.DiscoveryArtInstaller.Install();
            Directory.CreateDirectory(Output);
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"});
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                var backup=JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs-backup.json"));
                foreach(var s in backup.values){if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}PlayerPrefs.Save();
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("DISCOVERY_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!started&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null&&level.CurrentLevelTime>.1f){started=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("DISCOVERY_FAIL "+text);Debug.Log("DISCOVERY_PASS "+text);}
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception(n);}
        static object Get(object o,string n)=>Field(o,n).GetValue(o);
        static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static void Capture(string name)
        {
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"discovery-"+name+".png"});
            File.Copy(Path.GetFullPath("../work/discovery-"+name+".png"),Output+"/"+name+".png",true);
        }
        static IEnumerator AwaitGuide(string id)
        {
            for(int i=0;i<120&&!TutorialGuide.IsOpen;i++)yield return null;
            Check(TutorialGuide.IsOpen&&PlayerPrefs.GetInt(TutorialGuide.SeenKey(id))==1,"First appearance "+id);
            Check(Time.timeScale==0,"Guide pauses game "+id);
            yield return new WaitForSecondsRealtime(.65f);Capture(id.Replace('/','-'));
            TutorialGuide.Instance.Close();yield return null;
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            Object.FindObjectOfType<StageEventDirector>().enabled=false;Object.FindObjectOfType<TimedSpecialMonsterSpawner>().enabled=false;
            Object.FindObjectOfType<AcidToadSpawn>().enabled=false;
            var weapon=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());weapon.enabled=false;weapon.StopAllCoroutines();
            new TutorialGuideTests().EveryFirstDiscoveryHasItsOwnPlayableClip();Check(true,"43 catalog entries validated");
            var art=DiscoveryPreviewArt.Load();Check(art!=null&&art.health!=null&&art.potion!=null&&art.magnet!=null&&art.coin!=null&&art.gem!=null,"Actual item art references");
            var pools=level.CurrentLevelBlueprint.monsters;Vector2 origin=level.PlayerCharacter.transform.position;
            int index=Array.FindIndex(pools,p=>p.monstersPrefab.GetComponent<AcidToadMonster>()!=null);
            PlayerPrefs.DeleteKey(TutorialGuide.SeenKey("monster/acid-toad"));
            var toad=(AcidToadMonster)level.EntityManager.SpawnMonster(index,origin+Vector2.right*4,pools[index].monsterBlueprints[0],0,false);toad.AutoPatterns=false;
            yield return AwaitGuide("monster/acid-toad");
            TutorialGuide.QueueMonster(pools[index].monsterBlueprints[0],toad);yield return null;yield return null;
            Check(!TutorialGuide.IsOpen,"Toad rediscovery suppressed");
            var marker=toad.GetComponent<MapMarker>();Check(marker.AlwaysVisible&&marker.Icon==AcidToadArt.Frame("ToadHead",0),"Toad map icon and global visibility");
            toad.transform.position=origin+Vector2.right*35;yield return new WaitForSeconds(.65f);
            Check(Object.FindObjectsOfType<RectTransform>().Any(r=>r.name=="Toad direction bubble"),"Offscreen toad compass visible");Capture("toad-compass");
            toad.transform.position=origin+Vector2.right*4;yield return new WaitForSeconds(.15f);
            Check(!Object.FindObjectsOfType<RectTransform>().Any(r=>r.name=="Toad direction bubble"),"Onscreen compass hidden");
            index=Array.FindIndex(pools,p=>p.monstersPrefab.GetComponent<TreasureRunnerMonster>()!=null);
            PlayerPrefs.DeleteKey(TutorialGuide.SeenKey("monster/treasure-runner"));
            var squirrel=level.EntityManager.SpawnMonster(index,origin+Vector2.left*5,pools[index].monsterBlueprints[0],0,false);
            yield return AwaitGuide("monster/treasure-runner");
            TutorialGuide.QueueMonster(pools[index].monsterBlueprints[0],squirrel);yield return null;yield return null;Check(!TutorialGuide.IsOpen,"Squirrel rediscovery suppressed");
            foreach(string name in new[]{"Health","Red Potion","Magnet"})
            {
                var item=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Chest/"+name+".prefab")).GetComponent<Collectable>();
                string id="item/"+TutorialGuide.ItemId(item);PlayerPrefs.DeleteKey(TutorialGuide.SeenKey(id));
                item.transform.position=origin+Vector2.up*7;item.Init(level.EntityManager,level.PlayerCharacter);item.Setup(false);
                yield return AwaitGuide(id);TutorialGuide.QueueItem(item);yield return null;yield return null;Check(!TutorialGuide.IsOpen,"Repeated item suppressed "+id);
            if(item is RedPotion){item.Use();Check(level.SpawnPotionRemaining==30,"Potion activates spawn buff");level.ActivateSpawnPotion();Check(level.SpawnPotionRemaining==30&&LevelManager.SpawnPotionMultiplier==1.5f,"Potion refresh without stacking");}
                else Object.Destroy(item.gameObject);
            }
            var bomb=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Chest/Bomb.prefab");Check(TutorialGuide.ItemId(bomb.GetComponent<Collectable>())==null,"Bomb excluded");
            int elitePool=Array.FindIndex(pools,p=>p.monsterBlueprints.Any(b=>b is EliteMonsterBlueprint));
            Check(elitePool>=0,"Actual elite pool exists");
            var elite=level.EntityManager.SpawnMonster(elitePool,origin+Vector2.up*5,pools[elitePool].monsterBlueprints.First(b=>b is EliteMonsterBlueprint),0,false);
            yield return null;
            foreach(var m in Object.FindObjectsOfType<Monster>())
            {
                if(m.BodyRenderer==null||!m.BodyRenderer.enabled||m.BodyHitbox==null)continue;
                m.FitVisibleBody();var expected=VisibleBodyGeometry.InSpace(m.BodyRenderer,m.BodyHitbox.transform);
                Check(Vector2.Distance(m.BodyHitbox.size,expected.size)<.001f,"Local body fit "+m.name);
            }
            // Regression for double scaling on elites: doubling root scale must only double world bounds.
            var oldScale=toad.transform.localScale;toad.FitVisibleBody();Vector2 before=toad.BodyHitbox.bounds.size;
            toad.transform.localScale=oldScale*2;toad.FitVisibleBody();Physics2D.SyncTransforms();
            Check(Vector2.Distance(toad.BodyHitbox.bounds.size,before*2)<.01f,"No double scale in elite hitbox");toad.transform.localScale=oldScale;Physics2D.SyncTransforms();
            var projectile=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Projectiles/SyringeProjectile.prefab")).GetComponent<SyringeProjectile>();projectile.enabled=false;
            Set(projectile,"direction",Vector2.right);Set(projectile,"enableStuckNeedleVisual",true);
            var needleArt=(SpriteRenderer)Get(projectile,"projectileSpriteRenderer");
            typeof(SyringeProjectile).GetMethod("ApplyVisualRotationToDirection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(projectile,new object[]{Vector2.right});
            var embed=typeof(SyringeProjectile).GetMethod("TryCreateStuckNeedleVisual",BindingFlags.Instance|BindingFlags.NonPublic);
            Vector3 impact=new Vector3(toad.BodyHitbox.bounds.min.x-.02f,toad.BodyHitbox.bounds.center.y,toad.transform.position.z);
            embed.Invoke(projectile,new object[]{toad,toad.BodyHitbox,impact,needleArt.transform.rotation,needleArt.transform.lossyScale});
            yield return null;var embedded=toad.GetComponentInChildren<StuckNeedleVisual>();Check(embedded!=null,"Needle embeds on toad");
            var render=embedded.GetComponent<SpriteRenderer>();Check(render.sortingLayerID==toad.BodyRenderer.sortingLayerID&&render.sortingOrder==toad.BodyRenderer.sortingOrder-1,"Needle behind body in same layer");
            Check(embedded.transform.parent==toad.BodyRenderer.transform,"Needle follows animated body");
            var tipVertices=render.sprite.vertices;float tip=tipVertices.Max(v=>render.transform.TransformPoint(v).x);
            Check(tip>toad.BodyHitbox.bounds.min.x,"Visible needle tip enters body boundary");
            CaptureWorld("toad-embedded",toad.BodyHitbox.bounds.center,2.5f);
            impact=new Vector3(elite.BodyHitbox.bounds.min.x-.02f,elite.BodyHitbox.bounds.center.y,elite.transform.position.z);
            embed.Invoke(projectile,new object[]{elite,elite.BodyHitbox,impact,needleArt.transform.rotation,needleArt.transform.lossyScale});
            yield return null;Check(elite.GetComponentInChildren<StuckNeedleVisual>()!=null,"Actual elite supports surface embedding");CaptureWorld("elite-embedded",elite.BodyHitbox.bounds.center,2.2f);
            var snail=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Boss/RollCakeSnail.prefab")).GetComponent<SnailBossRuntime>();
            snail.transform.position=origin+Vector2.down*5;snail.Initialize(level.PlayerCharacter);snail.AutoPatterns=false;yield return new WaitForFixedUpdate();
            var box=snail.GetComponent<BoxCollider2D>();Check(box.size.x>0&&box.size.x<6,"Snail fitted body collider");
            embed.Invoke(projectile,new object[]{snail,box,box.bounds.center+Vector3.left*box.bounds.extents.x,needleArt.transform.rotation,needleArt.transform.lossyScale});
            yield return null;Check(snail.GetComponentInChildren<StuckNeedleVisual>()!=null,"Snail boss now supports embedded needles");
            Object.Destroy(snail.gameObject);Object.Destroy(projectile.gameObject);
            var dialog=level.EntityManager.AbilitySelectionDialog;dialog.OpenLegendary();yield return new WaitForSecondsRealtime(.8f);
            Check(dialog.MenuOpen&&dialog.ScrollRevealPending,"All noble cards start sealed");Capture("noble-sealed");
            var cards=Object.FindObjectsOfType<AbilityCard>().Where(c=>c.gameObject.activeInHierarchy).ToArray();
            Check(cards.Length==3&&cards.All(c=>c.transform.Find("Noble scroll seal")!=null),"Three scroll covers");
            cards[0].Selected();Check(dialog.MenuOpen&&dialog.ScrollRevealPending,"First click reveals, does not select");
            yield return new WaitForSecondsRealtime(.4f);Capture("noble-rolling");
            yield return new WaitForSecondsRealtime(.6f);Check(dialog.CanSelectRevealedCard,"All cards selectable after reveal");Capture("noble-revealed");
            cards[0].Selected();Check(!dialog.MenuOpen,"Second click chooses reward");
            dialog.Open(false);yield return new WaitForSecondsRealtime(.5f);Check(!dialog.ScrollRevealPending,"Normal augments skip scroll reveal");dialog.Close();
            toad.TakeDamage(100000);yield return new WaitForSeconds(.5f);Check(!marker.enabled,"Dead miniboss removed from map");
            SessionState.SetBool(Key+"Done",true);
        }
        static void CaptureWorld(string name,Vector3 center,float size)
        {
            var go=new GameObject("Body proof camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;
            camera.transform.position=new Vector3(center.x,center.y,-20);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.16f,.18f);
            var rt=RenderTexture.GetTemporary(1280,720,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(pixels);Object.Destroy(go);
        }
    }
}
