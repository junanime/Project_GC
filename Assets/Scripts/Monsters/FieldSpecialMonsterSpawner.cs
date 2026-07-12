using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 영양 도둑균, 추격형 보물 몬스터 같은 독립 필드 특수 몬스터를 생성하는 스포너입니다.
    ///
    /// 기존 일반 몬스터 풀을 건드리지 않고 Instantiate 방식으로 생성합니다.
    /// 미니 스테이지 진입 중이거나 런 흐름이 멈춘 상태에서는 스폰하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FieldSpecialMonsterSpawner : MonoBehaviour
    {
        [Serializable]
        private class SpawnEntry
        {
            [Tooltip("스폰할 필드 특수 몬스터 프리팹입니다.")]
            public FieldSpecialMonsterBase prefab;

            [Tooltip("게임 시작 후 첫 생성까지 걸리는 시간입니다.")]
            public float firstSpawnTime = 40f;

            [Tooltip("첫 생성 이후 재생성 간격입니다.")]
            public float spawnInterval = 60f;

            [Tooltip("동시에 필드에 존재할 수 있는 최대 수입니다.")]
            public int maxAliveCount = 1;

            [Tooltip("플레이어 기준 생성 거리입니다.")]
            public float spawnDistanceFromPlayer = 9f;

            [Tooltip("생성 거리의 랜덤 오차입니다.")]
            public float spawnDistanceJitter = 1.5f;

            [Tooltip("이 항목의 스폰을 활성화할지 여부입니다.")]
            public bool enabled = true;

            [NonSerialized] public float timer;
            [NonSerialized] public readonly List<FieldSpecialMonsterBase> alive = new List<FieldSpecialMonsterBase>();
        }

        [Header("References")]
        [Tooltip("현재 스테이지의 LevelManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("EntityManager입니다. 비워두면 LevelManager에서 가져오거나 자동으로 찾습니다.")]
        [SerializeField] private EntityManager entityManager;

        [Tooltip("플레이어 캐릭터입니다. 비워두면 LevelManager에서 가져오거나 자동으로 찾습니다.")]
        [SerializeField] private Character playerCharacter;

        [Header("Spawn Entries")]
        [Tooltip("생성할 필드 특수 몬스터 목록입니다. 영양 도둑균과 보물 몬스터를 각각 등록하세요.")]
        [SerializeField] private List<SpawnEntry> spawnEntries = new List<SpawnEntry>();

        [Header("Blocking")]
        [Tooltip("LevelManager.IsRunFlowPaused가 true일 때 스폰 타이머를 멈춥니다.")]
        [SerializeField] private bool blockWhileRunFlowPaused = true;

        [Tooltip("MiniStageRuntimeState.IsInsideMiniStage가 true일 때 스폰하지 않습니다.")]
        [SerializeField] private bool blockInsideMiniStage = true;

        [Header("Debug")]
        [Tooltip("스폰 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private void Awake()
        {
            ResolveReferences();
            InitializeTimers();
        }

        private void Update()
        {
            ResolveReferences();

            if (ShouldBlockSpawn())
            {
                return;
            }

            for (int i = 0; i < spawnEntries.Count; i++)
            {
                UpdateEntry(spawnEntries[i]);
            }
        }

        /// <summary>
        /// FieldSpecialMonsterBase가 사망/소멸/Destroy될 때 호출하는 제거 알림입니다.
        /// 이 메서드가 없어서 현재 컴파일 오류가 발생한 것입니다.
        /// </summary>
        public void NotifySpecialMonsterRemoved(FieldSpecialMonsterBase monster)
        {
            if (monster == null)
            {
                return;
            }

            for (int i = 0; i < spawnEntries.Count; i++)
            {
                SpawnEntry entry = spawnEntries[i];

                if (entry == null)
                {
                    continue;
                }

                entry.alive.Remove(monster);
            }
        }

        private void InitializeTimers()
        {
            for (int i = 0; i < spawnEntries.Count; i++)
            {
                SpawnEntry entry = spawnEntries[i];

                if (entry == null)
                {
                    continue;
                }

                entry.timer = 0f;
            }
        }

        private void UpdateEntry(SpawnEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (!entry.enabled)
            {
                return;
            }

            if (entry.prefab == null)
            {
                return;
            }

            CleanupEntry(entry);

            int maxAlive = Mathf.Max(1, entry.maxAliveCount);

            if (entry.alive.Count >= maxAlive)
            {
                return;
            }

            entry.timer += Time.deltaTime;

            float requiredTime = entry.firstSpawnTime;

            if (entry.timer > entry.firstSpawnTime)
            {
                requiredTime = entry.spawnInterval;
            }

            if (entry.timer < requiredTime)
            {
                return;
            }

            Spawn(entry);
            entry.timer = 0f;
        }

        private void Spawn(SpawnEntry entry)
        {
            if (entry == null || entry.prefab == null)
            {
                return;
            }

            if (playerCharacter == null)
            {
                return;
            }

            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.right;
            }

            float distance = Mathf.Max(
                1f,
                entry.spawnDistanceFromPlayer + UnityEngine.Random.Range(
                    -entry.spawnDistanceJitter,
                    entry.spawnDistanceJitter));

            Vector2 spawnPosition = (Vector2)playerCharacter.transform.position + direction * distance;

            FieldSpecialMonsterBase monster = Instantiate(
                entry.prefab,
                spawnPosition,
                Quaternion.identity);

            monster.SetupRuntime(this, entityManager, playerCharacter);
            entry.alive.Add(monster);

            if (debugLog)
            {
                Debug.Log(
                    $"[FieldSpecialMonsterSpawner] 특수 몬스터 생성: {entry.prefab.name}, pos={spawnPosition}",
                    this);
            }
        }

        private void CleanupEntry(SpawnEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            for (int i = entry.alive.Count - 1; i >= 0; i--)
            {
                FieldSpecialMonsterBase monster = entry.alive[i];

                if (monster == null || !monster.IsAlive)
                {
                    entry.alive.RemoveAt(i);
                }
            }
        }

        private bool ShouldBlockSpawn()
        {
            if (blockInsideMiniStage && MiniStageRuntimeState.IsInsideMiniStage)
            {
                return true;
            }

            if (blockWhileRunFlowPaused && levelManager != null && levelManager.IsRunFlowPaused)
            {
                return true;
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (levelManager != null)
            {
                if (entityManager == null)
                {
                    entityManager = levelManager.EntityManager;
                }

                if (playerCharacter == null)
                {
                    playerCharacter = levelManager.PlayerCharacter;
                }
            }

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }
        }
    }
}