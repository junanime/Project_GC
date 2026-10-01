using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Tests.Editor
{
    public static class OctoberGameplayChecks
    {
        public static bool Done;
        static void Check(bool ok,string message){if(!ok)throw new Exception("[OctoberGameplay] FAIL "+message);Debug.Log("[OctoberGameplay] PASS "+message);}
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        static void Capture(string name)
        {var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/OctoberIntegrationProof/"+name+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
        static IEnumerator WaitUntil(Func<bool> condition,string context,float seconds=8)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),context);}
        public static IEnumerator Run()
        {
            var ui=ApothecaryUI.Instance;var level=UnityEngine.Object.FindObjectOfType<LevelManager>();var p=level.PlayerCharacter;
            var items=OctoberItemRuntime.Get(p);
            var chargeWeapon=UnityEngine.Object.FindObjectOfType<SyringeDartAbility>();
            int attacksBeforePreview=items.State.attacks;
            var preview=typeof(SyringeDartAbility).GetMethod("UpdateHeavySnipeChargePreview",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int i=0;i<4;i++)preview.Invoke(chargeWeapon,new object[]{i/3f,Vector2.right});
            Check(items.State.attacks==attacksBeforePreview,"Heavy charge preview never counts as an attack or triggers item echoes");
            typeof(SyringeDartAbility).GetMethod("HideHeavySnipeChargePreview",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(chargeWeapon,null);
            // Isolate conditional effects from automatic proc chains during geometry checks.
            items.State.owned.Clear();items.State.delayedExp=0;items.State.overflow=0;
            var sample=ScriptableObject.CreateInstance<MonsterBlueprint>();sample.name="October resistance test";
            items.Give(ui.Config.items[62]);for(int i=0;i<8;i++)items.Hurt(1,sample);
            Check(Mathf.Abs(items.Incoming(100,sample)-80)<.01f,"Repeated damage resistance caps at 20%");
            var copy=JsonUtility.FromJson<OctoberItemState>(JsonUtility.ToJson(items.Capture()));items.Restore(copy);
            Check(Mathf.Abs(items.Incoming(100,sample)-80)<.01f,"Resistance survives serialized stage transfer");
            items.State.owned.Clear();items.Give(ui.Config.items[46]);Check(Mathf.Abs(items.SlowMultiplier(.5f)-.35f)<.001f,"Slow item adds 15 percentage points");
            Check(Mathf.Abs(items.SlowMultiplier(.2f)-.3f)<.001f,"Slow strength never exceeds 70%");
            items.State.owned.Clear();items.Give(ui.Config.items[26]);items.Healed(0,p.MaxHealth*10);
            Check(Mathf.Abs(items.State.overflow-p.MaxHealth*.25f)<.01f,"Overheal cannot grow without bound");
            items.State.owned.Clear();items.State.overflow=0;UnityEngine.Object.Destroy(sample);
            var stats=UnityEngine.Object.FindObjectOfType<StatsManager>();var shop=MerchantUIManager.Instance;
            var npcObject=new GameObject("October shop QA",typeof(BoxCollider2D));npcObject.transform.position=p.transform.position+Vector3.right*4;
            var npc=npcObject.AddComponent<MerchantNPC>();npc.SetShopItems(new System.Collections.Generic.List<MerchantItemBlueprint>{ui.Config.items[0],ui.Config.items[6],ui.Config.items[24]});
            shop.OpenShop(npc);yield return null;Check(ui.Page=="merchant"&&Time.timeScale==0,"Real NPC opens themed paused shop");
            int wallet=stats.CoinsGained;var offered=npc.GetShopItems()[0];
            var purchase=ui.GetComponentsInChildren<Button>().First(b=>b.name=="Button 구매 · "+offered.cost+" G");purchase.onClick.Invoke();yield return null;
            Check(items.Has(offered.octoberId)&&stats.CoinsGained==wallet-offered.cost,"Real shop purchase applies item and exact payment");
            shop.OnClickPurchaseItem(offered,null);Check(stats.CoinsGained==wallet-offered.cost,"Repeated purchase callback cannot charge twice");
            Check(ui.Page=="hud"&&Time.timeScale==1,"Purchase returns to live gameplay");
            items.State.owned.Clear();
            p.AddMaxHealthBonus(10000);p.GainHealth(10000);p.AddDashCharge(30);
            foreach(var weapon in UnityEngine.Object.FindObjectsOfType<SyringeDartAbility>())weapon.enabled=false;
            var director=UnityEngine.Object.FindObjectOfType<MiniStageDirector>();Check(director!=null,"Live mini-stage director");
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/MiniStages"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(o=>o.GetComponent<MiniStageRoomBase>()).Where(MiniStageDirector.RoomEnabled).ToArray();
            Check(prefabs.Length==6,"Exactly six enabled room prefabs; excluded rooms stay excluded");
            var camera=Camera.main;float zoom=camera.orthographicSize;
            foreach(var prefab in prefabs)
            {
                Set(director,"roomPrefabs",new[]{prefab});Set(director,"useForcedRoomIndex",false);
                director.EnterMiniStageFromPortal(null);
                yield return WaitUntil(()=>director.IsInsideMiniStage,"Enter "+prefab.name);
                var room=director.CurrentRoom;var arena=room.GetComponent<MiniStageArenaGeometry>();
                Check(arena!=null&&arena.Contains(p.transform.position,.1f),"Player starts inside "+prefab.name);
                var background=room.GetComponentsInChildren<SpriteRenderer>().First(s=>s.name=="MiniStage_Background");
                var original=prefab.GetComponentsInChildren<SpriteRenderer>(true).First(s=>s.name=="MiniStage_Background");
                Check(Mathf.Abs(background.bounds.size.x-original.bounds.size.x)<.01f&&Mathf.Abs(background.bounds.size.y-original.bounds.size.y)<.01f,"Original world background dimensions "+prefab.name);
                Check(room.transform.Find("October solid walls").GetComponentsInChildren<BoxCollider2D>().Length==8,"Eight thick visual wall segments "+prefab.name);
                for(int i=0;i<24;i++)Check(arena.Contains(arena.RandomInside(.65f),.64f),"Safe spawn "+prefab.name+" #"+i);
                var rb=p.GetComponent<Rigidbody2D>();
                for(int i=0;i<4;i++)
                {
                    Vector2 direction=new Vector2(i%2==0?1:-1,i<2?1:-1).normalized;
                    rb.position=arena.Clamp(arena.PlayArea.center+direction*arena.PlayArea.width,.55f);p.LookDirection=direction;p.Move(direction);
                    p.TryDash();yield return new WaitForSeconds(.45f);p.Move(Vector2.zero);
                    Check(arena.Contains(rb.position,.1f),"Actual diagonal dash cannot cross wall "+prefab.name+" #"+i);
                }
                p.transform.position=arena.PlayArea.center;rb.position=arena.PlayArea.center;rb.velocity=Vector2.zero;camera.orthographicSize=zoom;
                yield return new WaitForSeconds(.8f);Capture("room-"+prefab.name);
                if(room is MiniStageSniperRoom)
                    foreach(var sniper in (System.Collections.IEnumerable)typeof(MiniStageSniperRoom).GetField("spawnedSnipers",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(room))
                        if(sniper is Monster m&&m.HP>0)Check(arena.Contains(m.transform.position,.1f),"Sniper spawns inside near perimeter");
                typeof(MiniStageRoomBase).GetMethod("CompleteRoomWithoutReward",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(room,null);
                director.ReturnToFieldFromInteractable(room.ReturnInteractable);
                yield return WaitUntil(()=>!director.IsInsideMiniStage,"Return from "+prefab.name);
                Check(MiniStageArenaGeometry.Current==null,"Room boundary released on return");
                camera.orthographicSize=zoom;
            }
            // Track the real boss component in all quadrants, then bring it on screen.
            var boss=new GameObject("Compass QA boss").AddComponent<SnailBossRuntime>();boss.AutoPatterns=false;boss.Initialize(p);
            foreach(Vector2 direction in new[]{Vector2.right,Vector2.up,Vector2.left,Vector2.down})
            {
                boss.transform.position=p.transform.position+(Vector3)(direction*60);yield return new WaitForSeconds(.65f);
                var bubble=ui.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="Boss direction bubble");
                Check(bubble!=null&&bubble.gameObject.activeSelf,"Offscreen boss visible "+direction);
                Check(Vector2.Dot(bubble.anchoredPosition,direction)>0,"Boss indicator follows correct edge "+direction);
                if(direction==Vector2.right)Capture("boss-offscreen-right");
            }
            boss.transform.position=p.transform.position+Vector3.right*3;yield return new WaitForSeconds(.2f);
            Check(!ui.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Boss direction bubble").gameObject.activeSelf,"Nearby/onscreen boss indicator hides");
            UnityEngine.Object.Destroy(boss.gameObject);
            // Each character keeps the identical result frame and actions while switching only pose/title.
            var old=ui.SelectedCharacter;
            foreach(var character in ui.Config.characters)
            {
                Set(ui,"character",character);
                for(int result=0;result<2;result++)
                {
                    ApothecaryUI.TryShowResult(result==1);yield return null;
                    Check(ui.CharacterPreview.sprite==OctoberArt.Character(character,result==1?2:1),"Correct result art "+character.name+" "+result);
                    Check(ui.GetComponentsInChildren<Button>().Length==3,"Identical result actions "+character.name+" "+result);
                    Capture("result-"+OctoberArt.CharacterKey(character)+"-"+result);
                }
            }
            Set(ui,"character",old);ui.Show("hud");Time.timeScale=1;Done=true;
        }
    }
}
