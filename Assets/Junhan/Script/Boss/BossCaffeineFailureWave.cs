using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 카페인 주입기 실패 시 보스 중심에서 바깥으로 퍼지는 전멸 파동입니다.
    ///
    /// 판정 방식:
    /// - Telegraph 동안은 피해 없음.
    /// - Expansion 단계에서 원형 파동 전면이 바깥으로 확장됩니다.
    /// - 이전 반경 → 현재 반경 구간을 Sweep하여 고속 확장 판정 누락을 줄입니다.
    /// - 플레이어의 이전/현재 보스 중심 거리도 함께 비교하여
    ///   플레이어 이동으로 파동을 교차하는 경우를 잡습니다.
    ///
    /// 피해 규칙:
    /// - Character.TakeDamage를 사용합니다.
    /// - 따라서 현재 프로젝트의 Dash 무적 / 일반 무적 / 점막 요새 / 기존 Shield /
    ///   Revive 규칙을 그대로 존중합니다.
    /// - 방어가 적용되지 않은 일반 상태에서는 사실상 즉사하도록 큰 피해를 계산합니다.
    ///
    /// 이 스크립트는 보스 전용 피해/아이템 효과를 나중에 연결할 수 있도록
    /// 별도 런타임 오브젝트로 분리되어 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossCaffeineFailureWave :
        MonoBehaviour
    {
        public enum WaveState
        {
            Telegraph = 0,
            Expanding = 1,
            Finished = 2
        }

        [Header("Runtime - Read Only")]

        [Tooltip("현재 전멸 파동 상태입니다.")]
        [SerializeField]
        private WaveState currentState =
            WaveState.Telegraph;

        [Tooltip("현재 파동 반경입니다.")]
        [SerializeField]
        private float currentRadius;

        [Tooltip("현재 파동이 플레이어에게 적중했는지 표시합니다.")]
        [SerializeField]
        private bool playerHit;

        [Tooltip("현재 파동이 종료되었는지 표시합니다.")]
        [SerializeField]
        private bool isFinished;

        [Header("Debug")]

        [Tooltip("파동 상태 전환/플레이어 적중 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog;

        private Character targetCharacter;

        private Vector2 origin;

        private float telegraphDuration;
        private float expansionDuration;

        private float startRadius;
        private float maxRadius;
        private float hitThickness;

        private float lethalDamageMultiplier;

        private bool drawRuntimeRing;
        private int ringSegments;
        private float ringWidth;
        private Color ringColor;
        private int ringSortingOrder;
        private float endLinger;

        private float stateStartTime;
        private float previousRadius;
        private float previousPlayerDistance;

        private LineRenderer lineRenderer;
        private Material runtimeLineMaterial;

        public WaveState CurrentState =>
            currentState;

        public float CurrentRadius =>
            currentRadius;

        public bool PlayerHit =>
            playerHit;

        public bool IsFinished =>
            isFinished;

        public void Setup(
            Character target,
            Vector2 waveOrigin,
            float newTelegraphDuration,
            float newExpansionDuration,
            float newStartRadius,
            float newMaxRadius,
            float newHitThickness,
            float newLethalDamageMultiplier,
            bool shouldDrawRuntimeRing,
            int newRingSegments,
            float newRingWidth,
            Color newRingColor,
            int newRingSortingOrder,
            float newEndLinger,
            bool enableDebugLog)
        {
            targetCharacter =
                target;

            origin =
                waveOrigin;

            telegraphDuration =
                Mathf.Max(
                    0f,
                    newTelegraphDuration);

            expansionDuration =
                Mathf.Max(
                    0.05f,
                    newExpansionDuration);

            startRadius =
                Mathf.Max(
                    0f,
                    newStartRadius);

            maxRadius =
                Mathf.Max(
                    startRadius + 0.01f,
                    newMaxRadius);

            hitThickness =
                Mathf.Max(
                    0.05f,
                    newHitThickness);

            lethalDamageMultiplier =
                Mathf.Max(
                    0.1f,
                    newLethalDamageMultiplier);

            drawRuntimeRing =
                shouldDrawRuntimeRing;

            ringSegments =
                Mathf.Clamp(
                    newRingSegments,
                    24,
                    180);

            ringWidth =
                Mathf.Max(
                    0.01f,
                    newRingWidth);

            ringColor =
                newRingColor;

            ringSortingOrder =
                newRingSortingOrder;

            endLinger =
                Mathf.Max(
                    0f,
                    newEndLinger);

            debugLog =
                enableDebugLog;

            currentState =
                WaveState.Telegraph;

            currentRadius =
                startRadius;

            previousRadius =
                startRadius;

            previousPlayerDistance =
                GetPlayerDistance();

            playerHit =
                false;

            isFinished =
                false;

            stateStartTime =
                Time.time;

            transform.position =
                origin;

            SetupRuntimeRing();
            UpdateRuntimeRing(
                currentRadius);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineFailureWave] START | " +
                    $"Telegraph={telegraphDuration:0.##}s, " +
                    $"Expand={expansionDuration:0.##}s, " +
                    $"Radius={startRadius:0.##}->{maxRadius:0.##}",
                    this
                );
            }

            if (telegraphDuration <= 0f)
            {
                BeginExpansion();
            }
        }

        private void Update()
        {
            if (isFinished)
            {
                return;
            }

            if (targetCharacter == null)
            {
                FinishWave();
                return;
            }

            switch (currentState)
            {
                case WaveState.Telegraph:
                    UpdateTelegraph();
                    break;

                case WaveState.Expanding:
                    UpdateExpansion();
                    break;
            }
        }

        private void UpdateTelegraph()
        {
            currentRadius =
                startRadius;

            UpdateRuntimeRing(
                currentRadius);

            if (Time.time -
                stateStartTime >=
                telegraphDuration)
            {
                BeginExpansion();
            }
        }

        private void BeginExpansion()
        {
            currentState =
                WaveState.Expanding;

            stateStartTime =
                Time.time;

            previousRadius =
                startRadius;

            currentRadius =
                startRadius;

            previousPlayerDistance =
                GetPlayerDistance();

            if (debugLog)
            {
                Debug.Log(
                    "[BossCaffeineFailureWave] Telegraph END -> EXPAND",
                    this
                );
            }
        }

        private void UpdateExpansion()
        {
            float elapsed =
                Time.time -
                stateStartTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    expansionDuration);

            float nextRadius =
                Mathf.Lerp(
                    startRadius,
                    maxRadius,
                    t);

            float currentPlayerDistance =
                GetPlayerDistance();

            if (!playerHit &&
                DidWaveCrossPlayer(
                    previousRadius,
                    nextRadius,
                    previousPlayerDistance,
                    currentPlayerDistance))
            {
                ApplyLethalHit();
            }

            previousRadius =
                nextRadius;

            currentRadius =
                nextRadius;

            previousPlayerDistance =
                currentPlayerDistance;

            UpdateRuntimeRing(
                currentRadius);

            if (t >= 1f)
            {
                FinishWave();
            }
        }

        /// <summary>
        /// 파동의 이전/현재 반경과 플레이어의 이전/현재 중심 거리를
        /// 각각 구간으로 보고 서로 겹치는지 검사합니다.
        /// </summary>
        private bool DidWaveCrossPlayer(
            float previousWaveRadius,
            float nextWaveRadius,
            float previousDistance,
            float nextDistance)
        {
            float halfThickness =
                hitThickness *
                0.5f;

            float waveMin =
                Mathf.Min(
                    previousWaveRadius,
                    nextWaveRadius) -
                halfThickness;

            float waveMax =
                Mathf.Max(
                    previousWaveRadius,
                    nextWaveRadius) +
                halfThickness;

            float playerMin =
                Mathf.Min(
                    previousDistance,
                    nextDistance);

            float playerMax =
                Mathf.Max(
                    previousDistance,
                    nextDistance);

            return
                playerMax >= waveMin &&
                playerMin <= waveMax;
        }

        private void ApplyLethalHit()
        {
            if (targetCharacter == null ||
                playerHit)
            {
                return;
            }

            playerHit =
                true;

            float beforeHp =
                targetCharacter.CurrentHealth;

            float lethalDamage =
                Mathf.Max(
                    1f,
                    targetCharacter.CurrentHealth +
                    targetCharacter.CurrentArmor +
                    targetCharacter.MaxHealth *
                    lethalDamageMultiplier +
                    1f);

            targetCharacter.TakeDamage(
                lethalDamage,
                Vector2.zero,
                false);

            float afterHp =
                targetCharacter.CurrentHealth;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineFailureWave] PLAYER HIT | " +
                    $"Requested={lethalDamage:0.##}, " +
                    $"HP={beforeHp:0.##}->{afterHp:0.##} | " +
                    $"Character.TakeDamage defense rules preserved",
                    this
                );
            }
        }

        private float GetPlayerDistance()
        {
            if (targetCharacter == null)
            {
                return float.MaxValue;
            }

            Transform targetTransform =
                targetCharacter.CenterTransform != null
                    ? targetCharacter.CenterTransform
                    : targetCharacter.transform;

            return
                Vector2.Distance(
                    origin,
                    targetTransform.position);
        }

        private void SetupRuntimeRing()
        {
            if (!drawRuntimeRing)
            {
                return;
            }

            lineRenderer =
                GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                lineRenderer =
                    gameObject.AddComponent
                    <
                        LineRenderer
                    >();
            }

            lineRenderer.useWorldSpace =
                false;

            lineRenderer.loop =
                true;

            lineRenderer.positionCount =
                ringSegments;

            lineRenderer.startWidth =
                ringWidth;

            lineRenderer.endWidth =
                ringWidth;

            lineRenderer.startColor =
                ringColor;

            lineRenderer.endColor =
                ringColor;

            lineRenderer.numCapVertices =
                0;

            lineRenderer.numCornerVertices =
                2;

            lineRenderer.sortingOrder =
                ringSortingOrder;

            Shader shader =
                Shader.Find(
                    "Sprites/Default");

            if (shader != null)
            {
                runtimeLineMaterial =
                    new Material(
                        shader);

                runtimeLineMaterial.name =
                    "Runtime_CaffeineFailureWave";

                lineRenderer.sharedMaterial =
                    runtimeLineMaterial;
            }
        }

        private void UpdateRuntimeRing(
            float radius)
        {
            if (lineRenderer == null)
            {
                return;
            }

            int count =
                Mathf.Max(
                    24,
                    ringSegments);

            if (lineRenderer.positionCount !=
                count)
            {
                lineRenderer.positionCount =
                    count;
            }

            for (int i = 0;
                 i < count;
                 i++)
            {
                float angle =
                    (
                        (float)i /
                        count
                    ) *
                    Mathf.PI *
                    2f;

                lineRenderer.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) *
                        radius,
                        Mathf.Sin(angle) *
                        radius,
                        0f));
            }
        }

        public void CancelWave()
        {
            if (isFinished)
            {
                return;
            }

            if (debugLog)
            {
                Debug.Log(
                    "[BossCaffeineFailureWave] CANCEL",
                    this
                );
            }

            isFinished =
                true;

            currentState =
                WaveState.Finished;

            DestroyRuntimeMaterial();

            Destroy(
                gameObject);
        }

        private void FinishWave()
        {
            if (isFinished)
            {
                return;
            }

            isFinished =
                true;

            currentState =
                WaveState.Finished;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineFailureWave] FINISH | " +
                    $"PlayerHit={playerHit}",
                    this
                );
            }

            DestroyRuntimeMaterial();

            Destroy(
                gameObject,
                endLinger);
        }

        private void DestroyRuntimeMaterial()
        {
            if (runtimeLineMaterial == null)
            {
                return;
            }

            Destroy(
                runtimeLineMaterial);

            runtimeLineMaterial =
                null;
        }

        private void OnDestroy()
        {
            if (runtimeLineMaterial != null)
            {
                Destroy(
                    runtimeLineMaterial);

                runtimeLineMaterial =
                    null;
            }
        }
    }
}
