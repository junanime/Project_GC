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
        [Header("Stage 1 Terrain")]
        [SerializeField] private StomachMapDefinition stomachMap;
        [SerializeField] private Inventory inventory;
        [SerializeField] private StatsManager statsManager;
        [SerializeField] private GameOverDialog gameOverDialog;
        [SerializeField] private GameTimer gameTimer;

        [Header("Result Panel")]
        [Tooltip("새 결과 패널입니다. 연결하면 GameOverDialog보다 우선 사용됩니다.")]
        [SerializeField] private LevelResultPanel levelResultPanel;

        [Header("Runtime Pause")]
        [Tooltip("혈전 미니 스테이지처럼 같은 씬 안에서 별도 콘텐츠를 진행할 때, 기본 런 시간/스폰/보스/상자 흐름을 잠시 멈출지 여부입니다.")]
        [SerializeField] private bool logRunFlowPause = true;

        private float levelTime = 0f;
        public const float SpawnPotionDuration = 30f;
        public const float SpawnPotionMultiplier = 1.5f;
        public float SpawnPotionRemaining { get; private set; }
        public void ActivateSpawnPotion() { SpawnPotionRemaining=SpawnPotionDuration; }
        private float timeSinceLastMonsterSpawned;
        private float timeSinceLastChestSpawned;
        public int NormalMonstersSpawned { get; private set; }
        private TimedSpecialMonsterSpawner fieldSpawns;
        public void NotifyExternalFinalBossSpawned() { if (fieldSpawns != null) fieldSpawns.NotifyFinalBossSpawned(); }
        private bool levelEnded = false;
        private bool runFlowPaused = false;

        public float CurrentLevelTime => levelTime;
        public float LevelDuration => levelBlueprint != null ? levelBlueprint.levelTime : 0f;
        public LevelBlueprint CurrentLevelBlueprint => levelBlueprint;
        public EntityManager EntityManager => entityManager;
        public Character PlayerCharacter => playerCharacter;
        public Inventory PlayerInventory => inventory;
        public bool InitialFieldObjectsReady => !UsesInitialFieldLayout || GetComponent<FieldObjectLayout>()?.Ready == true;
        public bool IsRunFlowPaused => runFlowPaused || !InitialFieldObjectsReady;
        public bool IsLevelEnded => levelEnded;
        public bool UsesInitialFieldLayout => stomachMap != null;

        public void Init(LevelBlueprint levelBlueprint)
        {
            if (!CrossSceneData.HasPendingRunSceneTransfer) SnailFieldProgress.Reset();
            this.levelBlueprint = levelBlueprint;

            levelTime = 0f;
            SpawnPotionRemaining = 0f;
            if(gameTimer!=null)gameTimer.SetTime(LevelDuration);
            timeSinceLastMonsterSpawned = 0f;
            timeSinceLastChestSpawned = 0f;
            fieldSpawns = FindObjectOfType<TimedSpecialMonsterSpawner>();
            if (fieldSpawns == null) fieldSpawns = gameObject.AddComponent<TimedSpecialMonsterSpawner>();
            fieldSpawns.InitializeLevel(this);
            levelEnded = false;
            runFlowPaused = false;

            entityManager.Init(
                this.levelBlueprint,
                playerCharacter,
                inventory,
                statsManager,
                infiniteBackground,
                abilitySelectionDialog
            );

            abilityManager.Init(
                this.levelBlueprint,
                entityManager,
                playerCharacter,
                abilityManager
            );

            abilitySelectionDialog.Init(
                abilityManager,
                entityManager,
                playerCharacter
            );

            playerCharacter.Init(
                entityManager,
                abilityManager,
                statsManager
            );

            playerCharacter.OnDeath.AddListener(GameOver);

            infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);
            FiniteStomachMap.Install(this, stomachMap, infiniteBackground);

            entityManager.SpawnGemsAroundPlayer(
                this.levelBlueprint.initialExpGemCount,
                this.levelBlueprint.initialExpGemType
            );

            if (!UsesInitialFieldLayout) entityManager.SpawnChest(levelBlueprint.chestBlueprint);
            inventory.Init();
            var tutorial = GetComponent<TutorialGuide>();
            if (tutorial == null) tutorial = gameObject.AddComponent<TutorialGuide>();
            tutorial.Initialize(this);
            StageProgression.Ensure(this);
            SupplyCapsuleDirector.Ensure(this);
            FieldObjectLayout.Ensure(this);
            AcidLakeEvent.Install(this);
            RunScore.Install(this);
        }

        // Preserve the actual player, abilities, inventory, relic runtimes, stats and loot pools.
        public void BeginNextStage(LevelBlueprint next)
        {
            foreach (var mini in FindObjectsOfType<MiniStageDirector>()) mini.ResetForNextField();
            entityManager.ContinueInStage(next);
            levelBlueprint = next;
            levelTime = 0f;
            NormalMonstersSpawned = 0;
            timeSinceLastMonsterSpawned = timeSinceLastChestSpawned = 0f;
            if (gameTimer != null) gameTimer.SetTime(next.levelTime);
            infiniteBackground.SetStageTexture(next.backgroundTexture);
            if (fieldSpawns != null) fieldSpawns.RestartForStage(this);
            foreach (var director in FindObjectsOfType<StageEventDirector>()) director.RestartForStage();
            foreach (var toast in FindObjectsOfType<StageEventToastUI>()) toast.ResetForStage();
            foreach (var toad in FindObjectsOfType<AcidToadSpawn>()) toad.ResetForStage();
            foreach (var altar in FindObjectsOfType<FinalBossSummonInteractable>()) altar.ResetForNextStage();
        }

        private void Start()
        {
            Init(levelBlueprint);
        }

        private void Update()
        {
            if (levelEnded)
            {
                return;
            }

            if (IsRunFlowPaused || StageEntryLoading.IsLoading)
            {
                return;
            }

            levelTime += Time.deltaTime;
            SpawnPotionRemaining=Mathf.Max(0,SpawnPotionRemaining-Time.deltaTime);

            if (gameTimer != null)
            {
                gameTimer.SetTime(Mathf.Max(0f,LevelDuration-levelTime));
            }

            HandleNormalMonsterSpawn();
            HandleChestSpawn();
        }

        public void SetRunFlowPaused(bool paused)
        {
            if (runFlowPaused == paused)
            {
                return;
            }

            runFlowPaused = paused;

            if (!runFlowPaused)
            {
                timeSinceLastMonsterSpawned = 0f;
                timeSinceLastChestSpawned = 0f;
            }

            if (logRunFlowPause)
            {
                Debug.Log($"[LevelManager] Run Flow Pause = {runFlowPaused}");
            }
        }

        private void HandleNormalMonsterSpawn()
        {
            if (levelBlueprint == null)
            {
                return;
            }

            timeSinceLastMonsterSpawned += Time.deltaTime;

            float spawnRate = GetCurrentBaseMonsterSpawnRate();
       
            // 소화효소 처치 난이도 상승 연결:
            // 선택된 난이도가 SpawnRate이면 이 배율이 1.05, 1.10 ... 식으로 증가합니다.
            spawnRate *= DigestiveEnzymeDifficultyManager.SpawnRateMultiplier;
            if(SpawnPotionRemaining>0)spawnRate*=SpawnPotionMultiplier;

            float monsterSpawnDelay = spawnRate > 0f
                ? 1.0f / spawnRate
                : float.PositiveInfinity;

            while (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
            {
                SpawnMonsterFromSpawnTable();

                timeSinceLastMonsterSpawned -= monsterSpawnDelay;
            }
        }

        private void SpawnMonsterFromSpawnTable()
        {
            if (levelBlueprint == null || levelBlueprint.monsterSpawnTable == null)
            {
                return;
            }

            float normalizedTime = GetNormalizedLevelTime();

            int monsterIndex = levelBlueprint.monsterSpawnTable.SelectMonster(normalizedTime);
            SpawnMonsterByFlatIndex(monsterIndex);
        }

        public void SpawnMonsterFromCurrentSpawnTable()
        {
            SpawnMonsterFromSpawnTable();
        }

        private void HandleChestSpawn()
        {
            if (UsesInitialFieldLayout) return;
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

                timeSinceLastChestSpawned = Mathf.Repeat(
                    timeSinceLastChestSpawned,
                    levelBlueprint.chestSpawnDelay
                );
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

            return levelBlueprint.monsterSpawnTable.GetSpawnRate(
                GetNormalizedLevelTime()
            ) * (levelTime >= 8f * 60f ? 1.3f : 1f);
        }

        public void SpawnMonsterByFlatIndex(int monsterIndex, float hpMultiplier = 1f)
        {
            if (levelBlueprint != null && entityManager != null &&
                entityManager.LivingMonsters.Count >= Mathf.Max(1, levelBlueprint.normalSpawnPopulationLimit)) return;
            if (levelBlueprint == null || entityManager == null)
            {
                Debug.LogWarning(
                    "[LevelManager] SpawnMonsterByFlatIndex 실패: LevelBlueprint 또는 EntityManager가 비어 있습니다."
                );
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

            if (blueprintIndex < 0 ||
                blueprintIndex >= levelBlueprint.monsters[poolIndex].monsterBlueprints.Length)
            {
                Debug.LogWarning($"[LevelManager] 잘못된 blueprintIndex입니다: {blueprintIndex}");
                return;
            }

            MonsterBlueprint monsterBlueprint =
                levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];

            if (monsterBlueprint == null)
            {
                Debug.LogWarning(
                    $"[LevelManager] MonsterBlueprint가 비어 있습니다. monsterIndex={monsterIndex}"
                );
                return;
            }

            // 소화효소 처치 난이도 상승 연결:
            // 선택된 난이도가 MonsterHealth이면 이 배율이 1.05, 1.10 ... 식으로 증가합니다.
            // Spawn APIs take EXTRA HP, not total HP. Only explicit event buffs apply.
            float multiplier = Mathf.Max(0f, hpMultiplier) * DigestiveEnzymeDifficultyManager.MonsterHpMultiplier;
            float extraHp = monsterBlueprint.hp * (multiplier - 1f);

            Monster spawned = entityManager.SpawnMonsterRandomPosition(poolIndex, monsterBlueprint, extraHp);
            if (spawned != null && !(monsterBlueprint is EliteMonsterBlueprint)) NormalMonstersSpawned++;
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
                        $"[MonsterIndex] flatIndex={flatIndex} | " +
                        $"poolIndex={poolIndex} | " +
                        $"blueprintIndex={blueprintIndex} | " +
                        $"name={monsterName}"
                    );

                    flatIndex++;
                }
            }

            Debug.Log("[LevelManager] Monster Index Table End");
        }

        public void GameOver()
        {
            if (playerCharacter != null && playerCharacter.IsExiting) return;
            if (levelEnded)
            {
                return;
            }

            levelEnded = true;

            SaveCoinsGained();

            GameAudioManager.EndRunAudio(false);
            RunScore.Current?.Finish(false);

            Time.timeScale = 0f;

            if (ApothecaryUI.TryShowResult(false)) return;
            if (levelResultPanel != null)
            {
                levelResultPanel.Open(false);
                return;
            }

            if (gameOverDialog != null)
            {
                gameOverDialog.Open(false, statsManager);
            }
        }

        public void LevelPassed(Monster finalBossKilled)
        {
            if (playerCharacter != null && !playerCharacter.IsAlive) return;
            if (levelEnded)
            {
                return;
            }

            levelEnded = true;

            SaveCoinsGained();
            GameAudioManager.EndRunAudio(true);
            RunScore.Current?.Finish(true);
            // Boss BGM 종료.
            // Ingame BGM은 재개하지 않음.
            // 승리 보상 효과음 재생.
            Time.timeScale = 0f;

            if (ApothecaryUI.TryShowResult(true)) return;
            if (levelResultPanel != null)
            {
                levelResultPanel.Open(true);
                return;
            }

            if (gameOverDialog != null)
            {
                gameOverDialog.Open(true, statsManager);
            }
        }

        // Complete the browser journey without opening the ordinary result panel.
        // Keep time running for the outlet camera/iris while gameplay stays paused.
        public void CompleteWebDemo()
        {
            if (levelEnded) return;
            levelEnded = true;
            SaveCoinsGained();
            GameAudioManager.EndRunAudio(true);
            RunScore.Current?.Finish(true);
            ProgressPrefs.Save();
        }

        public void AbandonRun()
        {
            if (levelEnded) return;
            levelEnded = true;
            SaveCoinsGained();
            GameAudioManager.EndRunAudio(false);
            ProgressPrefs.Save();
        }

        private void SaveCoinsGained()
        {
            if (statsManager == null)
            {
                return;
            }

            int coinCount = ProgressPrefs.GetInt("Coins");
            ProgressPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            ProgressPrefs.Save();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
