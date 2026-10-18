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
    public static class BombItemSmoke
    {
        const string Key="BombItemSmoke",Output="Library/BombItemProof";
        static double deadline;static bool started;
        [Serializable] class Entry{public string id;}
        [Serializable] class Catalog{public Entry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}
        [Serializable] class Backup{public Saved[] values;}
        static BombItemSmoke(){if(SessionState.GetBool(Key,false))Attach();}
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
                File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");Debug.Log("[BombItem] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!started&&EditorApplication.isPlaying&&level!=null&&level.PlayerCharacter!=null&&level.CurrentLevelTime>.1f)
            {started=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception("[BombItem] FAIL "+message);Debug.Log("[BombItem] PASS "+message);}
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
            var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            var needle=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());needle.enabled=false;needle.StopAllCoroutines();
            var inventory=entities.Inventory;var slot=inventory.Slots.Single(s=>s.IsBomb);var aim=inventory.GetComponent<BombTargeting>();
            var ui=ApothecaryUI.Instance;var map=ExplorationMapSystem.Instance;
            var mini=Object.FindObjectsOfType<RectTransform>(true).First(r=>r.name=="MiniMapRoot").gameObject;
            Check(!mini.activeSelf,"desktop HUD minimap hidden");map.SetMiniMapVisible(true);Check(!mini.activeSelf,"legacy visibility cannot revive minimap");
            ui.OpenRunBook();yield return null;Check(map.FullMapTexture!=null&&ui.Page=="run","TAB map remains functional");ui.CloseRunBook();
            inventory.ApplyMobileLayout();yield return null;Check(!mini.activeSelf,"mobile inventory layout leaves minimap hidden");
            var dialog=Object.FindObjectOfType<AbilitySelectionDialog>(true);dialog.Open();yield return null;Check(!mini.activeSelf,"augment modal cannot revive minimap");dialog.SkipAbilities();yield return null;
            Vector2 origin=player.transform.position;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Chest/Bomb.prefab");
            void AddBomb(){var bomb=Object.Instantiate(prefab).GetComponent<Bomb>();bomb.Init(entities,player);bomb.gameObject.SetActive(false);slot.AddItem(bomb);}
            var enemies=new System.Collections.Generic.List<Monster>();
            Monster Spawn(Vector2 p)
            {
                var m=entities.SpawnMonster(0,p,level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0],0,false);
                m.enabled=false;m.StopAllCoroutines();m.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
                Set(m,"currentHealth",5000f);m.transform.position+=(Vector3)(p-NeedleTargeting.Position(m));enemies.Add(m);return m;
            }
            var center=origin+new Vector2(2,1);Spawn(center+Vector2.left*.3f);Spawn(center+Vector2.right*.3f);Spawn(center+Vector2.up*.3f);
            var isolated=Spawn(origin+Vector2.left*3);for(int i=0;i<6;i++)Spawn(origin+Vector2.right*(30+i*.1f));Physics2D.SyncTransforms();
            Vector2 auto=BombTargeting.AutomaticTarget(entities,player,Camera.main);
            Check(Vector2.Distance(auto,center+Vector2.up*.1f)<.05f,"automatic aim chooses dense visible group centroid, ignores offscreen larger group");
            Check(BombTargeting.DensestCenter(Array.Empty<Vector2>(),origin,BombTargeting.Radius)==origin,"no enemies falls back to player position");
            Check(Vector2.Distance(BombTargeting.DensestCenter(new[]{new Vector2(-1,0),new Vector2(1,0),new Vector2(5,0)},new Vector2(5,0),BombTargeting.Radius),Vector2.zero)<.01f,"spaced enemies fitting one blast outrank nearer isolated enemy");
            float width=player.Blueprint.idleSpriteSequence[0].bounds.size.x*player.GetComponentInChildren<SpriteAnimator>().transform.lossyScale.x;
            Check(Mathf.Abs(BombTargeting.Radius*2/width-4)<.3f,"blast diameter is four live Ashi widths");
            AddBomb();AddBomb();Check(slot.Count==2,"bomb inventory holds two");
            var mode=UnityEngine.InputSystem.InputSystem.settings.updateMode;UnityEngine.InputSystem.InputSystem.settings.updateMode=UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            foreach(var key in new[]{UnityEngine.InputSystem.Key.Digit1,UnityEngine.InputSystem.Key.Z})
            {
                int before=slot.Count;UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(key));UnityEngine.InputSystem.InputSystem.Update();
                Call(aim,"Update");slot.UseItem();Check(slot.Count==before-1,"keyboard "+key+" consumes once despite legacy callback");
                var thrown=Object.FindObjectsOfType<ThrownConsumableBomb>().Single();Check(Vector2.Distance(thrown.Target,auto)<.05f,"keyboard throws at crowd");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());UnityEngine.InputSystem.InputSystem.Update();slot.UseItem();
                Check(slot.Count==before-1,"key release cannot spend again or enter aim");
                yield return new WaitForSeconds(1.3f);
            }
            Check(enemies.Take(3).All(m=>Mathf.Approximately(m.HP,4400))&&isolated.HP==5000,"two explosions damage only monsters in radius, 300 each");
            AddBomb();AddBomb();var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            Vector2 click=Camera.main.WorldToScreenPoint(center);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=click});UnityEngine.InputSystem.InputSystem.Update();
            slot.GetComponent<Button>().onClick.Invoke();yield return null;Call(aim,"Update");
            Check(aim.Aiming&&slot.Count==2&&!Cursor.visible,"first HUD click arms bomb cursor without consumption");
            Check(Mathf.Abs(aim.Preview.bounds.size.x-BombTargeting.Radius*2)<.01f&&aim.Preview.color.a<.4f,"translucent preview matches actual diameter");
            var border=aim.Preview.transform.GetChild(0).GetComponent<SpriteRenderer>();
            Check(border.color.r<aim.Preview.color.r&&border.color.a>.9f,"dark red boundary is clearer than fill");
            Check(SortingLayer.GetLayerValueFromID(aim.Preview.sortingLayerID)>=SortingLayer.GetLayerValueFromID(enemies[0].GetComponentInChildren<SpriteRenderer>().sortingLayerID),"preview uses foreground sorting layer");
            yield return Capture("01-bomb-preview");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=click}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));UnityEngine.InputSystem.InputSystem.Update();
            Call(aim,"Update");Check(!aim.Aiming&&slot.Count==1&&!aim.Preview.enabled&&Cursor.visible,"second field click throws once and immediately removes preview/restores cursor");
            Call(needle,"ReadNeedleAimInput");Check(!(bool)Field(needle,"hasPendingNeedleDirection").GetValue(needle),"bomb click does not also redirect needle");
            yield return Capture("02-bomb-flight");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=click});UnityEngine.InputSystem.InputSystem.Update();
            yield return new WaitForSeconds(.8f);slot.UsePointerItem();yield return null;
            Time.timeScale=0;Call(aim,"Update");Check(!aim.Aiming&&slot.Count==1&&Cursor.visible,"pause cancels aim without consuming inventory");Time.timeScale=1;
            slot.UsePointerItem();yield return null;
            var touch=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            Vector2 hud=slot.transform.position;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=11,phase=UnityEngine.InputSystem.TouchPhase.Began,position=hud});UnityEngine.InputSystem.InputSystem.Update();Call(aim,"Update");
            Check(aim.Aiming&&slot.Count==1,"UI touch cannot confirm a field throw");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=12,phase=UnityEngine.InputSystem.TouchPhase.Began,position=click});UnityEngine.InputSystem.InputSystem.Update();Call(aim,"Update");
            Check(aim.Aiming,"field touch can preview while another finger holds UI");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=12,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=click});UnityEngine.InputSystem.InputSystem.Update();Call(aim,"Update");
            Check(!aim.Aiming&&slot.Count==0,"field touch release confirms exactly one throw");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=11,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=hud});UnityEngine.InputSystem.InputSystem.Update();
            UnityEngine.InputSystem.InputSystem.settings.updateMode=mode;
            yield return new WaitForSeconds(.8f);Check(!aim.BeginAim(slot),"empty inventory cannot arm");
            Check(AugmentDamageTracker.Instance.AugmentDamages.ContainsKey("폭탄"),"bomb damage is recorded in results");
            yield return Capture("03-hud-without-minimap");SessionState.SetBool(Key+"Done",true);
        }
    }
}
