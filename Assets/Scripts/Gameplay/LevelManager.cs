using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelBlueprint levelBlueprint;
        [SerializeField] private Character playerCharacter;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private AbilitySelectionDialog abilitySelectionDialog;
        [SerializeField] private InfiniteBackground infiniteBackground;
        [SerializeField] private Inventory inventory;
        [SerializeField] private StatsManager statsManager;
        [SerializeField] private GameOverDialog gameOverDialog;
        [SerializeField] private GameTimer gameTimer;

        private float levelTime = 0f;
        private float timeSinceLastMonsterSpawned;
        private float timeSinceLastChestSpawned;
        private bool miniBossSpawned = false;
        private bool finalBossSpawned = false;

        public float CurrentLevelTime => levelTime;
        public float LevelDuration => levelBlueprint != null ? levelBlueprint.levelTime : 0f;
        public LevelBlueprint CurrentLevelBlueprint => levelBlueprint;
        public EntityManager EntityManager => entityManager;
        public Character PlayerCharacter => playerCharacter;

        public void Init(LevelBlueprint levelBlueprint)
        {
            this.levelBlueprint = levelBlueprint;
            levelTime = 0f;

            entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);

            abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);
            abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);

            playerCharacter.Init(entityManager, abilityManager, statsManager);
            playerCharacter.OnDeath.AddListener(GameOver);

            entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount, this.levelBlueprint.initialExpGemType);

            entityManager.SpawnChest(levelBlueprint.chestBlueprint);

            infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);

            inventory.Init();
        }

        private void Start()
        {
            Init(levelBlueprint);
        }

        private void Update()
        {
            levelTime += Time.deltaTime;
            gameTimer.SetTime(levelTime);

            HandleNormalMonsterSpawn();
            HandleBossSpawn();
            HandleChestSpawn();
        }

        private void HandleNormalMonsterSpawn()
        {
            if (levelBlueprint == null)
            {
                return;
            }

            if (levelTime >= levelBlueprint.levelTime)
            {
                return;
            }

            timeSinceLastMonsterSpawned += Time.deltaTime;

            float spawnRate = GetCurrentBaseMonsterSpawnRate();
            float monsterSpawnDelay = spawnRate > 0f ? 1.0f / spawnRate : float.PositiveInfinity;

            if (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
            {
                SpawnMonsterFromSpawnTable();
                timeSinceLastMonsterSpawned = Mathf.Repeat(timeSinceLastMonsterSpawned, monsterSpawnDelay);
            }
        }

        private void SpawnMonsterFromSpawnTable()
        {
            if (levelBlueprint == null || levelBlueprint.monsterSpawnTable == null)
            {
                return;
            }

            float normalizedTime = GetNormalizedLevelTime();

            (int monsterIndex, float hpMultiplier) =
                levelBlueprint.monsterSpawnTable.SelectMonsterWithHPMultiplier(normalizedTime);

            SpawnMonsterByFlatIndex(monsterIndex, hpMultiplier);
        }

        private void HandleBossSpawn()
        {
            if (levelBlueprint == null)
            {
                return;
            }

            if (!miniBossSpawned &&
                levelBlueprint.miniBosses != null &&
                levelBlueprint.miniBosses.Length > 0 &&
                levelTime > levelBlueprint.miniBosses[0].spawnTime)
            {
                miniBossSpawned = true;
                entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, levelBlueprint.miniBosses[0].bossBlueprint);
            }

            if (!finalBossSpawned && levelTime > levelBlueprint.levelTime)
            {
                finalBossSpawned = true;
                Monster finalBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, levelBlueprint.finalBoss.bossBlueprint);
                finalBoss.OnKilled.AddListener(LevelPassed);
            }
        }

        private void HandleChestSpawn()
        {
            if (levelBlueprint == null)
            {
                return;
            }

            timeSinceLastChestSpawned += Time.deltaTime;

            if (timeSinceLastChestSpawned >= levelBlueprint.chestSpawnDelay)
            {
                for (int i = 0; i < levelBlueprint.chestSpawnAmount; i++)
                {
                    entityManager.SpawnChest(levelBlueprint.chestBlueprint);
                }

                timeSinceLastChestSpawned = Mathf.Repeat(timeSinceLastChestSpawned, levelBlueprint.chestSpawnDelay);
            }
        }

        public float GetNormalizedLevelTime()
        {
            if (levelBlueprint == null || levelBlueprint.levelTime <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(levelTime / levelBlueprint.levelTime);
        }

        public float GetCurrentBaseMonsterSpawnRate()
        {
            if (levelBlueprint == null || levelBlueprint.monsterSpawnTable == null)
            {
                return 0f;
            }

            return levelBlueprint.monsterSpawnTable.GetSpawnRate(GetNormalizedLevelTime());
        }

        public void SpawnMonsterByFlatIndex(int monsterIndex, float hpMultiplier = 1f)
        {
            if (levelBlueprint == null || entityManager == null)
            {
                Debug.LogWarning("[LevelManager] SpawnMonsterByFlatIndex 실패: LevelBlueprint 또는 EntityManager가 비어 있습니다.");
                return;
            }

            if (!levelBlueprint.MonsterIndexMap.ContainsKey(monsterIndex))
            {
                Debug.LogWarning($"[LevelManager] 잘못된 monsterIndex입니다: {monsterIndex}");
                return;
            }

            (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];

            if (poolIndex < 0 || poolIndex >= levelBlueprint.monsters.Length)
            {
                Debug.LogWarning($"[LevelManager] 잘못된 poolIndex입니다: {poolIndex}");
                return;
            }

            if (blueprintIndex < 0 || blueprintIndex >= levelBlueprint.monsters[poolIndex].monsterBlueprints.Length)
            {
                Debug.LogWarning($"[LevelManager] 잘못된 blueprintIndex입니다: {blueprintIndex}");
                return;
            }

            MonsterBlueprint monsterBlueprint = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];

            if (monsterBlueprint == null)
            {
                Debug.LogWarning($"[LevelManager] MonsterBlueprint가 비어 있습니다. monsterIndex={monsterIndex}");
                return;
            }

            entityManager.SpawnMonsterRandomPosition(
                poolIndex,
                monsterBlueprint,
                monsterBlueprint.hp * hpMultiplier
            );
        }

        public void SpawnRandomMonsterFromFlatIndexList(List<int> monsterIndices, float hpMultiplier = 1f)
        {
            if (monsterIndices == null || monsterIndices.Count == 0)
            {
                return;
            }

            int selectedIndex = monsterIndices[Random.Range(0, monsterIndices.Count)];
            SpawnMonsterByFlatIndex(selectedIndex, hpMultiplier);
        }

        public void LogMonsterIndexTable()
        {
            if (levelBlueprint == null)
            {
                Debug.LogWarning("[LevelManager] LevelBlueprint가 비어 있습니다.");
                return;
            }

            Debug.Log("[LevelManager] Monster Index Table Start");

            int flatIndex = 0;

            for (int poolIndex = 0; poolIndex < levelBlueprint.monsters.Length; poolIndex++)
            {
                LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

                if (container == null || container.monsterBlueprints == null)
                {
                    continue;
                }

                for (int blueprintIndex = 0; blueprintIndex < container.monsterBlueprints.Length; blueprintIndex++)
                {
                    MonsterBlueprint blueprint = container.monsterBlueprints[blueprintIndex];
                    string monsterName = blueprint != null ? blueprint.name : "NULL";

                    Debug.Log(
                        $"[MonsterIndex] flatIndex={flatIndex} | poolIndex={poolIndex} | blueprintIndex={blueprintIndex} | name={monsterName}"
                    );

                    flatIndex++;
                }
            }

            Debug.Log("[LevelManager] Monster Index Table End");
        }

        public void GameOver()
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            gameOverDialog.Open(false, statsManager);
        }

        public void LevelPassed(Monster finalBossKilled)
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            gameOverDialog.Open(true, statsManager);
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }
    }
}