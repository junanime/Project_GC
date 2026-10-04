using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using E = Vampire.RelicBlueprint.RelicEffectType;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class RelicCatalogSmoke
    {
        const string Key = "RelicCatalogSmoke";
        static bool attached, done, failed, loaded;
        static double deadline;
        static RelicCatalogSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            RelicCatalogInstaller.Install();
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).position=new Rect(0,0,1280,741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            attached = done = failed = loaded = false; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message,string stack,LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }
        public static void Complete() { done = true; }
        static void Tick()
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (EditorApplication.timeSinceStartup > deadline) failed = true;
            if (done || failed)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key,false);
                Debug.Log("[RelicQA] FINISHED failed=" + failed);
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!loaded && EditorApplication.isPlaying)
            { loaded=true; SceneManager.LoadScene(0); return; }
            if (!attached && EditorApplication.isPlaying && ApothecaryUI.Instance != null)
            {
                attached=true; var go=new GameObject("Relic catalog QA");
                UnityEngine.Object.DontDestroyOnLoad(go); go.AddComponent<RelicCatalogRunner>();
            }
        }
    }
    public sealed class RelicCatalogRunner : MonoBehaviour
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool condition,string name)
        { if (!condition) throw new Exception("[RelicQA] FAIL "+name); Debug.Log("[RelicQA] PASS "+name); }
        static void Near(float a,float b,string name) => Check(Mathf.Abs(a-b)<.002f,name+" "+a+" = "+b);
        IEnumerator Start()
        {
            string old=RelicSaveData.GetEquippedRelicId(); bool had=PlayerPrefs.HasKey("EquippedRelicId");
            var ui=ApothecaryUI.Instance; var config=ui.Config;
            var oldCharacter=CrossSceneData.CharacterBlueprint;
            var unlocks=config.relics.Select(r=>new {key="RelicUnlocked_"+r.relicId,has=PlayerPrefs.HasKey("RelicUnlocked_"+r.relicId),value=PlayerPrefs.GetInt("RelicUnlocked_"+r.relicId)}).ToArray();
            Directory.CreateDirectory("Library/RelicQA");
            try
            {
                Check(config.relics.Length==34,"34 catalog entries");
                Check(config.relics.Select(r=>r.relicId).Distinct().Count()==34,"unique IDs");
                foreach(var r in config.relics)
                {
                    Check(r.icon!=null&&r.icon.rect.width>200&&!string.IsNullOrEmpty(r.Description),"asset "+r.relicName);
                    RelicSaveData.Unlock(r.relicId);
                }
                for(int page=0;page<6;page++)
                {
                    ui.Show("prepare",2);
                    typeof(ApothecaryUI).GetField("pageIndex",Hidden).SetValue(ui,page);
                    typeof(ApothecaryUI).GetField("selection",Hidden).SetValue(ui,Mathf.Min(33,page*6));
                    typeof(ApothecaryUI).GetMethod("Render",Hidden).Invoke(ui,null);
                    yield return new WaitForSecondsRealtime(.3f);
                    var labels=ui.GetComponentsInChildren<TextMeshProUGUI>();
                    var description=labels.FirstOrDefault(t=>t.name=="Selection description");
                    Check(description==null||!description.isTextOverflowing,"page "+page+" description fits");
                    Canvas.ForceUpdateCanvases();
                    var capture=ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes("Library/RelicQA/catalog-"+page+".png",capture.EncodeToPNG());
                    Destroy(capture);
                }
                RelicSaveData.Equip("relic_01");
                for(int index=0;index<config.relics.Length;index++)
                {
                    typeof(ApothecaryUI).GetField("pageIndex",Hidden).SetValue(ui,index/6);
                    typeof(ApothecaryUI).GetField("selection",Hidden).SetValue(ui,index);
                    typeof(ApothecaryUI).GetMethod("Render",Hidden).Invoke(ui,null);
                    Canvas.ForceUpdateCanvases();yield return null;
                    var description=ui.GetComponentsInChildren<TextMeshProUGUI>().First(t=>t.name=="Selection description");
                    Check(!description.isTextOverflowing,"full description fits "+config.relics[index].relicName);
                }
                CrossSceneData.CharacterBlueprint=config.characters.First(c=>c.skills.kind==CharacterSkillDefinition.SkillKind.Hyuki);
                SceneManager.LoadScene(1); yield return new WaitForSecondsRealtime(2);
                var level=FindObjectOfType<LevelManager>(); level.enabled=false;
                var guide=FindObjectOfType<TutorialGuide>(); if(guide!=null){guide.Close();guide.enabled=false;}
                Time.timeScale=0;
                var p=level.PlayerCharacter; var rtime=p.Relics;
                foreach(var weapon in FindObjectsOfType<SyringeDartAbility>()) weapon.enabled=false;
                Check(rtime!=null,"runtime on actual player");
                Near(p.MaxHealth,120,"legacy heart applied once"); Near(p.CurrentHealth,120,"bonus HP starts full");
                var general=PlayerGeneralStatRuntime.GetOrCreate(p);
                rtime.Equip(null); float baseHp=p.MaxHealth, speed=p.CurrentMoveSpeed, atk=p.DamageMultiplier, attackSpeed=p.AttackSpeedMultiplier;
                float area=p.ProjectileSizeMultiplier, xp=p.ExperienceMultiplier, crit=p.CritChance;
                foreach(var relic in config.relics)
                {
                    rtime.Equip(relic);
                    Near(rtime.Bonus(relic.effectType),relic.effectValue,"effect "+relic.relicName);
                    switch(relic.effectType)
                    {
                        case E.MaxHealth:Near(p.MaxHealth,baseHp+relic.effectValue,"flat HP");break;
                        case E.MaxHealthPercent:Near(p.MaxHealth,baseHp+p.Blueprint.hp*relic.effectValue,"percent HP");break;
                        case E.MoveSpeed:Near(p.CurrentMoveSpeed,speed+relic.effectValue,"speed");break;
                        case E.MoveSpeedPercent:Near(p.CurrentMoveSpeed,speed+p.Blueprint.movespeed*relic.effectValue,"percent speed");break;
                        case E.CritChance:Near(p.CritChance,crit+relic.effectValue,"crit");break;
                        case E.AttackSpeed:Near(p.AttackSpeedMultiplier,attackSpeed+relic.effectValue,"attack speed");break;
                        case E.Damage:Near(p.DamageMultiplier,atk+relic.effectValue,"damage");break;
                        case E.Area:Near(p.ProjectileSizeMultiplier,area*(1+relic.effectValue),"area");break;
                        case E.Experience:Near(p.ExperienceMultiplier,xp+relic.effectValue,"XP");break;
                        case E.HealingPickup:Near(rtime.PickupHealing(30),37.5f,"pickup heal only");break;
                        case E.DebuffResistance:Near(p.DebuffDuration(4),4*(1-relic.effectValue),"status resistance");break;
                        case E.ProjectileCount:Check(p.AdditionalProjectiles>=1,"projectile count");break;
                        case E.Pierce:
                            var weapon=FindObjectOfType<SyringeDartAbility>();
                            var runtime=(SyringeSpecialRuntime)typeof(SyringeDartAbility).GetMethod("BuildSpecialRuntime",Hidden).Invoke(weapon,null);
                            Check(runtime.pierceEnabled&&runtime.pierceCount>=relic.effectValue,"pierce without augment");break;
                        case E.Bounce:Check(p.MouthwashCount>=1,"bounce count");break;
                        case E.Gold:Near(general.GoldGainMultiplier,1.15f,"gold");break;
                        case E.Knockback:Near(general.KnockbackMultiplier,1.25f,"knockback");break;
                        case E.Duration:Near(general.StatusDurationMultiplier,1.2f,"duration");break;
                        case E.PickupRange:Near(general.PickupRangeMultiplier,1.3f,"pickup");break;
                        case E.Evasion:Near(general.EvasionBonus,.06f,"evasion");break;
                        case E.DamageReduction:Near(general.DamageReductionBonus,relic.effectValue,"reduction");break;
                        case E.HitInvincibility:Near(p.InvincibilityTimeBonus,.2f,"invincibility");break;
                        case E.ShopDiscount:Check(rtime.ShopPrice(25)==22&&rtime.ShopPrice(130)==111,"discount rounded up");break;
                        case E.Barrier:
                            Near(rtime.Absorb(7),0,"barrier absorbs");Near(rtime.State.barrier,relic.effectValue-7,"barrier spent");
                            var saved=rtime.Capture();rtime.Equip(relic);rtime.Restore(saved);
                            Near(rtime.State.barrier,relic.effectValue-7,"barrier transfer");break;
                        case E.Revival:
                            Check(rtime.ConsumeRevival()&&!rtime.ConsumeRevival(),"revive once");
                            var state=rtime.Capture();rtime.Equip(relic);rtime.Restore(state);
                            Check(rtime.Revives==0,"revive stays consumed after transfer");break;
                        case E.SlowChance:Near(p.SlowChance,.15f,"slow chance");break;
                    }
                    var snapshot=p.CaptureRunSceneState();
                    p.RestoreRunSceneState(snapshot);
                    Check(p.Relics.Equipped.relicId==relic.relicId,"scene snapshot "+relic.relicName);
                }
                rtime.Equip(config.relics.First(r=>r.relicId=="bandage"));
                p.TakeDamage(25); Near(p.CurrentHealth,baseHp-5,"real incoming damage consumes barrier before HP");
                rtime.Equip(config.relics.First(r=>r.relicId=="life_sprout"));
                float hp=p.CurrentHealth;rtime.Tick(1);Near(p.CurrentHealth,hp+.3f,"regen tick");rtime.Tick(0);Near(p.CurrentHealth,hp+.3f,"paused regen");
                rtime.Equip(config.relics.First(r=>r.relicId=="bandage"));
                rtime.Absorb(20);rtime.Tick(7);Near(rtime.State.barrier,0,"shield recharge delay");
                rtime.Tick(1);rtime.Tick(1);Near(rtime.State.barrier,5,"shield recharge rate");
                rtime.Equip(config.relics.First(r=>r.relicId=="pillow"));
                hp=p.CurrentHealth;rtime.Tick(1);Near(p.CurrentHealth,hp,"idle recovery delay");
                rtime.Tick(1);Near(p.CurrentHealth,hp+.5f,"idle recovery after delay only");
                var stun=p.gameObject.AddComponent<PlayerMiniStageStunRuntime>();stun.ApplyStun(1);
                hp=p.CurrentHealth;rtime.Tick(2);Near(p.CurrentHealth,hp,"stun cannot trigger idle heal");
                Destroy(stun);
                rtime.Equip(config.relics.First(r=>r.relicId=="chestnut"));
                var dummy=new GameObject("Relic reflection target");dummy.transform.position=p.transform.position+Vector3.right;
                dummy.AddComponent<CircleCollider2D>();dummy.AddComponent<BoxCollider2D>();
                var monster=dummy.AddComponent<Monster>();monster.enabled=false;
                typeof(Monster).GetField("currentHealth",Hidden).SetValue(monster,200f);
                Physics2D.SyncTransforms();
                rtime.Hurt(10);Near(monster.HP,195,"reflection hits multiple colliders once");
                rtime.Hurt(10);Near(monster.HP,195,"reflection cooldown");
                rtime.Tick(.5f);rtime.Hurt(200);Near(monster.HP,145,"reflection capped at fifty");
                Destroy(dummy);
                rtime.Equip(config.relics.First(r=>r.relicId=="revival_bud"));
                typeof(Character).GetField("isInvincible",Hidden).SetValue(p,false);
                p.TakeDamage(1000);Near(p.CurrentHealth,baseHp*.5f,"fatal hit revives at half HP");
                Check(p.IsAlive&&rtime.Revives==0,"fatal hit consumes relic revive");
                var finalSnapshot=p.CaptureRunSceneState();p.RestoreRunSceneState(finalSnapshot);
                Check(rtime.Revives==0,"real consumed revive persists across snapshot");
                yield return new WaitForSecondsRealtime(.5f);
                Debug.Log("[RelicQA] All 34 effects/catalog and transfer checks complete");
                RelicCatalogSmoke.Complete();
            }
            finally
            {
                if(had)PlayerPrefs.SetString("EquippedRelicId",old);else PlayerPrefs.DeleteKey("EquippedRelicId");
                foreach(var saved in unlocks){if(saved.has)PlayerPrefs.SetInt(saved.key,saved.value);else PlayerPrefs.DeleteKey(saved.key);}
                PlayerPrefs.Save(); CrossSceneData.CharacterBlueprint=oldCharacter;Time.timeScale=1;
            }
        }
    }
}
