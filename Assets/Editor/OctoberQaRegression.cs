using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Editor
{
    public static class OctoberQaRegression
    {
        const string Output="Library/OctoberQa";
        static void Check(bool ok,string message)
        {File.AppendAllText(Output+"/checks.tsv",(ok?"PASS":"FAIL")+"\t"+message+"\n");if(!ok)Debug.LogError("[OctoberQa] "+message);}
        static void Set(object obj,string field,object value)=>OctoberQaSweep.FindField(obj,field).SetValue(obj,value);
        static object Call(object obj,string method,params object[] args)
        {for(var t=obj.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(obj,args);}throw new MissingMethodException(method);}
        static void Capture(string name)
        {var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);}
        public static IEnumerator Run(LevelManager level)
        {
            var actor=level.PlayerCharacter;var skills=actor.Skills;var blueprint=actor.Blueprint;
            var saved=actor.CaptureRunSceneState();var temp=Object.Instantiate(blueprint);Set(actor,"characterBlueprint",temp);
            foreach(string name in new[]{"AshiSkills","AriSkills","HyukiSkills","ShiniSkills"})
            {
                temp.skills=Resources.Load<CharacterSkillDefinition>(name);
                foreach(bool firstActive in new[]{false,true})
                {
                    skills.RestoreLevels(1,1);skills.RestoreDashRewardUpgrades(0);
                    int n=0;while(skills.TryUpgrade(firstActive))n++;
                    Check(n==(firstActive?skills.ActiveMaxLevel:skills.PassiveMaxLevel)-1,name+" first chosen skill reaches max");
                    n=0;while(skills.TryUpgrade(!firstActive))n++;
                    Check(n==(firstActive?skills.PassiveMaxLevel:skills.ActiveMaxLevel)-2,name+" other skill stops one below max");
                    Check(!(skills.PassiveLevel==skills.PassiveMaxLevel&&skills.ActiveLevel==skills.ActiveMaxLevel),name+" cannot max both");
                    for(int i=0;i<3;i++)Check(skills.TryUpgradeDash(),name+" dash reward "+(i+1));
                    int charges=actor.MaxDashCharges;Check(!skills.TryUpgradeDash()&&actor.MaxDashCharges==charges,name+" fourth dash reward rejected");
                    var snapshot=actor.CaptureRunSceneState();skills.RestoreDashRewardUpgrades(0);actor.RestoreRunSceneState(snapshot);
                    Check(skills.DashRewardUpgrades==3&&!skills.CanUpgradeDash,name+" dash reward cap survives transfer snapshot");
                    var ui=ApothecaryUI.Instance;ui.RequestSkillReward(()=>{});
                    float rewardDeadline=Time.realtimeSinceStartup+2;
                    while(ui.Page!="skillReward"&&Time.realtimeSinceStartup<rewardDeadline)yield return null;
                    yield return new WaitForEndOfFrame();
                    Check(ui.Page=="skillReward"&&Time.timeScale==0,name+" real queued reward pauses field");
                    Check(!ui.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.sprite==ui.Config.bookBackground),name+" reward has no title-art overlap");
                    Check(ui.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.Contains("회복하고 계속")&&b.interactable),name+" all capped rewards have exit fallback");
                    if(firstActive)Capture("reward-capped-"+name);
                    Call(ui,"FinishSkillReward",new object[]{null});
                    Check(ui.Page=="hud"&&Time.timeScale==1,name+" reward completion restores field");
                }
            }
            Set(actor,"characterBlueprint",blueprint);actor.RestoreRunSceneState(saved);Object.Destroy(temp);
            Set(actor,"isInvincible",true);
            // Reproduce reuse immediately during a hit flash: the returned instance must not stay white.
            var pools=level.CurrentLevelBlueprint.monsters;int cookiePool=Array.FindIndex(pools,p=>p.monstersPrefab.name.Contains("Dubai"));
            var cookie=level.EntityManager.SpawnMonster(cookiePool,(Vector2)actor.transform.position+Vector2.right*4,pools[cookiePool].monsterBlueprints[0]);
            Set(cookie,"currentHealth",10000f);cookie.TakeDamage(.01f);yield return null;
            cookie.DespawnForStageTransfer();
            cookie=level.EntityManager.SpawnMonster(cookiePool,(Vector2)actor.transform.position+Vector2.right*4,pools[cookiePool].monsterBlueprints[0]);
            yield return null;
            Check(cookie.BodyRenderer.sharedMaterial.shader.name=="Sprites/Default","Dubai pooled hit flash resets before reuse");
            cookie.gameObject.AddComponent<IceChillStatus>().Hit(0,actor);
            cookie.gameObject.AddComponent<CorrosionStatus>().Apply(10,.01f,.01f,5,false);
            yield return new WaitForSecondsRealtime(.2f);
            foreach(var r in cookie.GetComponentsInChildren<Renderer>())
                Check(r.sharedMaterials.All(m=>m!=null&&m.shader!=null&&m.shader.isSupported&&!m.shader.name.Contains("Error")),"Dubai status shader "+r.name);
            Capture("dubai-status");cookie.DespawnForStageTransfer();
            // A disabled/destroyed altar must still deliver the complete planned wave.
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Interaction/EliteSummonInteractableObject.prefab");
            Set(level,"levelTime",60f);
            var altar=Object.Instantiate(prefab,actor.transform.position+Vector3.right*3,Quaternion.identity).GetComponent<EliteMonsterSummonInteractable>();
            bool began=(bool)Call(altar,"ExecuteInteraction",actor);Call(altar,"CompleteInteraction");Object.Destroy(altar.gameObject);
            yield return new WaitForSecondsRealtime(1);
            var elites=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0&&m.Blueprint is EliteMonsterBlueprint).ToArray();
            Check(began&&elites.Length==3,"destroyed early altar completes 3 elite wave");
            Check(elites.Select(m=>m.Blueprint).Distinct().Count()==elites.Length,"elite wave has distinct monster types");
            Check(elites.All(m=>FiniteStomachMap.Instance.ExactDistance(m.transform.position,out _)>0),"elite wave remains inside map");
            foreach(var m in elites)m.DespawnForStageTransfer();
            Set(level,"levelTime",899f);
            altar=Object.Instantiate(prefab,actor.transform.position+Vector3.right*3,Quaternion.identity).GetComponent<EliteMonsterSummonInteractable>();
            began=(bool)Call(altar,"ExecuteInteraction",actor);Call(altar,"CompleteInteraction");Object.Destroy(altar.gameObject);
            yield return new WaitForSecondsRealtime(1);
            elites=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0&&m.Blueprint is EliteMonsterBlueprint).ToArray();
            Check(began&&elites.Length==5,"late altar completes 5 elite wave");Capture("elite-wave-five");
            foreach(var m in elites)m.DespawnForStageTransfer();
            // Activate two altars inside the inter-spawn interval.
            for(int i=0;i<2;i++)
            {
                altar=Object.Instantiate(prefab,actor.transform.position+Vector3.right*3,Quaternion.identity).GetComponent<EliteMonsterSummonInteractable>();
                Check((bool)Call(altar,"ExecuteInteraction",actor),"overlapping elite wave starts "+i);
                Call(altar,"CompleteInteraction");Object.Destroy(altar.gameObject);
            }
            yield return new WaitForSecondsRealtime(1);
            elites=level.EntityManager.LivingMonsters.Where(m=>m!=null&&m.HP>0&&m.Blueprint is EliteMonsterBlueprint).ToArray();
            Check(elites.Length==8,"overlapping altar waves respect combined cap 8");
            foreach(var m in elites)m.DespawnForStageTransfer();Set(level,"levelTime",0f);
            int elitePool=Array.FindIndex(pools,p=>p.monsterBlueprints.Any(b=>b is EliteMonsterBlueprint)&&p.monstersPrefab.GetComponent<MeleeMonster>()!=null);
            var lootElite=Object.Instantiate((EliteMonsterBlueprint)pools[elitePool].monsterBlueprints.First(b=>b is EliteMonsterBlueprint));
            var lootSource=Object.Instantiate(lootElite.sourceNormalBlueprint);
            lootSource.coinLootTable=new LootTable<CoinType>{lootTable=new[]{new Loot<CoinType>{item=CoinType.Gold5,dropChance=1}}};
            lootElite.sourceNormalBlueprint=lootSource;lootElite.useSourceLootTables=true;lootElite.guaranteedExtraCoinCount=0;
            lootElite.coinLootTable=new LootTable<CoinType>{lootTable=Array.Empty<Loot<CoinType>>()};
            var lootMonster=level.EntityManager.SpawnMonster(elitePool,(Vector2)actor.transform.position+Vector2.right*6,lootElite);
            int coinsBefore=Object.FindObjectsOfType<Coin>().Length;
            Call(lootMonster,"DropLoot");
            Check(Object.FindObjectsOfType<Coin>().Length==coinsBefore+1,"elite inherits source coin table as well as experience");
            lootMonster.DespawnForStageTransfer();Object.Destroy(lootElite);Object.Destroy(lootSource);
            FieldLayoutStudy.Simulate(FiniteStomachMap.Instance,actor.transform.position,Output,300);
            var points=FieldLayoutStudy.Generate(FiniteStomachMap.Instance,actor.transform.position,20261014,FieldLayoutStudy.Candidate);
            var objects=FieldLayoutStudy.Spawn(level,points);yield return new WaitForSecondsRealtime(1);
            Check(objects.Count==points.Count,"all planned field objects really spawn");
            Check(objects.All(o=>FiniteStomachMap.Instance.ExactDistance(o.transform.position,out _)>8),"actual field objects clear wall slope");
            Check(Object.FindObjectsOfType<MerchantNPC>().Length==16,"preview contains 16 merchants");
            Check(Object.FindObjectsOfType<Chest>().Length==20,"preview contains 20 item chests");
            Check(Object.FindObjectsOfType<BloodClotMonster>().Length==12,"preview contains 12 mini-stage entrances");
            ApothecaryUI.Instance.OpenRunBook();yield return null;yield return new WaitForEndOfFrame();Capture("layout-preview-tab");
            File.WriteAllText(Output+"/layout-points.csv","kind,x,y\n"+string.Join("\n",points.Select(p=>$"{p.kind},{p.position.x},{p.position.y}")));
            yield return OctoberUiCatalogChecks.Run(level);
        }
    }
}
