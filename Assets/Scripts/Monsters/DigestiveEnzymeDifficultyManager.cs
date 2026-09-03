using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum DigestiveEnzymeDifficultyType
    {
        SpawnRate,
        MonsterHealth,
        MonsterMoveSpeed
    }

    /// <summary>
    /// 소화효소 몬스터 처치로 올라가는 난이도를 관리합니다.
    /// 
    /// 한 번 난이도가 오를 때 모든 항목을 올리지 않고,
    /// 활성화된 난이도 항목 중 하나만 랜덤으로 선택해 증가시킵니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DigestiveEnzymeDifficultyManager : MonoBehaviour
    {
        public static DigestiveEnzymeDifficultyManager Instance { get; private set; }

        [Header("Increase Rules")]
        [Tooltip("소화효소 처치로 난이도가 한 번 오를 때 증가하는 퍼센트입니다. 5면 +5%입니다.")]
        [SerializeField] private float difficultyIncreasePercent = 5f;
        [Header("Protection Contract")]
        [Tooltip("소화효소가 일반 몬스터를 처치했을 때 난이도 하락 계약을 사용할지 여부입니다.")]
        [SerializeField] private bool enableProtectionContract = true;

        [Tooltip("소화효소가 일반 몬스터를 몇 마리 처치하면 난이도를 1회 낮출지 정합니다.")]
        [SerializeField] private int enzymeMonsterKillsPerDecrease = 5;

        [Tooltip("보호 계약으로 난이도가 한 번 낮아질 때 감소하는 퍼센트입니다. 5면 -5%입니다.")]
        [SerializeField] private float difficultyDecreasePercent = 5f;

        private int enzymeMonsterKillCount;
        [Tooltip("소화효소를 몇 마리 처치할 때마다 난이도를 1회 올릴지 정합니다. 1이면 매번 상승합니다.")]
        [SerializeField] private int killCountPerIncrease = 1;

        [Tooltip("각 난이도 항목이 최대로 누적될 수 있는 퍼센트입니다. 50이면 최대 +50%입니다.")]
        [SerializeField] private float maxDifficultyIncreasePercent = 50f;

        [Header("Selectable Difficulty Types")]
        [Tooltip("난이도 상승 후보에 일반 몬스터 스폰량 증가를 포함합니다.")]
        [SerializeField] private bool allowSpawnRateIncrease = true;

        [Tooltip("난이도 상승 후보에 일반 몬스터 체력 증가를 포함합니다.")]
        [SerializeField] private bool allowMonsterHealthIncrease = true;

        [Tooltip("난이도 상승 후보에 일반 몬스터 이동속도 증가를 포함합니다.")]
        [SerializeField] private bool allowMonsterMoveSpeedIncrease = true;

        [Header("Move Speed Runtime Apply")]
        [Tooltip("활성화된 일반 몬스터에게 이동속도 증가 배율을 주기적으로 적용합니다.")]
        [SerializeField] private bool applyMoveSpeedBonusToActiveMonsters = true;

        [Tooltip("일반 몬스터 이동속도 배율을 다시 적용하는 주기입니다.")]
        [SerializeField] private float moveSpeedApplyInterval = 1f;

        [Tooltip("보스 몬스터에게는 소화효소 난이도 이동속도 증가를 적용하지 않습니다.")]
        [SerializeField] private bool ignoreBossMonstersForMoveSpeed = true;

        [Header("Debug")]
        [Tooltip("난이도 상승 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<DigestiveEnzymeDifficultyType> selectableTypes =
            new List<DigestiveEnzymeDifficultyType>();

        private int currentKillCount;
        private float spawnRateBonusPercent;
        private float monsterHealthBonusPercent;
        private float monsterMoveSpeedBonusPercent;
        private float nextMoveSpeedApplyTime;

        public static float SpawnRateMultiplier
        {
            get
            {
                if (Instance == null)
                {
                    return 1f;
                }

                return 1f + Instance.spawnRateBonusPercent * 0.01f;
            }
        }

        public static float MonsterHpMultiplier
        {
            get
            {
                if (Instance == null)
                {
                    return 1f;
                }

                return 1f + Instance.monsterHealthBonusPercent * 0.01f;
            }
        }

        public static float MonsterMoveSpeedMultiplier
        {
            get
            {
                if (Instance == null)
                {
                    return 1f;
                }

                return 1f + Instance.monsterMoveSpeedBonusPercent * 0.01f;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "[DigestiveEnzymeDifficultyManager] 씬에 매니저가 2개 이상 있습니다. 가장 최근 매니저를 사용합니다."
                );
            }

            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!applyMoveSpeedBonusToActiveMonsters)
            {
                return;
            }

            if (Time.time < nextMoveSpeedApplyTime)
            {
                return;
            }

            nextMoveSpeedApplyTime = Time.time + Mathf.Max(0.1f, moveSpeedApplyInterval);
            ApplyMoveSpeedBonusToActiveMonsters();
        }
        public static void NotifyDigestiveEnzymeKilledMonster(
    DigestiveEnzymeMonster source,
    Monster killedMonster)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.RegisterDigestiveEnzymeMonsterKill(source, killedMonster);
        }

        private void RegisterDigestiveEnzymeMonsterKill(
            DigestiveEnzymeMonster source,
            Monster killedMonster)
        {
            if (!enableProtectionContract)
            {
                return;
            }

            if (source == null || killedMonster == null)
            {
                return;
            }

            enzymeMonsterKillCount++;

            int safeRequiredKillCount = Mathf.Max(1, enzymeMonsterKillsPerDecrease);

            if (debugLog)
            {
                Debug.Log(
                    $"[DigestiveEnzymeProtectionContract] 소화효소 몬스터 처치 누적: " +
                    $"{enzymeMonsterKillCount}/{safeRequiredKillCount}");
            }

            if (enzymeMonsterKillCount < safeRequiredKillCount)
            {
                return;
            }

            enzymeMonsterKillCount = 0;
            ApplyRandomDifficultyDecrease();
        }

        private void ApplyRandomDifficultyDecrease()
        {
            List<DigestiveEnzymeDifficultyType> decreaseCandidates =
                new List<DigestiveEnzymeDifficultyType>();

            if (spawnRateBonusPercent > 0f)
            {
                decreaseCandidates.Add(DigestiveEnzymeDifficultyType.SpawnRate);
            }

            if (monsterHealthBonusPercent > 0f)
            {
                decreaseCandidates.Add(DigestiveEnzymeDifficultyType.MonsterHealth);
            }

            if (monsterMoveSpeedBonusPercent > 0f)
            {
                decreaseCandidates.Add(DigestiveEnzymeDifficultyType.MonsterMoveSpeed);
            }

            if (decreaseCandidates.Count <= 0)
            {
                if (debugLog)
                {
                    Debug.Log("[DigestiveEnzymeProtectionContract] 낮출 수 있는 난이도 누적치가 없습니다.");
                }

                return;
            }

            DigestiveEnzymeDifficultyType selectedType =
                decreaseCandidates[Random.Range(0, decreaseCandidates.Count)];

            float decreaseAmount = Mathf.Max(0f, difficultyDecreasePercent);

            switch (selectedType)
            {
                case DigestiveEnzymeDifficultyType.SpawnRate:
                    spawnRateBonusPercent = Mathf.Max(0f, spawnRateBonusPercent - decreaseAmount);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[DigestiveEnzymeProtectionContract] 스폰량 난이도 감소. " +
                            $"spawnRateBonus={spawnRateBonusPercent}% / multiplier={SpawnRateMultiplier:F2}");
                    }
                    break;

                case DigestiveEnzymeDifficultyType.MonsterHealth:
                    monsterHealthBonusPercent = Mathf.Max(0f, monsterHealthBonusPercent - decreaseAmount);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[DigestiveEnzymeProtectionContract] 몬스터 체력 난이도 감소. " +
                            $"hpBonus={monsterHealthBonusPercent}% / multiplier={MonsterHpMultiplier:F2}");
                    }
                    break;

                case DigestiveEnzymeDifficultyType.MonsterMoveSpeed:
                    monsterMoveSpeedBonusPercent = Mathf.Max(0f, monsterMoveSpeedBonusPercent - decreaseAmount);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[DigestiveEnzymeProtectionContract] 몬스터 이동속도 난이도 감소. " +
                            $"moveSpeedBonus={monsterMoveSpeedBonusPercent}% / multiplier={MonsterMoveSpeedMultiplier:F2}");
                    }
                    break;
            }
        }
        public static void NotifyDigestiveEnzymeKilledByPlayer(DigestiveEnzymeMonster source)
        {
            if (Instance == null)
            {
                Debug.LogWarning(
                    "[DigestiveEnzymeDifficultyManager] 씬에 DigestiveEnzymeDifficultyManager가 없어 난이도 상승을 적용하지 못했습니다."
                );

                return;
            }

            Instance.RegisterDigestiveEnzymeKill(source);
           
        }
        /// <summary>
        /// 소화효소 몬스터 처치가 아닌 다른 특수 이벤트에서
        /// 기존 소화효소 난이도 상승과 같은 랜덤 난이도 상승을 발생시키기 위한 공개 메서드입니다.
        ///
        /// 예: 위산 거머리가 30초 동안 피를 빨아먹은 뒤 난이도 상승을 발생시킬 때 사용합니다.
        /// </summary>
        public static void NotifyDifficultyIncreaseFromExternalSource(string externalSourceName)
        {
            if (Instance == null)
            {
                Debug.LogWarning(
                    $"[DigestiveEnzymeDifficultyManager] 씬에 DigestiveEnzymeDifficultyManager가 없어 외부 난이도 상승을 적용하지 못했습니다. Source={externalSourceName}"
                );
                return;
            }

            Instance.ApplyRandomDifficultyIncrease();

            if (Instance.debugLog)
            {
                Debug.Log($"[DigestiveEnzymeDifficulty] 외부 원인으로 난이도 상승 적용. Source={externalSourceName}");
            }
        }
        private void RegisterDigestiveEnzymeKill(DigestiveEnzymeMonster source)
        {
            currentKillCount++;

            int safeKillCountPerIncrease = Mathf.Max(1, killCountPerIncrease);

            if (currentKillCount < safeKillCountPerIncrease)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[DigestiveEnzymeDifficulty] 소화효소 처치 누적. {currentKillCount}/{safeKillCountPerIncrease}"
                    );
                }

                return;
            }

            currentKillCount = 0;
            ApplyRandomDifficultyIncrease();
        }

        private void ApplyRandomDifficultyIncrease()
        {
            BuildSelectableDifficultyList();

            if (selectableTypes.Count == 0)
            {
                if (debugLog)
                {
                    Debug.Log(
                        "[DigestiveEnzymeDifficulty] 올릴 수 있는 난이도 항목이 없습니다. 이미 최대치이거나 모든 항목이 비활성화되었습니다."
                    );
                }

                return;
            }

            DigestiveEnzymeDifficultyType selectedType =
                selectableTypes[Random.Range(0, selectableTypes.Count)];

            switch (selectedType)
            {
                case DigestiveEnzymeDifficultyType.SpawnRate:
                    spawnRateBonusPercent = IncreasePercent(spawnRateBonusPercent);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[DigestiveEnzymeDifficulty] 스폰량 증가 선택. spawnRateBonus={spawnRateBonusPercent}% / multiplier={SpawnRateMultiplier:F2}"
                        );
                    }
                    break;

                case DigestiveEnzymeDifficultyType.MonsterHealth:
                    monsterHealthBonusPercent = IncreasePercent(monsterHealthBonusPercent);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[DigestiveEnzymeDifficulty] 몬스터 체력 증가 선택. hpBonus={monsterHealthBonusPercent}% / multiplier={MonsterHpMultiplier:F2}"
                        );
                    }
                    break;

                case DigestiveEnzymeDifficultyType.MonsterMoveSpeed:
                    monsterMoveSpeedBonusPercent = IncreasePercent(monsterMoveSpeedBonusPercent);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[DigestiveEnzymeDifficulty] 몬스터 이동속도 증가 선택. moveSpeedBonus={monsterMoveSpeedBonusPercent}% / multiplier={MonsterMoveSpeedMultiplier:F2}"
                        );
                    }
                    break;
            }
        }

        private void BuildSelectableDifficultyList()
        {
            selectableTypes.Clear();

            if (allowSpawnRateIncrease && CanIncrease(spawnRateBonusPercent))
            {
                selectableTypes.Add(DigestiveEnzymeDifficultyType.SpawnRate);
            }

            if (allowMonsterHealthIncrease && CanIncrease(monsterHealthBonusPercent))
            {
                selectableTypes.Add(DigestiveEnzymeDifficultyType.MonsterHealth);
            }

            if (allowMonsterMoveSpeedIncrease && CanIncrease(monsterMoveSpeedBonusPercent))
            {
                selectableTypes.Add(DigestiveEnzymeDifficultyType.MonsterMoveSpeed);
            }
        }

        private bool CanIncrease(float currentPercent)
        {
            return currentPercent < maxDifficultyIncreasePercent;
        }

        private float IncreasePercent(float currentPercent)
        {
            float increaseAmount = Mathf.Max(0f, difficultyIncreasePercent);
            float maxPercent = Mathf.Max(0f, maxDifficultyIncreasePercent);

            return Mathf.Min(maxPercent, currentPercent + increaseAmount);
        }

        private void ApplyMoveSpeedBonusToActiveMonsters()
        {
            float multiplier = MonsterMoveSpeedMultiplier;

            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster monster = monsters[i];

                if (monster == null)
                {
                    continue;
                }

                if (!monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (monster.HP <= 0f)
                {
                    continue;
                }

                if (ignoreBossMonstersForMoveSpeed && monster is BossMonster)
                {
                    continue;
                }

                DigestiveEnzymeMoveSpeedRuntimeMarker marker =
                    monster.GetComponent<DigestiveEnzymeMoveSpeedRuntimeMarker>();

                if (marker == null)
                {
                    marker = monster.gameObject.AddComponent<DigestiveEnzymeMoveSpeedRuntimeMarker>();
                }

                if (!marker.HasBaseMoveSpeed)
                {
                    marker.CaptureBaseMoveSpeed(monster.moveSpeed);
                }

                if (Mathf.Approximately(marker.LastAppliedMultiplier, multiplier))
                {
                    continue;
                }

                monster.moveSpeed = marker.BaseMoveSpeed * multiplier;
                marker.LastAppliedMultiplier = multiplier;
            }
        }

        public string GetDebugStatusText()
        {
            return
                $"SpawnRate +{spawnRateBonusPercent}% / " +
                $"HP +{monsterHealthBonusPercent}% / " +
                $"MoveSpeed +{monsterMoveSpeedBonusPercent}%";
        }
    }

    /// <summary>
    /// 소화효소 난이도 매니저가 몬스터 이동속도 보정 기준값을 저장하기 위해
    /// 런타임에 일반 몬스터 오브젝트에 붙이는 내부 마커입니다.
    /// 
    /// 몬스터 풀에서 비활성화되면 기준값을 초기화해서,
    /// 같은 오브젝트가 재사용될 때 이전 배율이 꼬이지 않게 합니다.
    /// </summary>
    internal class DigestiveEnzymeMoveSpeedRuntimeMarker : MonoBehaviour
    {
        public bool HasBaseMoveSpeed { get; private set; }
        public float BaseMoveSpeed { get; private set; }
        public float LastAppliedMultiplier { get; set; } = 1f;

        public void CaptureBaseMoveSpeed(float moveSpeed)
        {
            BaseMoveSpeed = Mathf.Max(0.05f, moveSpeed);
            LastAppliedMultiplier = 1f;
            HasBaseMoveSpeed = true;
        }

        private void OnDisable()
        {
            HasBaseMoveSpeed = false;
            BaseMoveSpeed = 0f;
            LastAppliedMultiplier = 1f;
        }
    }
}