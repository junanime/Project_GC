using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static class FestivalRosterChecks
    {
        static void Check(bool ok, string message) => FestivalQaSmoke.Check(ok, message);
        public static IEnumerator Run(LevelManager level)
        {
            var run = level.GetComponent<FestivalRunController>();
            var clock = typeof(LevelManager).GetField("levelTime", BindingFlags.Instance | BindingFlags.NonPublic);
            float previous = level.CurrentLevelTime;
            bool paused = level.IsRunFlowPaused;
            level.SetRunFlowPaused(true);
            var originals = level.CurrentLevelBlueprint.monsters.SelectMany(p => p.monsterBlueprints).ToArray();
            string snapshot = string.Join("|", originals.Select(b => JsonUtility.ToJson(b)));
            string[][] expected = { new[] { "1", "1_1", "SugarCube_Normal_Body" },
                new[] { "2", "3", "3_1" }, new[] { "4", "king-oyster", "red-cap" } };
            try
            {
                foreach(float seconds in new[] {0f,11.999f,12f,23.999f,24f,36f,44.99f})
                {
                    int phase = seconds < 12 ? 0 : seconds < 24 ? 1 : 2;
                    for(int j = 0; j < 3; j++)
                    {
                        int flat = run.SelectNormal(seconds, (j+.5f)/3f);
                        Check(((Object)originals[flat]).name == expected[phase][j], "normal phase boundary "+seconds+" / "+j);
                    }
                }
                for(int phase=0; phase<3; phase++)
                for(int j=0; j<3; j++)
                {
                    clock.SetValue(level, phase*12f);
                    int flat = run.SelectNormal(phase*12f, (j+.5f)/3f);
                    var source = originals[flat]; var copy = run.ResolveNormal(source);
                    Check(copy != source && copy.hp == FestivalDemo.Settings.normalBaseHealth*Mathf.Pow(1.25f,phase), "isolated species HP "+copy.name);
                    level.SpawnMonsterByFlatIndex(flat);
                    var spawned = level.EntityManager.LivingMonsters.LastOrDefault(m => m.Blueprint==copy);
                    Check(spawned!=null && Mathf.Approximately(spawned.SpawnMaxHealth,copy.hp) && Mathf.Abs(spawned.moveSpeed-copy.movespeed)<=.101f,
                        "real pooled spawn uses species HP/speed "+copy.name);
                    float hp = spawned.SpawnMaxHealth, speed = spawned.moveSpeed, damage = copy.atk;
                    clock.SetValue(level, 44f);
                    Check(spawned.SpawnMaxHealth==hp && spawned.moveSpeed==speed && copy.atk==damage,
                        "old species keeps stats after phase change "+copy.name);
                    if (copy is SugarCubeMonsterBlueprint sugar)
                    {
                        Check(!sugar.CanSplit() && sugar.GetEffectiveWalkSpriteSequence().Length==24 &&
                            sugar.GetEffectiveWalkSpriteSequence()[0].name.StartsWith("Trio_"), "three-piece sugar walk, no cube stack or split");
                        int normal=RunScore.Current.Data.normal, count=level.EntityManager.LivingMonsters.Count;
                        spawned.TakeDamage(100000);
                        yield return null;
                        Check(RunScore.Current.Data.normal==normal+1 && !level.EntityManager.LivingMonsters.Any(m=>m.Blueprint==sugar.splitChildBlueprint),
                            "sugar death awards normal kill without children");
                    }
                    else spawned.DespawnForStageTransfer();
                    yield return null;
                }
                Check(snapshot==string.Join("|", originals.Select(b=>JsonUtility.ToJson(b))), "source monster assets unchanged by festival");
            }
            finally { clock.SetValue(level, previous); level.SetRunFlowPaused(paused); }
        }
    }
}
