using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    // An invulnerable automated observer, not a human difficulty/clear-rate benchmark.
    public static class OctoberQaSoak
    {
        const string Output="Library/OctoberQaSoak";
        static void Capture(string name)
        {var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);}
        public static IEnumerator Run(LevelManager level)
        {
            Directory.CreateDirectory(Output);
            var actor=level.PlayerCharacter;var map=FiniteStomachMap.Instance;
            OctoberQaSweep.FindField(actor,"isInvincible").SetValue(actor,true);
            Time.timeScale=1;
            var variants=new[]{
                new FieldLayoutStudy.Settings(),
                new FieldLayoutStudy.Settings{chests=24,merchants=20,bloodClots=16},
                new FieldLayoutStudy.Settings{chests=24,merchants=20,bloodClots=16,discoveryRadius=7},
                new FieldLayoutStudy.Settings{chests=26,merchants=22,bloodClots=16,discoveryRadius=11}
            };
            for(int i=0;i<variants.Length;i++)
            {FieldLayoutStudy.Simulate(map,actor.transform.position,Output+"/candidate-"+i,150,variants[i]);yield return null;}
            var points=FieldLayoutStudy.Generate(map,actor.transform.position,20261014,variants[1]);
            FieldLayoutStudy.Spawn(level,points);
            yield return new WaitForSeconds(1);
            File.WriteAllText(Output+"/samples.csv","fieldSeconds,kills,eliteKills,gold,shopPurchases,chestsOpened,augmentsChosen,livingMonsters,x,y\n");
            File.WriteAllText(Output+"/notes.txt","Editor-only layout: 24 chests / 20 merchants / 16 clots / 6 altars. Ashi normal damage, invulnerable for continuity. Uses actual combat, drops, level-up selection, field events. Observer knows only nearby objects. Not a clear-rate or FPS benchmark. Stops before rollcake boss.\n");
            int chests=0,purchases=0,augments=0;float nextSample=0,nextImage=0,nextSkill=0,nextDash=0;
            float began=Time.realtimeSinceStartup;
            Vector2 waypoint=actor.transform.position;
            var visitedShops=new Dictionary<MerchantNPC,int>();
            var stats=Object.FindObjectOfType<StatsManager>();
            Action<Chest> opened=c=>chests++;Chest.OnAnyChestOpened+=opened;
            try
            {
                while(level.CurrentLevelTime<875 && Time.realtimeSinceStartup-began<1100)
                {
                    if(level.IsLevelEnded)throw new Exception("Soak ended unexpectedly before planned stop");
                    var ui=ApothecaryUI.Instance;
                    var dialog=level.EntityManager.AbilitySelectionDialog;
                    if(actor.IsTrapBound)
                    {
                        var trap=Object.FindObjectsOfType<TrapMonster>().FirstOrDefault(t=>t.IsActive&&t.EscapeTarget>=0);
                        if(trap!=null)
                        {
                            var input=typeof(TrapMonster).GetMethod("HandleArrowInput",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                            input.Invoke(trap,new[]{Enum.ToObject(input.GetParameters()[0].ParameterType,trap.EscapeTarget)});
                        }
                        yield return new WaitForSecondsRealtime(.25f);continue;
                    }
                    if(dialog.MenuOpen)
                    {
                        actor.Move(Vector2.zero);
                        var card=dialog.GetComponentsInChildren<AbilityCard>().FirstOrDefault();
                        if(card!=null){card.Selected();if(!dialog.MenuOpen)augments++;}
                        yield return new WaitForSecondsRealtime(.2f);continue;
                    }
                    if(ui.Page!="hud")
                    {File.AppendAllText(Output+"/notes.txt","Unexpected modal "+ui.Page+" at "+level.CurrentLevelTime+"\n");yield return new WaitForSecondsRealtime(.2f);continue;}
                    Vector2 position=actor.transform.position;
                    Chest targetChest=Object.FindObjectsOfType<Chest>().Where(c=>c.CanInteract&&Vector2.Distance(c.transform.position,position)<11).OrderBy(c=>Vector2.Distance(c.transform.position,position)).FirstOrDefault();
                    MerchantNPC targetShop=Object.FindObjectsOfType<MerchantNPC>().Where(n=>n.CanInteract&&(!visitedShops.TryGetValue(n,out int previousGold)||stats.CoinsGained>=previousGold+25)&&Vector2.Distance(n.transform.position,position)<11).OrderBy(n=>Vector2.Distance(n.transform.position,position)).FirstOrDefault();
                    float chestDistance=targetChest==null?float.MaxValue:Vector2.Distance(targetChest.transform.position,position);
                    float shopDistance=targetShop==null?float.MaxValue:Vector2.Distance(targetShop.transform.position,position);
                    if(chestDistance<shopDistance)waypoint=targetChest.transform.position;
                    else if(targetShop!=null)waypoint=targetShop.transform.position;
                    else if(Vector2.Distance(position,waypoint)<2)
                    {
                        for(int attempt=0;attempt<200;attempt++)
                        {var r=map.WorldRect;waypoint=new Vector2(UnityEngine.Random.Range(r.xMin,r.xMax),UnityEngine.Random.Range(r.yMin,r.yMax));if(map.DistanceToRim(waypoint,out _)>12)break;}
                    }
                    if(chestDistance<1.25f)targetChest.OpenChest();
                    if(shopDistance<1.8f && targetShop.TryOpenShop())
                    {
                        actor.Move(Vector2.zero);visitedShops[targetShop]=stats.CoinsGained;
                        var items=targetShop.GetShopItems().Where(i=>!OctoberItemRuntime.Get(actor).Has(i.octoberId)&&RelicRuntime.Price(actor,i.cost)<=stats.CoinsGained).OrderBy(i=>i.cost).ToArray();
                        if(items.Length>0)
                        {MerchantUIManager.Instance.OnClickPurchaseItem(items[0],null);purchases++;}
                        else MerchantUIManager.Instance.CloseShop();
                        yield return new WaitForSecondsRealtime(.2f);continue;
                    }
                    // Collect nearby rewards physically; merely passing chests is not acquisition.
                    var pickup=Object.FindObjectsOfType<Collectable>().Where(c=>!c.IsBeingCollected&&c.CollectableType!=null&&c.CollectableType.inventoryStackSize==0&&Vector2.Distance(c.transform.position,position)<4).OrderBy(c=>Vector2.Distance(c.transform.position,position)).FirstOrDefault();
                    if(pickup!=null)waypoint=pickup.transform.position;
                    var direction=(waypoint-position).normalized;
                    actor.Move(direction);
                    if(level.CurrentLevelTime>=nextSkill){actor.Skills.TryActivate();nextSkill=level.CurrentLevelTime+5;}
                    if(level.CurrentLevelTime>=nextDash){actor.TryDash();nextDash=level.CurrentLevelTime+12;}
                    if(level.CurrentLevelTime>=nextSample)
                    {
                        nextSample=level.CurrentLevelTime+15;
                        File.AppendAllText(Output+"/samples.csv",$"{level.CurrentLevelTime:0.0},{stats.MonstersKilled},{stats.EliteMonstersKilled},{stats.CoinsGained},{purchases},{chests},{augments},{level.EntityManager.LivingMonsters.Count},{position.x:0.0},{position.y:0.0}\n");
                        foreach(var m in level.EntityManager.LivingMonsters)
                            if(m!=null&&m.HP>0&&m.BodyRenderer!=null&&m.BodyRenderer.sharedMaterial!=null&&!m.BodyRenderer.sharedMaterial.shader.isSupported)
                                Debug.LogError("Soak unsupported monster shader: "+m.name);
                    }
                    if(level.CurrentLevelTime>=nextImage)
                    {nextImage=level.CurrentLevelTime+120;yield return new WaitForEndOfFrame();Capture("field-"+Mathf.RoundToInt(level.CurrentLevelTime));}
                    yield return new WaitForSecondsRealtime(.2f);
                }
                actor.Move(Vector2.zero);
                File.AppendAllText(Output+"/notes.txt",$"Completed fieldSeconds={level.CurrentLevelTime:0.0}, realSeconds={Time.realtimeSinceStartup-began:0.0}, kills={stats.MonstersKilled}, gold={stats.CoinsGained}, purchases={purchases}, chests={chests}, augments={augments}\n");
                if(level.CurrentLevelTime<875)throw new Exception("Soak stalled before target field time");
                level.enabled=false;level.EntityManager.enabled=false;
                foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SupplyCapsuleDirector){b.enabled=false;b.StopAllCoroutines();}
                foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(m!=null&&m.HP>0)m.DespawnForStageTransfer();
                foreach(var a in Object.FindObjectsOfType<Ability>())a.enabled=false;
                Directory.CreateDirectory("Library/StomachMapProof");
                yield return StomachMapChecks.Run(level,map);
                File.WriteAllText(Output+"/result.txt","PASS");
            }
            finally {Chest.OnAnyChestOpened-=opened;actor.Move(Vector2.zero);}
        }
    }
}
