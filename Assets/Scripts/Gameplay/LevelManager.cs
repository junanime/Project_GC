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
        public const float HourglassDuration = 20f;
        public const float HourglassSpawnRate = 1.20f;
        public const float HourglassMonsterSpeed = 1.20f;
        public const float HourglassPlayerSpeed = 1.15f;
        public const float HourglassAttackSpeed = 1.12f;
        public float HourglassRemaining { get; private set; }
        public float HourglassExtraStageTime { get; private set; }

        public bool ActivateHourglass()
        {
            if (levelEnded || IsRunFlowPaused || Time.timeScale <= 0f || StageEntryLoading.IsLoading || MiniStageRuntimeState.IsInsideMiniStage ||
                playerCharacter == null || !playerCharacter.IsAlive || HourglassRemaining > 0f)
                return false;
            HourglassRemaining = HourglassDuration;
            ApplyHourglassPace(true);
            return true;
        }

        void ApplyHourglassPace(bool active)
        {
            if (playerCharacter == null || playerCharacter.HourglassActive == active) return;
            playerCharacter.SetHourglassPace(active);
            if (entityManager == null || entityManager.LivingMonsters == null) return;
            foreach (var monster in entityManager.LivingMonsters)
                if (monster != null && monster.IsAlive && monster.UsesHourglassPace) monster.RefreshMovementDrag();
        }
        private float timeSinceLastMonsterSpawned;
        private int bonusWaveTenths;
        private float timeSinceLastChestSpawned;
        public int NormalMonstersSpawned { get; private set; }
        private TimedSpecialMonsterSpawner fieldSpawns;
        public void NotifyExternalFinalBossSpawned() { if (fieldSpawns != null) fieldSpawns.NotifyFinalBossSpawned(); }
        private bool levelEnded = false;
        private bool runFlowPaused = false;

        public float CurrentLevelTime => levelTime;
        public float LevelDuration => levelBlueprint != null ? levelBlueprint.levelTime : 0f;
        readonly GStarWaveFormation waveFormation = new GStarWaveFormation();

        public void ReserveOverchargeLanes(BloodClotOvercharge clot)
        {
            if (GStarDemo.Enabled) waveFormation.Reserve(this, clot);
        }

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
            bonusWaveTenths = 0;
            waveFormation.Reset();
            HourglassRemaining = HourglassExtraStageTime = 0f;
            ApplyHourglassPace(false);
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

            StageGrowthReset.CaptureLaunch(this, abilityManager);

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
            ConstipationKingDirector.Ensure(this);
            SupplyCapsuleDirector.Ensure(this);
            FieldObjectLayout.Ensure(this);
            AcidLakeEvent.Install(this);
            RunScore.Install(this);
        }

        // Transfers restore the launch combat loadout first; score and currency remain run-wide.
        public void BeginNextStage(LevelBlueprint next)
        {
            foreach (var mini in FindObjectsOfType<MiniStageDirector>()) mini.ResetForNextField();
            entityManager.ContinueInStage(next);
            levelBlueprint = next;
            levelTime = 0f;
            bonusWaveTenths = 0;
            waveFormation.Reset();
            NormalMonstersSpawned = 0;
            HourglassRemaining = HourglassExtraStageTime = 0f;
            ApplyHourglassPace(false);
            timeSinceLastMonsterSpawned = timeSinceLastChestSpawned = 0f;
            if (gameTimer != null) gameTimer.SetTime(next.levelTime);
            infiniteBackground.SetStageTexture(next.backgroundTexture);
            if (fieldSpawns != null) fieldSpawns.RestartForStage(this);
            foreach (var director in FindObjectsOfType<StageEventDirector>()) director.RestartForStage();
            foreach (var toast in FindObjectsOfType<StageEventToastUI>()) toast.ResetForStage();
            foreach (var toad in FindObjectsOfType<AcidToadSpawn>()) toad.ResetForStage();
            foreach (var altar in FindObjectsOfType<FinalBossSummonInteractable>()) altar.ResetForNextStage();
            GameAudioManager.BeginNextStageMusic();
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

            float dt = Time.deltaTime;
            float acceleratedSeconds = Mathf.Min(dt, HourglassRemaining);
            levelTime += dt + acceleratedSeconds;
            HourglassExtraStageTime += acceleratedSeconds;
            HourglassRemaining -= acceleratedSeconds;
            if (acceleratedSeconds > 0f && HourglassRemaining <= 0f) ApplyHourglassPace(false);

            if (gameTimer != null)
            {
                gameTimer.SetTime(Mathf.Max(0f, LevelDuration - levelTime), HourglassRemaining);
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
            ApplyHourglassPace(!paused && HourglassRemaining > 0f);

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

            if (GStarDemo.Enabled)
                waveFormation.DispatchCohort(this, bonusWaveTenths, GStarFieldBalance.BonusPerTen(FiniteStomachMap.Instance));
            timeSinceLastMonsterSpawned += Time.deltaTime;

            float spawnRate = GetCurrentBaseMonsterSpawnRate();
       
            // 소화효소 처치 난이도 상승 연결:
            // 선택된 난이도가 SpawnRate이면 이 배율이 1.05, 1.10 ... 식으로 증가합니다.
            spawnRate *= DigestiveEnzymeDifficultyManager.SpawnRateMultiplier;
            if (HourglassRemaining > 0f) spawnRate *= HourglassSpawnRate;

            float monsterSpawnDelay = spawnRate > 0f
                ? 1.0f / spawnRate
                : float.PositiveInfinity;

            if (GStarDemo.Enabled && monsterSpawnDelay - timeSinceLastMonsterSpawned <= .75f)
                waveFormation.Preview(this);
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
            int bonusRate = GStarFieldBalance.BonusPerTen(FiniteStomachMap.Instance);
            if (GStarDemo.Enabled && levelBlueprint.MonsterIndexMap.TryGetValue(monsterIndex,out var entry))
            {
                var group = levelBlueprint.monsters[entry.Item1];
                var bp = group.monsterBlueprints[entry.Item2];
                if (bp != null && bp.GetType() == typeof(MeleeMonsterBlueprint) &&
                    group.monstersPrefab.TryGetComponent<Monster>(out var proto) && proto.GetType() == typeof(MeleeMonster) &&
                    (!ColonMonsterRoster.InColon || !ColonMonsterRoster.Active.IsSpecial(bp)) &&
                    waveFormation.QueueCohort(this, monsterIndex, bonusWaveTenths, bonusRate)) return;
            }
            SpawnRolledWaveMonster(monsterIndex, null, 0);
        }

        internal void SpawnRolledWaveMonster(int monsterIndex, Vector2[] points, int slot)
        {
            var spawned = SpawnAtWaveSlot(monsterIndex, 1f, true,
                points != null && slot < points.Length ? points[slot] : (Vector2?)null);
            int bonus = GStarFieldBalance.BonusPerTen(FiniteStomachMap.Instance);
            if (bonus == 0 || !GStarFieldBalance.IsOrdinary(spawned)) return;
            // Preserve each original wave roll (including every special). Only successful
            // ordinary rolls earn extra units, each with a separate legal spawn point.
            bonusWaveTenths += bonus;
            if (bonusWaveTenths >= 10)
            {
                bonusWaveTenths -= 10;
                SpawnAtWaveSlot(monsterIndex, 1f, true,
                    points != null && slot + 1 < points.Length ? points[slot+1] : (Vector2?)null);
            }
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
            TrySpawnMonsterByFlatIndex(monsterIndex, hpMultiplier);
        }

        private Monster TrySpawnMonsterByFlatIndex(int monsterIndex, float hpMultiplier, bool wave = false) =>
            SpawnAtWaveSlot(monsterIndex, hpMultiplier, wave, null);

        private Monster SpawnAtWaveSlot(int monsterIndex, float hpMultiplier, bool wave, Vector2? exact)
        {
            if (levelBlueprint == null || entityManager == null)
            {
                Debug.LogWarning(
                    "[LevelManager] SpawnMonsterByFlatIndex 실패: LevelBlueprint 또는 EntityManager가 비어 있습니다."
                );
                return null;
            }

            int baseLimit = Mathf.Max(1, levelBlueprint.normalSpawnPopulationLimit);
            int bonus = GStarFieldBalance.BonusPerTen(FiniteStomachMap.Instance);
            if (entityManager.LivingMonsters.Count >= baseLimit + Mathf.CeilToInt(baseLimit * bonus / 10f)) return null;

            if (!levelBlueprint.MonsterIndexMap.ContainsKey(monsterIndex))
            {
                Debug.LogWarning($"[LevelManager] 잘못된 monsterIndex입니다: {monsterIndex}");
                return null;
            }

            (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];

            if (poolIndex < 0 || poolIndex >= levelBlueprint.monsters.Length)
            {
                Debug.LogWarning($"[LevelManager] 잘못된 poolIndex입니다: {poolIndex}");
                return null;
            }

            if (blueprintIndex < 0 ||
                blueprintIndex >= levelBlueprint.monsters[poolIndex].monsterBlueprints.Length)
            {
                Debug.LogWarning($"[LevelManager] 잘못된 blueprintIndex입니다: {blueprintIndex}");
                return null;
            }

            MonsterBlueprint monsterBlueprint =
                levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];

            if (monsterBlueprint == null)
            {
                Debug.LogWarning(
                    $"[LevelManager] MonsterBlueprint가 비어 있습니다. monsterIndex={monsterIndex}"
                );
                return null;
            }

            // 소화효소 처치 난이도 상승 연결:
            // 선택된 난이도가 MonsterHealth이면 이 배율이 1.05, 1.10 ... 식으로 증가합니다.
            // Spawn APIs take EXTRA HP, not total HP. Only explicit event buffs apply.
            float multiplier = Mathf.Max(0f, hpMultiplier) * DigestiveEnzymeDifficultyManager.MonsterHpMultiplier;
            float extraHp = monsterBlueprint.hp * (multiplier - 1f);

            var group = levelBlueprint.monsters[poolIndex];
            Vector2 position = exact.GetValueOrDefault();
            bool formed = exact.HasValue || wave && GStarDemo.Enabled && monsterBlueprint.GetType() == typeof(MeleeMonsterBlueprint)
                && group.monstersPrefab != null && group.monstersPrefab.TryGetComponent<Monster>(out var prototype)
                && prototype.GetType() == typeof(MeleeMonster)
                && (!ColonMonsterRoster.InColon || !ColonMonsterRoster.Active.IsSpecial(monsterBlueprint))
                && waveFormation.TryPosition(this, out position);
            var spawned = formed ? entityManager.SpawnMonster(poolIndex, position, monsterBlueprint, extraHp)
                : entityManager.SpawnMonsterRandomPosition(poolIndex, monsterBlueprint, extraHp);
            if (spawned != null && !(monsterBlueprint is EliteMonsterBlueprint)) NormalMonstersSpawned++;
            return spawned;
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
            GStarAnalytics.Finish(false, "hp_zero");
            GStarSession.CompleteAttempt(false);

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
            // Legacy final-boss/portal callbacks cannot bypass the mandatory colon encounter.
            if (GetComponent<StageProgression>()?.StageNumber == 2 &&
                (GetComponent<ConstipationKingDirector>()?.Defeated != true || GetComponent<ColonExitSequence>()?.CompletionAuthorized != true)) return;
            if (playerCharacter != null && !playerCharacter.IsAlive) return;
            if (levelEnded)
            {
                return;
            }

            levelEnded = true;

            SaveCoinsGained();
            GameAudioManager.EndRunAudio(true);
            RunScore.Current?.Finish(true);
            GStarAnalytics.Finish(true, "stage_clear");
            GStarSession.CompleteAttempt(true);
            // Boss BGM 종료.
            // Ingame BGM은 재개하지 않음.
            // 승리 보상 효과음 재생.
            Time.timeScale = 0f;

            if (GetComponent<StageProgression>()?.StageNumber == 2)
            {
                GameEndingFlow.Begin(this);
                return;
            }

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
            if (GStarDemo.Enabled)
            {
                RunScore.Current?.Finish(false);
                GStarAnalytics.Abandon("return_or_quit");
                GStarSession.CompleteAttempt(false);
            }
            ProgressPrefs.Save();
        }

        private void SaveCoinsGained()
        {
            if (GStarDemo.Enabled) return;
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
            if (GStarDemo.Enabled)
            {
                AbandonRun();
                ApothecaryUI.ReturnToPreparation();
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            if (GStarDemo.Enabled)
            {
                AbandonRun();
                ApothecaryUI.ReturnToPreparation();
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
