using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 상호작용 시 현재 일반 몬스터 스폰 테이블에 비례해서
    /// 대응되는 엘리트 몬스터를 여러 마리 소환하는 오브젝트.
    /// </summary>
    public class EliteMonsterSummonInteractable : InteractableEventObject
    {
        [Serializable]
        public class NormalToEliteMapping
        {
            [Tooltip("일반 몬스터 flatIndex")]
            public int normalFlatIndex;

            [Tooltip("대응되는 엘리트 몬스터 flatIndex")]
            public int eliteFlatIndex;
        }

        [Header("Elite Spawn Count")]
        [SerializeField] private int minSpawnCount = 3;
        [SerializeField] private int maxSpawnCount = 5;

        [Tooltip("true면 현재 플레이 시간에 비례해서 최소~최대 수 사이로 소환합니다.")]
        [SerializeField] private bool scaleCountByLevelTime = true;

        [Header("Spawn Position")]
        [Tooltip("오브젝트 주변에 엘리트가 생성되는 반경입니다.")]
        [SerializeField] private float spawnRadius = 5f;

        [Tooltip("플레이어 바로 위에 겹쳐 스폰되는 것을 막기 위한 최소 거리입니다.")]
        [SerializeField] private float minDistanceFromPlayer = 2.5f;

        [Header("Selection")]
        [Tooltip("현재 일반 몬스터 스폰 테이블에 맞춰 엘리트를 고릅니다.")]
        [SerializeField] private bool followCurrentNormalSpawnTable = true;

        [Tooltip("스폰 테이블에서 뽑힌 일반 몬스터가 매핑에 없을 때 다시 뽑는 최대 시도 횟수입니다.")]
        [SerializeField] private int maxSelectionAttempts = 20;

        [Tooltip("followCurrentNormalSpawnTable이 꺼져 있거나 선택 실패 시 매핑 목록에서 랜덤으로 고릅니다.")]
        [SerializeField] private bool fallbackRandomMappedElite = true;

        [Header("Mappings")]
        [SerializeField] private List<NormalToEliteMapping> normalToEliteMappings = new List<NormalToEliteMapping>();

        [Header("Spawn Timing")]
        [Tooltip("여러 마리를 한 번에 생성하지 않고 약간의 간격을 두고 생성합니다.")]
        [SerializeField] private float intervalBetweenSpawns = 0.12f;

        [Header("HP Buff")]
        [Tooltip("추가 HP 보정입니다. 보통 0으로 둡니다.")]
        [SerializeField] private float hpBuff = 0f;

        private Dictionary<int, int> mappingDictionary;

        protected override bool ExecuteInteraction(Character player)
        {
            if (levelManager == null)
            {
                Debug.LogError("[EliteMonsterSummonInteractable] LevelManager를 찾지 못했습니다.", this);
                return false;
            }

            if (levelManager.EntityManager == null)
            {
                Debug.LogError("[EliteMonsterSummonInteractable] EntityManager가 비어 있습니다.", this);
                return false;
            }

            if (levelManager.CurrentLevelBlueprint == null)
            {
                Debug.LogError("[EliteMonsterSummonInteractable] CurrentLevelBlueprint가 비어 있습니다.", this);
                return false;
            }

            BuildMappingDictionary();

            if (mappingDictionary == null || mappingDictionary.Count == 0)
            {
                Debug.LogError("[EliteMonsterSummonInteractable] Normal To Elite Mapping이 비어 있습니다.", this);
                return false;
            }

            int aliveElites = 0;
            foreach(var m in levelManager.EntityManager.LivingMonsters)
                if(m != null && m.HP > 0 && m.Blueprint is EliteMonsterBlueprint) aliveElites++;
            int spawnCount = Mathf.Min(GetSpawnCountByTime(), Mathf.Max(0, 8-aliveElites));
            if(spawnCount == 0) return false;
            var requests = PlanWave(spawnCount, player);
            if(requests.Count == 0) return false;
            // CompleteInteraction disables (and the field spawner may destroy) this altar.
            // Capture the wave now; its coroutine must be owned by the surviving level.
            levelManager.StartCoroutine(SpawnPlannedWave(levelManager, levelManager.CurrentLevelBlueprint, requests, intervalBetweenSpawns));

            if (debugLog)
            {
                Debug.Log($"[EliteMonsterSummonInteractable] 엘리트 소환 시작 | Count={spawnCount}", this);
            }

            return true;
        }

        struct WaveSpawn
        {
            public int pool;
            public MonsterBlueprint blueprint;
            public Vector2 position;
            public float hp;
        }

        List<WaveSpawn> PlanWave(int count, Character player)
        {
            var result = new List<WaveSpawn>();
            var used = new HashSet<int>();
            var blueprint = levelManager.CurrentLevelBlueprint;
            // Melee elites provide readable groups. Avoid stacking ranged, trap or miniboss skills.
            var eligible = new List<int>();
            foreach(var pair in blueprint.MonsterIndexMap)
            {
                var container=blueprint.monsters[pair.Value.Item1];
                var elite=container.monsterBlueprints[pair.Value.Item2] as EliteMonsterBlueprint;
                if(elite!=null && container.monstersPrefab.GetComponent<MeleeMonster>()!=null)
                {
                    // The large hopping cookie joins the middle/late groups.
                    if(container.monstersPrefab.name.Contains("Dubai") && levelManager.CurrentLevelTime<360)continue;
                    eligible.Add(pair.Key);
                }
            }
            for(int i=0;i<count;i++)
            {
                int index=SelectEliteFlatIndex();
                if(!eligible.Contains(index)||used.Contains(index))
                {
                    var available=eligible.FindAll(n=>!used.Contains(n));
                    if(available.Count==0)break;
                    index=available[UnityEngine.Random.Range(0,available.Count)];
                }
                var pair=blueprint.MonsterIndexMap[index];
                Vector2 pos=Vector2.zero;bool placed=false;
                for(int attempt=0;attempt<40;attempt++)
                {
                    pos=FiniteStomachMap.SafeSpawn(GetSpawnPosition(player),1.25f);
                    if(player!=null&&Vector2.Distance(pos,player.transform.position)<minDistanceFromPlayer)continue;
                    if(result.Exists(r=>Vector2.Distance(r.position,pos)<2f))continue;
                    placed=true;break;
                }
                if(!placed)continue;
                used.Add(index);
                result.Add(new WaveSpawn{pool=pair.Item1,blueprint=blueprint.monsters[pair.Item1].monsterBlueprints[pair.Item2],position=pos,hp=hpBuff});
            }
            return result;
        }

        static IEnumerator SpawnPlannedWave(LevelManager owner, LevelBlueprint stage, List<WaveSpawn> requests, float interval)
        {
            foreach(var r in requests)
            {
                while(owner!=null && !owner.IsLevelEnded && owner.CurrentLevelBlueprint==stage && (owner.IsRunFlowPaused||MiniStageRuntimeState.IsInsideMiniStage))yield return null;
                if(owner==null||owner.IsLevelEnded||owner.CurrentLevelBlueprint!=stage||!owner.PlayerCharacter.IsAlive)yield break;
                // Two nearby altars may be activated before either wave finishes.
                int alive=0;
                foreach(var monster in owner.EntityManager.LivingMonsters)
                    if(monster!=null&&monster.HP>0&&monster.Blueprint is EliteMonsterBlueprint)alive++;
                if(alive>=8)yield break;
                owner.EntityManager.SpawnMonster(r.pool,r.position,r.blueprint,r.hp);
                if(interval>0)yield return new WaitForSeconds(interval);
            }
        }

        private int GetSpawnCountByTime()
        {
            int min = Mathf.Max(0, minSpawnCount);
            int max = Mathf.Max(min, maxSpawnCount);

            if (!scaleCountByLevelTime || levelManager == null || levelManager.LevelDuration <= 0f)
            {
                return min;
            }

            float normalizedTime = Mathf.Clamp01(levelManager.CurrentLevelTime / levelManager.LevelDuration);

            return Mathf.RoundToInt(Mathf.Lerp(min, max, normalizedTime));
        }

        private void BuildMappingDictionary()
        {
            mappingDictionary = new Dictionary<int, int>();

            if (normalToEliteMappings == null)
            {
                return;
            }

            for (int i = 0; i < normalToEliteMappings.Count; i++)
            {
                NormalToEliteMapping mapping = normalToEliteMappings[i];

                if (mapping == null)
                {
                    continue;
                }

                if (mapping.normalFlatIndex < 0 || mapping.eliteFlatIndex < 0)
                {
                    continue;
                }

                mappingDictionary[mapping.normalFlatIndex] = mapping.eliteFlatIndex;
            }
            // Flat indices changed as new monsters were added. Prefer the actual
            // source blueprint so a slime selection cannot silently become a chicken.
            var stage=levelManager.CurrentLevelBlueprint;
            foreach(var elitePair in stage.MonsterIndexMap)
            {
                var elite=stage.monsters[elitePair.Value.Item1].monsterBlueprints[elitePair.Value.Item2] as EliteMonsterBlueprint;
                if(elite==null||elite.sourceNormalBlueprint==null||!elite.useSourceVisual)continue;
                foreach(var normalPair in stage.MonsterIndexMap)
                    if(stage.monsters[normalPair.Value.Item1].monsterBlueprints[normalPair.Value.Item2]==elite.sourceNormalBlueprint)
                        mappingDictionary[normalPair.Key]=elitePair.Key;
            }
        }

        private int SelectEliteFlatIndex()
        {
            if (followCurrentNormalSpawnTable && TrySelectEliteFromCurrentNormalTable(out int selectedEliteIndex))
            {
                return selectedEliteIndex;
            }

            if (fallbackRandomMappedElite)
            {
                return SelectRandomMappedElite();
            }

            return -1;
        }

        private bool TrySelectEliteFromCurrentNormalTable(out int eliteFlatIndex)
        {
            eliteFlatIndex = -1;

            LevelBlueprint levelBlueprint = levelManager.CurrentLevelBlueprint;

            if (levelBlueprint == null || levelBlueprint.monsterSpawnTable == null)
            {
                return false;
            }

            float normalizedTime = levelManager.GetNormalizedLevelTime();

            int attempts = Mathf.Max(1, maxSelectionAttempts);

            for (int i = 0; i < attempts; i++)
            {
                int normalFlatIndex = levelBlueprint.monsterSpawnTable.SelectMonster(normalizedTime);

                if (normalFlatIndex < 0)
                {
                    continue;
                }

                if (mappingDictionary.TryGetValue(normalFlatIndex, out eliteFlatIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private int SelectRandomMappedElite()
        {
            if (normalToEliteMappings == null || normalToEliteMappings.Count == 0)
            {
                return -1;
            }

            List<int> validEliteIndices = new List<int>();

            for (int i = 0; i < normalToEliteMappings.Count; i++)
            {
                NormalToEliteMapping mapping = normalToEliteMappings[i];

                if (mapping != null && mapping.eliteFlatIndex >= 0)
                {
                    validEliteIndices.Add(mapping.eliteFlatIndex);
                }
            }

            if (validEliteIndices.Count == 0)
            {
                return -1;
            }

            return validEliteIndices[UnityEngine.Random.Range(0, validEliteIndices.Count)];
        }

        private void SpawnEliteByFlatIndex(int eliteFlatIndex, Character player)
        {
            LevelBlueprint levelBlueprint = levelManager.CurrentLevelBlueprint;

            if (levelBlueprint == null)
            {
                return;
            }

            if (!levelBlueprint.MonsterIndexMap.ContainsKey(eliteFlatIndex))
            {
                Debug.LogWarning($"[EliteMonsterSummonInteractable] 잘못된 eliteFlatIndex: {eliteFlatIndex}", this);
                return;
            }

            (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[eliteFlatIndex];

            if (poolIndex < 0 || poolIndex >= levelBlueprint.monsters.Length)
            {
                Debug.LogWarning($"[EliteMonsterSummonInteractable] 잘못된 poolIndex: {poolIndex}", this);
                return;
            }

            LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

            if (container == null ||
                container.monsterBlueprints == null ||
                blueprintIndex < 0 ||
                blueprintIndex >= container.monsterBlueprints.Length)
            {
                Debug.LogWarning($"[EliteMonsterSummonInteractable] 잘못된 blueprintIndex: {blueprintIndex}", this);
                return;
            }

            MonsterBlueprint monsterBlueprint = container.monsterBlueprints[blueprintIndex];

            if (monsterBlueprint == null)
            {
                Debug.LogWarning($"[EliteMonsterSummonInteractable] MonsterBlueprint가 비어 있습니다. eliteFlatIndex={eliteFlatIndex}", this);
                return;
            }

            Vector2 spawnPosition = GetSpawnPosition(player);

            Monster spawnedMonster = levelManager.EntityManager.SpawnMonster(
                poolIndex,
                spawnPosition,
                monsterBlueprint,
                hpBuff
            );

            if (debugLog)
            {
                Debug.Log(
                    $"[EliteMonsterSummonInteractable] 엘리트 소환 | " +
                    $"EliteFlatIndex={eliteFlatIndex} | PoolIndex={poolIndex} | " +
                    $"Blueprint={monsterBlueprint.name} | Position={spawnPosition} | " +
                    $"Monster={(spawnedMonster != null ? spawnedMonster.name : "NULL")}",
                    this
                );
            }
        }

        private Vector2 GetSpawnPosition(Character player)
        {
            Vector2 center = transform.position;
            Character targetPlayer = player != null ? player : levelManager.PlayerCharacter;

            for (int i = 0; i < 20; i++)
            {
                Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;

                if (randomDirection == Vector2.zero)
                {
                    randomDirection = Vector2.up;
                }

                Vector2 position =
                    center +
                    randomDirection * UnityEngine.Random.Range(0.5f, Mathf.Max(0.5f, spawnRadius));

                if (targetPlayer == null)
                {
                    return position;
                }

                float distanceToPlayer =
                    Vector2.Distance(position, targetPlayer.transform.position);

                if (distanceToPlayer >= minDistanceFromPlayer)
                {
                    return position;
                }
            }

            if (targetPlayer != null)
            {
                Vector2 awayFromPlayer =
                    ((Vector2)transform.position - (Vector2)targetPlayer.transform.position).normalized;

                if (awayFromPlayer == Vector2.zero)
                {
                    awayFromPlayer = Vector2.up;
                }

                return (Vector2)transform.position + awayFromPlayer * minDistanceFromPlayer;
            }

            return transform.position;
        }
    }
}
