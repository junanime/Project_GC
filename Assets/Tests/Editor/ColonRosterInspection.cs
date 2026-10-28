using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Vampire.Tests.Editor
{
    public static class ColonRosterInspection
    {
        public const string Output="D:/Project_GC/work/colon-roster-20261023";
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            var owner=Object.FindObjectOfType<LevelManager>();
            var source=(LevelBlueprint)new SerializedObject(owner).FindProperty("levelBlueprint").objectReferenceValue;
            var level=Object.Instantiate(source);
            var applier=Object.FindObjectOfType<LevelSpawnBalanceApplier>();
            var profile=(LevelSpawnBalanceProfile)new SerializedObject(applier).FindProperty("spawnBalanceProfile").objectReferenceValue;
            profile.ApplyTo(level);
            var report=new StringBuilder();int flat=0;
            for(int p=0;p<level.monsters.Length;p++)
            {
                var pool=level.monsters[p];report.AppendLine("POOL "+p+" "+AssetDatabase.GetAssetPath(pool.monstersPrefab)+" components="+string.Join(",",pool.monstersPrefab.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null).Select(c=>c.GetType().Name)));
                foreach(var bp in pool.monsterBlueprints)
                {
                    report.AppendLine($"FLAT {flat} {bp.name} {bp.GetType().Name} hp={bp.hp} atk={bp.atk} speed={bp.movespeed} animation={bp.walkSpriteSequence?.Length}");
                    File.WriteAllText(Output+"/bp-"+flat+".json",EditorJsonUtility.ToJson(bp,true));
                    report.AppendLine("WEIGHTS "+string.Join(" ",new[]{0f,2,4,6,8,10,12,14,15}.Select(m=>$"{m}m:{level.monsterSpawnTable.GetProbability(m/15,flat):F3}")));
                    flat++;
                }
            }
            File.WriteAllText(Output+"/roster.txt",report.ToString());
            File.WriteAllText(Output+"/effective-level.json",EditorJsonUtility.ToJson(level,true));
            File.WriteAllText(Output+"/timed-spawns.json",EditorJsonUtility.ToJson(Object.FindObjectOfType<TimedSpecialMonsterSpawner>(),true));
            File.WriteAllText(Output+"/profile.json",EditorJsonUtility.ToJson(profile,true));
            Object.DestroyImmediate(level);Debug.Log("Colon roster inspection saved: "+Output);
        }
    }
}
