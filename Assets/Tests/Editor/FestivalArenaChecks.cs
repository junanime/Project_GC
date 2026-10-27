using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    public static class FestivalArenaChecks
    {
        static void Check(bool ok,string label)=>FestivalQaSmoke.Check(ok,label);
        public static IEnumerator Run(LevelManager level)
        {
            var hero=level.PlayerCharacter;var skills=hero.Skills;
            string key=OctoberArt.CharacterKey(hero.Blueprint);
            float expectedHealth=key=="Ashi"?80:key=="Hyuki"?100:key=="Ari"?200:150;
            Check(Mathf.Approximately(hero.MaxHealth,expectedHealth)&&Mathf.Approximately(hero.CurrentHealth,expectedHealth),"festival preset starts at configured full health: "+key);
            Check(Object.FindObjectsOfType<FinalBossSummonInteractable>().Length==0,"no live festival boss altar");
            Check(Object.FindObjectOfType<StageEventDirector>().FestivalEventKind!="Gold","no festival gold event");
            Check(level.EntityManager.SpawnCoin(hero.transform.position)==null,"festival monster/event coins suppressed");
            Check(CurrencyJar.Create(level,hero.transform.position,false,null)==null&&CurrencyJar.Create(level,hero.transform.position,true,null)==null,"both currency jar placements suppressed");
            Check(CurrencyJarDrop.Create(level,hero.transform.position,false,50,null)==null&&CurrencyJarDrop.Create(level,hero.transform.position,true,20,null)==null,"gold and silver jar drops suppressed");
            int silver=SilverWallet.Silver;SilverRunRewarder.RewardPickup(20);
            Check(SilverWallet.Silver==silver,"festival silver pickup cannot change wallet");
            var manager=Object.FindObjectOfType<AbilityManager>();
            var nobles=manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true);
            var owned=nobles.Where(a=>a.Owned).ToArray();
            Check(owned.Length==1&&FestivalDemo.StartingNobleAllowed(owned[0]),"exactly one random noble at start "+key);
            Check(nobles.Count(FestivalDemo.StartingNobleAllowed)==9,"nine eligible nobles, life-burn excluded to preserve HP");
            Check(FestivalDemo.GrantStartingNoble(manager)==owned[0]&&nobles.Count(a=>a.Owned)==1,"starting grant cannot reroll or duplicate "+key);
            var quest=hero.GetComponent<PrescriptionRuntime>();
            quest.Record(PrescriptionRuntime.Goal.Kills,10000);quest.Record(PrescriptionRuntime.Goal.Dashes,10000);quest.Record(PrescriptionRuntime.Goal.ActiveUses,10000);
            Check(quest.Claimed&&!quest.TryAutomaticReward()&&nobles.Count(a=>a.Owned)==1,"quest progress cannot grant another noble "+key);
            Check(!Object.FindObjectsOfType<RectTransform>().Any(t=>t.name=="Prescription HUD"),"festival hides quest HUD "+key);
            var chest=Object.FindObjectOfType<Chest>();
            Check(chest!=null,"festival item chest retained");
            var roll=typeof(Chest).GetMethod("RollLoot",BindingFlags.Instance|BindingFlags.NonPublic);
            var festivalRoll=typeof(Chest).GetMethod("RollFestivalLoot",BindingFlags.Instance|BindingFlags.NonPublic);
            int bombs=0,heals=0;
            for(int i=0;i<1000;i++)
            {
                var reward=(Loot<GameObject>)festivalRoll.Invoke(chest,new object[]{(i+.5f)/1000});
                if(reward.item.GetComponent<Bomb>()!=null)bombs++;
                else if(reward.item.GetComponent<Health>()!=null)heals++;
                else throw new Exception("Festival chest reward is neither bomb nor healing");
            }
            Check(bombs==300&&heals==700,"festival chest probability intervals are 30% bomb / 70% healing");
            for(int i=0;i<100;i++)
            {
                var reward=(Loot<GameObject>)roll.Invoke(chest,null);
                if(reward==null||reward.item==null||(reward.item.GetComponent<Bomb>()==null&&reward.item.GetComponent<Health>()==null))
                    throw new Exception("Chest produced an unapproved festival reward");
            }
            Check(true,"100 actual chest rolls contain only bomb or healing");
            Check(FestivalLeaderboard.Score(new RunScoreData{normal=1})==30&&FestivalLeaderboard.Score(new RunScoreData{special=1})==150,"festival normal/special score 30/150");
            Check(new RunScoreData{special=1}.Total==50,"ordinary online special score unchanged");
            if(key=="Ashi")Check(Mathf.Approximately(skills.EffectiveActiveDuration,10),"Ashi active lasts ten seconds");
            if(key=="Hyuki")Check(Mathf.Approximately(skills.StatusDamageBonus,.25f)&&Mathf.Approximately(IceSkillRules.EffectiveFreezeDelay,.6f),"Hyuki status bonus and freeze startup");
            if(key=="Shini")Check(Mathf.Approximately(skills.Breath.EffectivePreparation,.3f)&&Mathf.Approximately(skills.Breath.EffectiveDamagePerSecond,140),"Shini preparation and base damage");
            if(key=="Ari"||key=="Hyuki")
            {
                for(int use=0;use<3;use++)
                {
                    Check(skills.TryActivate(),key+" activation at simulated "+(20*use)+" seconds");
                    skills.Tick(19.9f);
                    Check(!skills.CanActivate,key+" not ready before twenty seconds");
                    skills.Tick(.11f);
                    Check(skills.CanActivate,key+" cooldown counts from activation");
                }
                skills.EndRun();
            }
            if(key=="Ashi")
            {
                Check(FestivalDemo.Preset(hero.Blueprint).damageBonus==0 && FestivalDemo.Preset(hero.Blueprint).attackSpeedBonus==0,"Ashi has no festival starting offense bonuses");
                Check(skills.TryActivate() && Mathf.Approximately(skills.MovementMultiplier,1),"Ashi active grants no movement boost");
                skills.EndRun();
                yield return FestivalRosterChecks.Run(level);
                yield return AcidScoring(level);
            }
            FestivalDemo.EnabledOverride=false;
            try
            {
                if(key=="Ashi")Check(Mathf.Approximately(skills.EffectiveActiveDuration,skills.Definition.activeDuration),"ordinary Ashi duration unchanged");
                if(key=="Hyuki")Check(Mathf.Approximately(skills.StatusDamageBonus,.1f)&&Mathf.Approximately(IceSkillRules.EffectiveFreezeDelay,1.65f),"ordinary Hyuki untouched");
                if(key=="Shini")Check(Mathf.Approximately(skills.Breath.EffectivePreparation,.88f)&&Mathf.Approximately(skills.Breath.EffectiveDamagePerSecond,35),"ordinary Shini untouched");
                var coin=level.EntityManager.SpawnCoin(hero.transform.position,CoinType.Bronze1,false);
                Check(coin!=null,"ordinary currency spawning retained");level.EntityManager.DespawnCoin(coin,false);
            }
            finally{FestivalDemo.EnabledOverride=null;}
            yield return null;
            Check(Object.FindObjectsOfType<Coin>().Length==0&&Object.FindObjectsOfType<CurrencyJarDrop>().Length==0,"no live currency pickups after checks");
        }
        static IEnumerator AcidScoring(LevelManager level)
        {
            int index=Array.FindIndex(level.CurrentLevelBlueprint.monsters,p=>p.monsterBlueprints.Any(b=>b!=null&&((Object)b).name=="Acid Spore Blueprint"));
            var blueprint=level.CurrentLevelBlueprint.monsters[index].monsterBlueprints.First(b=>b!=null&&((Object)b).name=="Acid Spore Blueprint");
            int score=RunScore.Current.Data.special;
            var acid=(ExplodingMonster)level.EntityManager.SpawnMonster(index,(Vector2)level.PlayerCharacter.transform.position+Vector2.right*8,blueprint);
            typeof(ExplodingMonster).GetMethod("StartExplosion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(acid,new object[]{null});
            float until=Time.realtimeSinceStartup+5;
            while(acid.gameObject.activeInHierarchy&&Time.realtimeSinceStartup<until)yield return null;
            Check(!acid.gameObject.activeInHierarchy&&RunScore.Current.Data.special==score,"acid self-explosion completes without special kill credit");
            acid=(ExplodingMonster)level.EntityManager.SpawnMonster(index,(Vector2)level.PlayerCharacter.transform.position+Vector2.right*8,blueprint);
            acid.TakeDamage(100000);
            Check(RunScore.Current.Data.special==score+1,"direct acid kill earns exactly one special kill");
            yield return new WaitForSeconds(.5f);
        }
    }
}
