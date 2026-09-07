using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Yellow Core 전용 카페인 주입기 챌린지 패턴입니다.
    ///
    /// Step 8A:
    /// - 보스 몸 주변에 카페인 주입기 3개 생성
    /// - 10초 안에 모두 파괴하면 성공
    /// - 성공 시 보스 이동/접촉을 잠시 봉쇄하고 Groggy + Core Open
    /// - 실패 시 현재는 주입기 정리 + Fail 로그까지만 수행
    ///
    /// Step 8B에서:
    /// - 실패 시 전장 확산 공격
    /// - 최종 Instant Kill 규칙
    /// 을 추가합니다.
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

        [Header("Failure - Step 8B Hook")]

        [Tooltip(
            "실패 시 생성할 임시 VFX입니다. " +
            "Step 8A에서는 실패 즉사 공격을 아직 적용하지 않습니다."
        )]
        [SerializeField]
        private GameObject failureVfxPrefab;

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

            // 성공 Groggy까지 이 패턴이 유지되도록 합니다.
            while (successGroggyApplied)
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

        private void HandleFailure()
        {
            if (!challengeActive)
            {
                return;
            }

            challengeActive =
                false;

            lastChallengeSucceeded =
                false;

            lastChallengeFailed =
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
                    $"Step 8A에서는 즉사 확산 공격을 아직 실행하지 않습니다.",
                    this
                );
            }

            ForceDestroyRemainingInjectors();

            remainingInjectorCount =
                0;

            challengeRoutine =
                null;
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
            CleanupInjectors();
        }
    }
}
