using UnityEngine;
using UnityEngine.SceneManagement;
namespace Vampire
{
    public sealed class SnailBossDebugSpawn : MonoBehaviour
    {
        float nextFieldWave=15;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded-=OnScene;SceneManager.sceneLoaded+=OnScene;OnScene(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }
        static void OnScene(Scene scene,LoadSceneMode mode)
        {
            if(FindObjectOfType<LevelManager>()!=null&&FindObjectOfType<SnailBossDebugSpawn>()==null)
                new GameObject("Insert final boss shortcut").AddComponent<SnailBossDebugSpawn>();
        }
        void Update()
        {
            if(GameInput.GetKeyDown(KeyCode.Insert))TrySpawn();
            var level=FindObjectOfType<LevelManager>();
            if(level==null||SnailBossRuntime.Paused||level.IsRunFlowPaused||level.IsLevelEnded)return;
            if(level.CurrentLevelTime>=nextFieldWave)
            {nextFieldWave=level.CurrentLevelTime+35;SpawnFieldMinis(level);}
        }
        public static int SpawnFieldMinis(LevelManager level)
        {
            if(level==null||level.PlayerCharacter==null||FindObjectOfType<SnailBossRuntime>()!=null)return 0;
            int living=0;foreach(var m in level.EntityManager.LivingMonsters)if(m is AcidLeechMonster)living++;
            var entries=level.CurrentLevelBlueprint.monsters;
            for(int i=0;i<entries.Length;i++)
            {
                if(entries[i].monstersPrefab==null||entries[i].monstersPrefab.GetComponent<AcidLeechMonster>()==null||entries[i].monsterBlueprints.Length==0)continue;
                int count=Mathf.Clamp(12-living,0,3);
                for(int n=0;n<count;n++)
                {
                    Vector2 pos=(Vector2)level.PlayerCharacter.transform.position+Random.insideUnitCircle.normalized*Random.Range(6f,9f);
                    level.EntityManager.SpawnMonster(i,pos,entries[i].monsterBlueprints[0]);
                }
                return count;
            }
            return 0;
        }
        public static bool TrySpawn()
        {
            if(SnailBossRuntime.Paused||FindObjectOfType<SnailBossRuntime>()!=null||FinalBossSummonInteractable.IsSummoning)return false;
            var level=FindObjectOfType<LevelManager>();if(level==null||level.IsLevelEnded||level.IsRunFlowPaused)return false;
            var terminal=FindObjectOfType<FinalBossSummonInteractable>();if(terminal!=null)return terminal.TryAutomaticSummon();
            if(level.PlayerCharacter==null)return false;
            var boss=level.EntityManager.SpawnFinalBoss(level.CurrentLevelBlueprint,(Vector2)level.PlayerCharacter.transform.position+Vector2.up*7);
            if(boss==null)return false;level.NotifyExternalFinalBossSpawned();GameAudioManager.StartBossAudio();return true;
        }
    }
}
