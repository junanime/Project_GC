using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Yellow Core 전용 카페인 주입기 챌린지 패턴입니다.
    ///
    /// Step 8B:
    /// - 보스 몸 주변에 카페인 주입기 3개 생성
    /// - 10초 안에 모두 파괴하면 성공
    /// - 성공 시 보스 이동/접촉을 잠시 봉쇄하고 Groggy + Core Open
    /// - 실패 시 보스 중심에서 카페인 전멸 파동이 바깥으로 확산
    /// - 파동에 닿으면 Character의 기존 피해 파이프라인으로 치명 피해 적용
    /// </summary>
    public class BossCaffeineInjectorPattern :
        BossPatternBase
    {
        [Header("Caffeine Injector / 카페인 주입기")]

        [Tooltip(
            "생성할 카페인 주입기 프리팹입니다. " +
            "BossCaffeineInjector + Collider2D가 필요합니다."
        )]
        [SerializeField]
        private GameObject injectorPrefab;

        [Tooltip(
            "한 번의 챌린지에서 생성할 주입기 수입니다. " +
            "현재 기획 기본값은 3입니다."
        )]
        [SerializeField, Min(1)]
        private int injectorCount = 3;

        [Tooltip(
            "각 주입기의 HP입니다. " +
            "테스트 시작값은 60을 권장합니다."
        )]
        [SerializeField, Min(1f)]
        private float injectorHp = 60f;

        [Header("Placement / 배치")]

        [Tooltip(
            "보스 중심에서 주입기를 배치할 거리입니다. " +
            "보스 몸에 꽂힌 느낌이 나도록 1~1.5 정도에서 조정하세요."
        )]
        [SerializeField, Min(0f)]
        private float spawnRadius = 1.25f;

        [Tooltip(
            "첫 주입기의 배치 각도입니다. " +
            "90이면 첫 주입기가 보스 위쪽에 배치됩니다."
        )]
        [SerializeField]
        private float spawnAngleOffset = 90f;

        [Tooltip(
            "체크하면 생성된 주입기를 BossController Transform 자식으로 만들어 " +
            "보스가 이동할 때 같이 따라가게 합니다."
        )]
        [SerializeField]
        private bool parentInjectorsToBoss = true;

        [Header("Challenge Timer / 제한 시간")]

        [Tooltip(
            "3개의 주입기를 모두 파괴해야 하는 제한 시간입니다. " +
            "기획 기본값은 10초입니다."
        )]
        [SerializeField, Min(0.1f)]
        private float challengeDuration = 10f;

        [Tooltip(
            "체크하면 주입기가 남아 있는 동안 이 BossPattern이 실행 상태를 유지합니다. " +
            "Step 8A 테스트에서는 ON을 권장합니다. " +
            "OFF로 두면 Pattern Cast 후 다른 패턴이 다시 실행될 수 있습니다."
        )]
        [SerializeField]
        private bool blockOtherPatternsDuringChallenge = true;

        [Tooltip(
            "Block Other Patterns가 OFF일 때 주입기 생성 직후 " +
            "이 패턴의 Cast가 끝났다고 처리할 시간입니다."
        )]
        [SerializeField, Min(0f)]
        private float patternCastSeconds = 0.25f;

        [Header("Success / 성공 보상")]

        [Tooltip(
            "모든 주입기를 제한 시간 안에 파괴했을 때 적용할 Groggy 시간입니다."
        )]
        [SerializeField, Min(0f)]
        private float successGroggyDuration = 3.5f;

        [Tooltip(
            "성공 Groggy/Core Open을 담당하는 BossCoreDamageStateBridge입니다. " +
            "비워 두면 보스 루트에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossCoreDamageStateBridge coreDamageStateBridge;

        [Tooltip(
            "성공 시 보스 위치에 생성할 VFX입니다. " +
            "비워 두어도 기능은 동작합니다."
        )]
        [SerializeField]
        private GameObject successVfxPrefab;

        [Header("Failure - Step 8B / 카페인 확산 전멸 공격")]

        [Tooltip(
            "실패 시 보스 중심에 생성할 시작 VFX입니다. " +
            "비워 두어도 확산 판정은 정상 동작합니다."
        )]
        [SerializeField]
        private GameObject failureVfxPrefab;

        [Tooltip(
            "체크하면 제한 시간 실패 후 보스 중심에서 바깥으로 퍼지는 카페인 전멸 파동을 생성합니다."
        )]
        [SerializeField]
        private bool enableFailureLethalWave = true;

        [Tooltip(
            "실패 확정 후 실제 파동이 퍼지기 전 경고 시간입니다."
        )]
        [SerializeField, Min(0f)]
        private float failureWaveTelegraphDuration = 0.75f;

        [Tooltip(
            "파동이 시작 반경에서 최대 반경까지 확장되는 시간입니다."
        )]
        [SerializeField, Min(0.05f)]
        private float failureWaveExpansionDuration = 2.5f;

        [Tooltip(
            "확산 파동이 시작하는 반경입니다."
        )]
        [SerializeField, Min(0f)]
        private float failureWaveStartRadius = 0.5f;

        [Tooltip(
            "확산 파동의 최대 반경입니다. " +
            "현재 필드 테스트에서는 30 정도로 시작하세요."
        )]
        [SerializeField, Min(0.1f)]
        private float failureWaveMaxRadius = 30f;

        [Tooltip(
            "플레이어와 충돌하는 파동 띠의 두께입니다. " +
            "고속 확산 시 판정 누락 방지를 위해 0.8~1.2를 권장합니다."
        )]
        [SerializeField, Min(0.05f)]
        private float failureWaveHitThickness = 1f;

        [Tooltip(
            "플레이어에게 넣을 치명 피해 계산 배율입니다. " +
            "실제 피해 = CurrentHealth + CurrentArmor + MaxHealth × 이 값 + 1. " +
            "기본 2면 일반 HP/방어 기준 사실상 즉사 피해입니다."
        )]
        [SerializeField, Min(0.1f)]
        private float failureWaveLethalDamageMultiplier = 2f;

        [Tooltip(
            "체크하면 별도 아트가 없어도 테스트할 수 있도록 LineRenderer 원형 파동을 런타임에 표시합니다."
        )]
        [SerializeField]
        private bool drawFailureRuntimeRing = true;

        [Tooltip("런타임 원형 파동의 선분 수입니다.")]
        [SerializeField, Range(24, 180)]
        private int failureRuntimeRingSegments = 96;

        [Tooltip("런타임 원형 파동의 선 두께입니다.")]
        [SerializeField, Min(0.01f)]
        private float failureRuntimeRingWidth = 0.16f;

        [Tooltip("런타임 원형 파동 색상입니다.")]
        [SerializeField]
        private Color failureRuntimeRingColor =
            new Color(1f, 0.78f, 0.08f, 0.9f);

        [Tooltip("런타임 원형 파동 Sprite/LineRenderer Sorting Order입니다.")]
        [SerializeField]
        private int failureRuntimeRingSortingOrder = 900;

        [Tooltip(
            "파동 최대 반경 도달 후 화면에 남아 있다가 제거되는 시간입니다."
        )]
        [SerializeField, Min(0f)]
        private float failureWaveEndLinger = 0.2f;

        [Header("Injector Visual")]

        [Tooltip("각 주입기 생성 VFX입니다.")]
        [SerializeField]
        private GameObject injectorSpawnVfxPrefab;

        [Tooltip("각 주입기 파괴 VFX입니다.")]
        [SerializeField]
        private GameObject injectorDestroyVfxPrefab;

        [Tooltip(
            "체크하면 주입기 SpriteRenderer의 Sorting Order를 아래 값으로 강제합니다."
        )]
        [SerializeField]
        private bool forceInjectorSortingOrder = true;

        [Tooltip(
            "주입기 Sorting Order입니다. " +
            "테스트 파츠보다 앞에 보이도록 600 정도부터 확인하세요."
        )]
        [SerializeField]
        private int injectorSortingOrder = 600;

        [Header("Runtime - Read Only")]

        [Tooltip("현재 주입기 챌린지가 진행 중인지 표시합니다.")]
        [SerializeField]
        private bool challengeActive;

        [Tooltip("현재 살아 있는 주입기 개수입니다.")]
        [SerializeField]
        private int remainingInjectorCount;

        [Tooltip("현재 챌린지 경과 시간입니다.")]
        [SerializeField]
        private float currentChallengeElapsed;

        [Tooltip("마지막 챌린지가 성공했는지 표시합니다.")]
        [SerializeField]
        private bool lastChallengeSucceeded;

        [Tooltip("마지막 챌린지가 시간 초과로 실패했는지 표시합니다.")]
        [SerializeField]
        private bool lastChallengeFailed;

        [Tooltip("현재 실패 전멸 파동 시퀀스가 진행 중인지 표시합니다.")]
        [SerializeField]
        private bool failureSequenceActive;

        [Header("Debug")]

        [Tooltip(
            "주입기 생성/파괴/남은 개수/성공/실패/Groggy 로그를 출력합니다."
        )]
        [SerializeField]
        private bool debugLog = true;

        private readonly List<BossCaffeineInjector>
            activeInjectors =
                new List<BossCaffeineInjector>();

        private Coroutine challengeRoutine;
        private bool successGroggyApplied;
        private bool failureRuntimeLockApplied;
        private BossCaffeineFailureWave activeFailureWave;

        public bool IsChallengeActive => challengeActive;
        public int RemainingInjectorCount => remainingInjectorCount;
        public float ChallengeElapsed => currentChallengeElapsed;

        private void Reset()
        {
            patternName =
                "Caffeine Injector";

            cooldown =
                35f;
        }

        public override void Init(
            BossController controller)
        {
            base.Init(
                controller);

            ResolveCoreDamageStateBridge();
        }

        public override bool CanUse()
        {
            if (challengeActive)
            {
                return false;
            }

            return
                base.CanUse();
        }

        protected override IEnumerator ExecutePattern()
        {
            if (bossController == null ||
                bossController.IsDead)
            {
                yield break;
            }

            if (injectorPrefab == null)
            {
                Debug.LogWarning(
                    "[BossCaffeineInjectorPattern] Injector Prefab이 비어 있습니다.",
                    this
                );

                yield break;
            }

            CleanupInjectors();

            challengeActive =
                true;

            lastChallengeSucceeded =
                false;

            lastChallengeFailed =
                false;

            currentChallengeElapsed =
                0f;

            SpawnInjectors();

            remainingInjectorCount =
                CountAliveInjectors();

            if (remainingInjectorCount <= 0)
            {
                challengeActive =
                    false;

                yield break;
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineInjectorPattern] Challenge START | " +
                    $"Count={remainingInjectorCount}, " +
                    $"Limit={challengeDuration:0.##}s",
                    this
                );
            }

            if (challengeRoutine != null)
            {
                StopCoroutine(
                    challengeRoutine);
            }

            challengeRoutine =
                StartCoroutine(
                    ChallengeRoutine());

            if (!blockOtherPatternsDuringChallenge)
            {
                yield return
                    new WaitForSeconds(
                        Mathf.Max(
                            0f,
                            patternCastSeconds));

                yield break;
            }

            while (challengeActive)
            {
                yield return null;
            }

            // 성공 Groggy 또는 실패 전멸 파동이 끝날 때까지
            // 이 패턴을 실행 상태로 유지합니다.
            while (successGroggyApplied ||
                   failureSequenceActive)
            {
                yield return null;
            }
        }

        /// <summary>
        /// BossCaffeineInjector가 실제 파괴될 때 호출합니다.
        /// </summary>
        public void NotifyInjectorDestroyed(
            BossCaffeineInjector injector)
        {
            if (injector == null)
            {
                return;
            }

            activeInjectors.Remove(
                injector);

            remainingInjectorCount =
                CountAliveInjectors();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineInjectorPattern] Injector DESTROYED | " +
                    $"Remain={remainingInjectorCount}",
                    this
                );
            }
        }

        private void SpawnInjectors()
        {
            activeInjectors.Clear();

            int finalCount =
                Mathf.Max(
                    1,
                    injectorCount);

            float angleStep =
                360f /
                finalCount;

            for (int i = 0;
                 i < finalCount;
                 i++)
            {
                float angle =
                    spawnAngleOffset +
                    angleStep *
                    i;

                float rad =
                    angle *
                    Mathf.Deg2Rad;

                Vector3 offset =
                    new Vector3(
                        Mathf.Cos(rad),
                        Mathf.Sin(rad),
                        0f) *
                    spawnRadius;

                Vector3 spawnPosition =
                    bossController.BossCenterPosition +
                    offset;

                GameObject injectorObject =
                    Instantiate(
                        injectorPrefab,
                        spawnPosition,
                        Quaternion.identity);

                injectorObject.name =
                    $"Boss_CaffeineInjector_{i + 1}";

                if (parentInjectorsToBoss &&
                    bossController != null)
                {
                    injectorObject.transform.SetParent(
                        bossController.transform,
                        true);
                }

                BossCaffeineInjector injector =
                    injectorObject.GetComponent
                    <
                        BossCaffeineInjector
                    >();

                if (injector == null)
                {
                    injector =
                        injectorObject.AddComponent
                        <
                            BossCaffeineInjector
                        >();
                }

                injector.Setup(
                    this,
                    injectorHp,
                    injectorDestroyVfxPrefab,
                    debugLog);

                ApplyInjectorSortingOrder(
                    injectorObject);

                if (injectorSpawnVfxPrefab != null)
                {
                    Instantiate(
                        injectorSpawnVfxPrefab,
                        spawnPosition,
                        Quaternion.identity);
                }

                activeInjectors.Add(
                    injector);
            }
        }

        private IEnumerator ChallengeRoutine()
        {
            currentChallengeElapsed =
                0f;

            while (challengeActive)
            {
                if (bossController == null ||
                    bossController.IsDead)
                {
                    challengeActive =
                        false;

                    break;
                }

                CleanupInjectorList();

                remainingInjectorCount =
                    CountAliveInjectors();

                if (remainingInjectorCount <= 0)
                {
                    yield return
                        HandleSuccess();

                    yield break;
                }

                currentChallengeElapsed +=
                    Time.deltaTime;

                if (currentChallengeElapsed >=
                    Mathf.Max(
                        0.1f,
                        challengeDuration))
                {
                    yield return
                        HandleFailure();

                    yield break;
                }

                yield return null;
            }

            challengeRoutine =
                null;
        }

        private IEnumerator HandleSuccess()
        {
            if (!challengeActive)
            {
                yield break;
            }

            challengeActive =
                false;

            lastChallengeSucceeded =
                true;

            lastChallengeFailed =
                false;

            remainingInjectorCount =
                0;

            if (successVfxPrefab != null &&
                bossController != null)
            {
                Instantiate(
                    successVfxPrefab,
                    bossController.BossCenterPosition,
                    Quaternion.identity);
            }

            ResolveCoreDamageStateBridge();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineInjectorPattern] SUCCESS | " +
                    $"Time={currentChallengeElapsed:0.##}s, " +
                    $"Groggy={successGroggyDuration:0.##}s",
                    this
                );
            }

            if (bossController == null ||
                successGroggyDuration <= 0f)
            {
                challengeRoutine =
                    null;

                yield break;
            }

            successGroggyApplied =
                true;

            bossController.SetExternalMovementLock(
                true);

            bossController.SetSuppressContactDamage(
                true);

            if (coreDamageStateBridge != null)
            {
                coreDamageStateBridge.SetGroggy(
                    true);
            }
            else
            {
                Debug.LogWarning(
                    "[BossCaffeineInjectorPattern] BossCoreDamageStateBridge를 찾지 못했습니다. " +
                    "성공 Groggy/Core x2가 적용되지 않습니다.",
                    this
                );
            }

            float endTime =
                Time.time +
                successGroggyDuration;

            while (Time.time <
                   endTime)
            {
                if (bossController == null ||
                    bossController.IsDead ||
                    bossController.IsPhaseTransitioning)
                {
                    break;
                }

                StopBossMotion();

                yield return null;
            }

            ClearSuccessGroggy();

            challengeRoutine =
                null;
        }

        private IEnumerator HandleFailure()
        {
            if (!challengeActive)
            {
                yield break;
            }

            challengeActive =
                false;

            lastChallengeSucceeded =
                false;

            lastChallengeFailed =
                true;

            failureSequenceActive =
                true;

            if (failureVfxPrefab != null &&
                bossController != null)
            {
                Instantiate(
                    failureVfxPrefab,
                    bossController.BossCenterPosition,
                    Quaternion.identity);
            }

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossCaffeineInjectorPattern] FAILURE | " +
                    $"Time Limit={challengeDuration:0.##}s, " +
                    $"Remain={CountAliveInjectors()} | " +
                    $"Caffeine lethal wave START",
                    this
                );
            }

            // 남은 주입기는 실패가 확정되는 순간 제거합니다.
            ForceDestroyRemainingInjectors();

            remainingInjectorCount =
                0;

            if (bossController == null ||
                bossController.IsDead)
            {
                failureSequenceActive =
                    false;

                challengeRoutine =
                    null;

                yield break;
            }

            // 실패 전멸 연출 중에는 보스가 움직이거나 접촉 피해를 주지 않게 고정합니다.
            bossController.SetExternalMovementLock(
                true);

            bossController.SetSuppressContactDamage(
                true);

            failureRuntimeLockApplied =
                true;

            StopBossMotion();

            if (enableFailureLethalWave)
            {
                SpawnFailureWave();

                while (activeFailureWave != null &&
                       !activeFailureWave.IsFinished)
                {
                    if (bossController == null ||
                        bossController.IsDead ||
                        bossController.IsPhaseTransitioning)
                    {
                        activeFailureWave.CancelWave();
                        break;
                    }

                    StopBossMotion();

                    yield return null;
                }
            }
            else
            {
                // 기능을 꺼둔 경우에도 실패 연출이 너무 즉시 끝나지 않도록
                // Telegraph 시간만큼 잠깐 유지합니다.
                float waitTime =
                    Mathf.Max(
                        0f,
                        failureWaveTelegraphDuration);

                if (waitTime > 0f)
                {
                    yield return
                        new WaitForSeconds(
                            waitTime);
                }
            }

            ClearFailureSequence();

            challengeRoutine =
                null;
        }

        private void SpawnFailureWave()
        {
            if (bossController == null ||
                bossController.PlayerCharacter == null)
            {
                return;
            }

            if (activeFailureWave != null)
            {
                activeFailureWave.CancelWave();
                activeFailureWave =
                    null;
            }

            GameObject waveObject =
                new GameObject(
                    "Boss_CaffeineFailureWave");

            waveObject.transform.position =
                bossController.BossCenterPosition;

            activeFailureWave =
                waveObject.AddComponent
                <
                    BossCaffeineFailureWave
                >();

            activeFailureWave.Setup(
                bossController.PlayerCharacter,
                bossController.BossCenterPosition,
                failureWaveTelegraphDuration,
                failureWaveExpansionDuration,
                failureWaveStartRadius,
                failureWaveMaxRadius,
                failureWaveHitThickness,
                failureWaveLethalDamageMultiplier,
                drawFailureRuntimeRing,
                failureRuntimeRingSegments,
                failureRuntimeRingWidth,
                failureRuntimeRingColor,
                failureRuntimeRingSortingOrder,
                failureWaveEndLinger,
                debugLog
            );
        }

        private void ClearFailureSequence()
        {
            if (activeFailureWave != null)
            {
                if (!activeFailureWave.IsFinished)
                {
                    activeFailureWave.CancelWave();
                }

                activeFailureWave =
                    null;
            }

            if (failureRuntimeLockApplied &&
                bossController != null)
            {
                bossController.SetSuppressContactDamage(
                    false);

                bossController.SetExternalMovementLock(
                    false);
            }

            failureRuntimeLockApplied =
                false;

            failureSequenceActive =
                false;
        }

        private void ResolveCoreDamageStateBridge()
        {
            if (coreDamageStateBridge != null ||
                bossController == null)
            {
                return;
            }

            coreDamageStateBridge =
                bossController.GetComponent
                <
                    BossCoreDamageStateBridge
                >();

            if (coreDamageStateBridge == null)
            {
                coreDamageStateBridge =
                    bossController.GetComponentInParent
                    <
                        BossCoreDamageStateBridge
                    >(true);
            }

            if (coreDamageStateBridge == null &&
                bossController.transform.root != null)
            {
                coreDamageStateBridge =
                    bossController.transform.root
                        .GetComponentInChildren
                        <
                            BossCoreDamageStateBridge
                        >(true);
            }
        }

        private void ClearSuccessGroggy()
        {
            if (!successGroggyApplied)
            {
                return;
            }

            if (coreDamageStateBridge != null)
            {
                coreDamageStateBridge.SetGroggy(
                    false);
            }

            if (bossController != null)
            {
                bossController.SetSuppressContactDamage(
                    false);

                bossController.SetExternalMovementLock(
                    false);
            }

            successGroggyApplied =
                false;
        }

        private void StopBossMotion()
        {
            if (bossController == null)
            {
                return;
            }

            Rigidbody2D rb =
                bossController.Rigidbody;

            if (rb == null)
            {
                return;
            }

            rb.velocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;
        }

        private int CountAliveInjectors()
        {
            int count =
                0;

            for (int i = 0;
                 i < activeInjectors.Count;
                 i++)
            {
                BossCaffeineInjector injector =
                    activeInjectors[i];

                if (injector != null &&
                    !injector.IsDestroyed)
                {
                    count++;
                }
            }

            return count;
        }

        private void CleanupInjectorList()
        {
            for (int i =
                     activeInjectors.Count - 1;
                 i >= 0;
                 i--)
            {
                BossCaffeineInjector injector =
                    activeInjectors[i];

                if (injector == null ||
                    injector.IsDestroyed)
                {
                    activeInjectors.RemoveAt(
                        i);
                }
            }
        }

        private void ForceDestroyRemainingInjectors()
        {
            for (int i =
                     activeInjectors.Count - 1;
                 i >= 0;
                 i--)
            {
                BossCaffeineInjector injector =
                    activeInjectors[i];

                if (injector != null &&
                    !injector.IsDestroyed)
                {
                    injector.ForceDestroy();
                }
            }

            activeInjectors.Clear();
        }

        private void CleanupInjectors()
        {
            ForceDestroyRemainingInjectors();

            remainingInjectorCount =
                0;
        }

        private void ApplyInjectorSortingOrder(
            GameObject injectorObject)
        {
            if (!forceInjectorSortingOrder ||
                injectorObject == null)
            {
                return;
            }

            SpriteRenderer[] renderers =
                injectorObject
                    .GetComponentsInChildren
                    <
                        SpriteRenderer
                    >(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                SpriteRenderer renderer =
                    renderers[i];

                if (renderer != null)
                {
                    renderer.sortingOrder =
                        injectorSortingOrder;
                }
            }
        }

        private void OnDisable()
        {
            CleanupRuntime();
        }

        private void OnDestroy()
        {
            CleanupRuntime();
        }

        private void CleanupRuntime()
        {
            if (challengeRoutine != null)
            {
                StopCoroutine(
                    challengeRoutine);

                challengeRoutine =
                    null;
            }

            challengeActive =
                false;

            ClearSuccessGroggy();
            ClearFailureSequence();
            CleanupInjectors();
        }
    }
}
