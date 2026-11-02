using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 신경차단
    /// 일정 주기로 화면 안의 모든 몬스터에게 짧은 정지 상태를 부여한다.
    /// 신경차단이 활성화된 동안 새로 스폰된 몬스터도 남은 시간만큼 정지 상태에 들어간다.
    /// </summary>
    public class NeuralBlockController : MonoBehaviour
    {
        [Header("Neural Block / 신경차단")]
        [Tooltip("신경차단이 발동되는 주기입니다.")]
        [SerializeField] private float interval = 12f;

        [Tooltip("몬스터가 정지되는 시간입니다.")]
        [SerializeField] private float freezeDuration = 1f;

        [Tooltip("화면 밖 몬스터까지 살짝 포함할 여유값입니다. 0이면 정확히 화면 안만 포함합니다.")]
        [SerializeField] private float screenPadding = 0.08f;

        [Tooltip("몬스터 레이어입니다. root 레이어뿐 아니라 자식 Collider 레이어까지 검사합니다.")]
        [SerializeField] private LayerMask monsterLayer;

        [Tooltip("체크하면 신경차단 발동/대상 수 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Camera mainCamera;
        private float timer = 0f;
        private float activeUntilTime = -1f;

        private NeuralTimeStopVisual timeVisual;
        private Coroutine summonRoutine;

        public bool IsBlocking => Time.time < activeUntilTime;

        public void Configure(
            float interval,
            float freezeDuration,
            float screenPadding,
            LayerMask monsterLayer,
            bool debugLog)
        {
            this.interval = Mathf.Max(0.1f, interval);
            this.freezeDuration = Mathf.Max(0.05f, freezeDuration);
            this.screenPadding = Mathf.Max(0f, screenPadding);
            this.monsterLayer = monsterLayer;
            this.debugLog = debugLog;

            mainCamera = Camera.main;


        }

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (timeVisual != null) Destroy(timeVisual);
        }

        private void Update()
        {
            var source = GetComponent<SyringeDartAbility>();
            if (source != null && source.AttacksBlocked) return;
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            timer += Time.deltaTime;

            if (timer >= interval)
            {
                timer -= interval;
                if (summonRoutine == null) summonRoutine = StartCoroutine(SummonThenBlock());
            }

            // 신경차단이 활성화된 동안 새로 스폰된 몬스터도 남은 시간만큼 정지 상태에 넣는다.
            if (IsBlocking)
            {
                ApplyFreezeToScreenMonsters(activeUntilTime - Time.time);
            }
        }

        private void ActivateNeuralBlock()
        {
            activeUntilTime = Time.time + freezeDuration;

            int affectedCount = ApplyFreezeToScreenMonsters(freezeDuration);

            if (debugLog)
            {
                Debug.Log(
                    $"[신경차단] 발동 | Duration={freezeDuration:0.00}s | TargetCount={affectedCount}",
                    this
                );
            }

            if (timeVisual == null) timeVisual = gameObject.AddComponent<NeuralTimeStopVisual>();
            timeVisual.Begin(freezeDuration);
        }

        private IEnumerator SummonThenBlock()
        {
            var character = GetComponentInParent<Character>();
            if (character == null) character = FindObjectOfType<Character>();
            if (timeVisual == null) timeVisual = gameObject.AddComponent<NeuralTimeStopVisual>();
            timeVisual.Prepare(character);
            yield return new WaitForSeconds(PhoenixSummonVisual.RequiemDuration);
            if (character != null && character.CurrentHealth > 0 && character.gameObject.activeInHierarchy)
                ActivateNeuralBlock();
            summonRoutine = null;
        }
        private void OnDisable()
        {
            StopAllCoroutines(); summonRoutine = null;
            activeUntilTime = -1;
            if (timeVisual != null) timeVisual.Clear();
        }

        private int ApplyFreezeToScreenMonsters(float remainingDuration)
        {
            if (remainingDuration <= 0f)
            {
                return 0;
            }

            List<Monster> targets = GetScreenMonsters();
            int appliedCount = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                Monster monster = targets[i];

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                NeuralBlockedMonsterStatus status = monster.GetComponent<NeuralBlockedMonsterStatus>();

                if (status == null)
                {
                    status = monster.gameObject.AddComponent<NeuralBlockedMonsterStatus>();
                }

                status.Apply(remainingDuration, false);
                appliedCount++;
            }

            return appliedCount;
        }

        private List<Monster> GetScreenMonsters()
        {
            List<Monster> layerMatchedTargets = new List<Monster>();
            List<Monster> fallbackTargets = new List<Monster>();

            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster monster = monsters[i];

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 checkPosition = GetMonsterWorldPosition(monster);

                if (!IsInScreen(checkPosition))
                {
                    continue;
                }

                fallbackTargets.Add(monster);

                if (IsMonsterLayerMatched(monster))
                {
                    layerMatchedTargets.Add(monster);
                }
            }

            // 레이어가 root/child 구조 때문에 안 맞는 경우가 있어서,
            // 레이어 매칭 대상이 없으면 화면 안 Monster 전체를 fallback으로 사용한다.
            if (layerMatchedTargets.Count > 0)
            {
                return layerMatchedTargets;
            }

            return fallbackTargets;
        }

        private Vector3 GetMonsterWorldPosition(Monster monster)
        {
            if (monster != null && monster.CenterTransform != null)
            {
                return monster.CenterTransform.position;
            }

            return monster != null ? monster.transform.position : Vector3.zero;
        }

        private bool IsMonsterLayerMatched(Monster monster)
        {
            if (monster == null)
            {
                return false;
            }

            if (monsterLayer.value == 0)
            {
                return true;
            }

            if ((monsterLayer.value & (1 << monster.gameObject.layer)) != 0)
            {
                return true;
            }

            Collider2D[] colliders = monster.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];

                if (col == null)
                {
                    continue;
                }

                if ((monsterLayer.value & (1 << col.gameObject.layer)) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInScreen(Vector3 worldPosition)
        {
            if (mainCamera == null)
            {
                return true;
            }

            Vector3 viewportPoint = mainCamera.WorldToViewportPoint(worldPosition);

            if (viewportPoint.z < 0f)
            {
                return false;
            }

            return viewportPoint.x >= -screenPadding &&
                   viewportPoint.x <= 1f + screenPadding &&
                   viewportPoint.y >= -screenPadding &&
                   viewportPoint.y <= 1f + screenPadding;
        }

    }
}
