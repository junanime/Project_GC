using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Editor
{
    public static class WkuOrdinaryMonsterChecks
    {
        public static IEnumerator Run(LevelManager level,Action<bool,string> check)
        {
            var controller=level.GetComponent<FestivalRunController>();var settings=FestivalDemo.Settings;
            var originals=level.CurrentLevelBlueprint.monsters.SelectMany(p=>p.monsterBlueprints).ToArray();
            string snapshot=string.Join("|",originals.Select(JsonUtility.ToJson));
            bool oldPause=level.IsRunFlowPaused;float oldScale=Time.timeScale,oldTime=level.CurrentLevelTime;
            var clock=typeof(LevelManager).GetField("levelTime",BindingFlags.Instance|BindingFlags.NonPublic);
            level.SetRunFlowPaused(true);Time.timeScale=0;
            try
            {
                for(int i=0;i<9;i++)
                {
                    var spec=settings.normalMonsters[i];var source=originals.First(b=>((Object)b).name==spec.blueprint);var copy=controller.ResolveNormal(source);
                    check(Mathf.Approximately(copy.hp,60*Mathf.Pow(1.25f,i/3))&&copy.movespeed==spec.speed&&copy.atk==spec.damage,"festival HP speed damage preserved: "+spec.blueprint);
                }
                var catalog=WkuMonsterLookCatalog.Current;check(catalog.looks.Length>=30,"at least 30 ordinary appearances from stages 0 1 2");
                check(catalog.looks.Select(r=>r.stage).Distinct().Count()==3,"all three source stages represented");
                WkuMonsterLookCatalog.RestartCycleForQA();var seen=new HashSet<string>();int flat=controller.SelectNormal(0,.1f);
                for(int i=0;i<catalog.looks.Length;i++)
                {
                    yield return null;
                    int before=level.EntityManager.LivingMonsters.Count;
                    level.SpawnMonsterByFlatIndex(flat);var actor=level.EntityManager.LivingMonsters.Last();var skin=actor.GetComponent<WkuMonsterSkin>();
                    check(level.EntityManager.LivingMonsters.Count==before+1,"isolated ordinary spawn succeeds "+i);
                    seen.Add(skin.LookId);check(actor.SpawnMaxHealth==60&&Mathf.Abs(actor.moveSpeed-.64f)<=.101f&&actor.Blueprint.atk==8,$"same stats with varied appearance {i}: HP={actor.SpawnMaxHealth} speed={actor.moveSpeed} damage={actor.Blueprint.atk}");
                    if(i==0)
                    {
                        var body=(SpriteRenderer)typeof(WkuMonsterSkin).GetField("body",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(skin);
                        var box=body.GetComponent<BoxCollider2D>();var size=box.size;var scale=body.transform.localScale;yield return null;
                        check(box.size==size&&body.transform.localScale==scale,"visual swap preserves original collision size and body transform");
                        check(skin.Visual.GetComponent<Collider2D>()==null,"appearance renderer has no collider");
                    }
                    actor.DespawnForStageTransfer();
                }
                check(seen.Count==catalog.looks.Length,"one shuffle cycle displays every ordinary appearance with identical stats");
                check(snapshot==string.Join("|",originals.Select(JsonUtility.ToJson)),"source blueprints unchanged by appearance selection");
            }
            finally{clock.SetValue(level,oldTime);level.SetRunFlowPaused(oldPause);Time.timeScale=oldScale;}
            foreach(float seconds in new[]{8f,13f,16f,24f,30f,32f,40f})
            {
                clock.SetValue(level,seconds);yield return null;yield return null;
                check(controller.SpecialSpawned==0,"no special monster scheduled at "+seconds+" seconds");
                check(level.EntityManager.LivingMonsters.All(m=>settings.normalMonsters.Any(s=>s.blueprint==((Object)m.Blueprint).name)),"only ordinary stat profiles at "+seconds);
                check(Object.FindObjectsOfType<StageEventCornerUI>().All(c=>!c.IsVisible),"no creature corner ornaments at "+seconds);
                check(Object.FindObjectsOfType<ChameleonPortrait>().All(p=>p.GetComponent<CanvasGroup>().alpha==0),"no creature event portraits at "+seconds);
            }
            clock.SetValue(level,oldTime);
        }
    }
}
