using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 필드 이벤트 '제산 거품 폭주' 전용 보조 디렉터입니다.
    ///
    /// StageEventDirector.cs를 크게 교체하지 않고,
    /// 씬에 별도 오브젝트로 붙여 테스트할 수 있게 만들었습니다.
    ///
    /// 이벤트 중에는 맵 주변에 안전 거품이 계속 생성되고,
    /// 플레이어가 어떤 거품 안에도 들어가 있지 않으면 지속 피해를 받습니다.
    /// </summary>
    public class AntacidBubbleSurgeEventController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("현재 레벨 진행 시간과 플레이어 참조를 가져올 LevelManager입니다. 비워두면 씬에서 자동 탐색합니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("이벤트 시작/경고 문구를 띄울 Toast UI입니다. 비워두면 씬에서 자동 탐색합니다.")]
        [SerializeField] private StageEventToastUI eventToastUI;

        [Tooltip("제산 거품 프리팹입니다. AntacidBubbleZone 컴포넌트가 붙어 있어야 합니다.")]
        [SerializeField] private GameObject bubblePrefab;

        [Tooltip("화면 가장자리 하얀 거품 경고 연출용 CanvasGroup입니다. 없어도 이벤트는 정상 작동합니다.")]
        [SerializeField] private CanvasGroup warningEdgeCanvasGroup;

        [Header("Event Info")]
        [Tooltip("UI와 로그에 표시될 이벤트 이름입니다.")]
        [SerializeField] private string eventName = "제산 거품 폭주";

        [Tooltip("이벤트를 사용할지 여부입니다.")]
        [SerializeField] private bool eventEnabled = true;

        [Header("Timing")]
        [Tooltip("이벤트 시작 시간입니다. 단위는 초입니다. 90 = 1분 30초")]
        [SerializeField] private float startTime = 90f;

        [Tooltip("이벤트 지속 시간입니다. 단위는 초입니다.")]
        [SerializeField] private float duration = 20f;

        [Tooltip("이벤트 시작 전 경고가 먼저 표시되는 시간입니다.")]
        [SerializeField] private float warningDuration = 2f;

        [Header("Bubble Spawn")]
        [Tooltip("거품 생성 간격입니다.")]
        [SerializeField] private float bubbleSpawnInterval = 1f;

        [Tooltip("한 번 생성할 때 만들어지는 거품 수입니다.")]
        [SerializeField] private int bubblesPerWave = 2;

        [Tooltip("동시에 존재할 수 있는 거품 최대 수입니다.")]
        [SerializeField] private int maxActiveBubbles = 8;

        [Tooltip("각 거품이 유지되는 시간입니다. 요구사항 기준 5초입니다.")]
        [SerializeField] private float bubbleLifetime = 5f;

        [Tooltip("거품이 사라질 때 페이드아웃되는 시간입니다.")]
        [SerializeField] private float bubbleFadeOutDuration = 0.35f;

        [Tooltip("거품 반경입니다.")]
        [SerializeField] private float bubbleRadius = 1.8f;

        [Tooltip("플레이어 기준 최소 생성 거리입니다.")]
        [SerializeField] private float minSpawnDistanceFromPlayer = 1.2f;

        [Tooltip("플레이어 기준 최대 생성 거리입니다.")]
        [SerializeField] private float maxSpawnDistanceFromPlayer = 6f;

        [Tooltip("다른 거품과 유지할 최소 거리입니다.")]
        [SerializeField] private float minDistanceBetweenBubbles = 2.5f;

        [Tooltip("거품 색상입니다.")]
        [SerializeField] private Color bubbleColor = new Color(1f, 1f, 1f, 0.65f);

        [Header("Outside Damage")]
        [Tooltip("거품 밖에 있을 때 피해를 받는 간격입니다.")]
        [SerializeField] private float outsideDamageTickInterval = 0.5f;

        [Tooltip("거품 밖에 있을 때 틱마다 받는 피해량입니다.")]
        [SerializeField] private float outsideDamagePerTick = 2f;

        [Tooltip("체크하면 이벤트 시작 직후 거품 밖에 있을 때 바로 첫 피해를 받을 수 있습니다.")]
        [SerializeField] private bool damageImmediatelyWhenOutside = false;

        [Header("Debug")]
        [Tooltip("체크하면 제산 거품 이벤트 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<AntacidBubbleZone> activeBubbles = new List<AntacidBubbleZone>();

        private bool warningShown;
        private bool started;
        private bool finished;

        private float eventEndTime;
        private float bubbleSpawnTimer;
        private float outsideDamageTimer;

        private void Start()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (eventToastUI == null)
            {
                eventToastUI = FindObjectOfType<StageEventToastUI>();
            }

            SetWarningEdgeAlpha(0f);
        }

        private void Update()
        {
            if (!eventEnabled || finished || levelManager == null)
            {
                return;
            }

            float currentTime = levelManager.CurrentLevelTime;

            if (!warningShown && currentTime >= Mathf.Max(0f, startTime - warningDuration))
            {
                warningShown = true;
                ShowToast("제산 반응 발생");
                SetWarningEdgeAlpha(1f);

                if (debugLog)
                {
                    Debug.Log($"[제산 거품 폭주] 경고 시작 | time={currentTime:F1}s", this);
                }
            }

            if (!started && currentTime >= startTime)
            {
                StartEvent(currentTime);
            }

            if (!started)
            {
                return;
            }

            if (currentTime >= eventEndTime)
            {
                EndEvent(currentTime);
                return;
            }

            TickBubbleSpawn();
            TickOutsideDamage();
            CleanNullBubbles();
        }

        private void StartEvent(float currentTime)
        {
            started = true;
            eventEndTime = currentTime + Mathf.Max(0.1f, duration);
            bubbleSpawnTimer = 0f;
            outsideDamageTimer = damageImmediatelyWhenOutside
                ? Mathf.Max(0.01f, outsideDamageTickInterval)
                : 0f;

            ShowToast($"{eventName} 이벤트가 시작됐습니다!");
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.FieldEventStart
);
            SpawnBubbleWave();

            if (debugLog)
            {
                Debug.Log(
                    $"[제산 거품 폭주] 시작 | time={currentTime:F1}s | " +
                    $"duration={duration:F1}s | end={eventEndTime:F1}s",
                    this);
            }
        }

        private void EndEvent(float currentTime)
        {
            finished = true;
            SetWarningEdgeAlpha(0f);

            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                if (activeBubbles[i] != null)
                {
                    Destroy(activeBubbles[i].gameObject);
                }
            }

            activeBubbles.Clear();

            if (debugLog)
            {
                Debug.Log($"[제산 거품 폭주] 종료 | time={currentTime:F1}s", this);
            }
        }

        private void TickBubbleSpawn()
        {
            if (bubblePrefab == null)
            {
                return;
            }

            CleanNullBubbles();

            bubbleSpawnTimer += Time.deltaTime;

            if (bubbleSpawnTimer < Mathf.Max(0.05f, bubbleSpawnInterval))
            {
                return;
            }

            bubbleSpawnTimer = 0f;
            SpawnBubbleWave();
        }

        private void SpawnBubbleWave()
        {
            CleanNullBubbles();

            int spawnCount = Mathf.Max(1, bubblesPerWave);

            for (int i = 0; i < spawnCount; i++)
            {
                if (activeBubbles.Count >= Mathf.Max(1, maxActiveBubbles))
                {
                    return;
                }

                TrySpawnBubble();
            }
        }

        private void TrySpawnBubble()
        {
            Character playerCharacter = levelManager.PlayerCharacter;

            if (playerCharacter == null)
            {
                return;
            }

            Vector3 selectedPosition = playerCharacter.transform.position;
            bool foundPosition = false;

            for (int i = 0; i < 24; i++)
            {
                Vector3 candidate = GetRandomPositionAroundPlayer(playerCharacter);

                if (IsFarEnoughFromOtherBubbles(candidate))
                {
                    selectedPosition = candidate;
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                if (debugLog)
                {
                    Debug.LogWarning("[제산 거품 폭주] 다른 거품과의 최소 거리 조건을 만족하는 위치를 찾지 못했습니다.", this);
                }

                return;
            }

            GameObject bubbleObject = Instantiate(bubblePrefab, selectedPosition, Quaternion.identity);
            AntacidBubbleZone bubbleZone = bubbleObject.GetComponent<AntacidBubbleZone>();

            if (bubbleZone == null)
            {
                bubbleZone = bubbleObject.AddComponent<AntacidBubbleZone>();
            }

            bubbleZone.Init(
                bubbleRadius,
                bubbleLifetime,
                bubbleFadeOutDuration,
                bubbleColor,
                debugLog);

            activeBubbles.Add(bubbleZone);
            // 거품이 실제 생성되고 초기화까지 완료된 순간
            // 거품 하나당 1회 재생합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.AntacidBubbleSpawn
            );
            if (debugLog)
            {
                Debug.Log($"[제산 거품 폭주] 거품 생성 | pos={selectedPosition}", bubbleZone);
            }
        }

        private Vector3 GetRandomPositionAroundPlayer(Character playerCharacter)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            if (randomDirection == Vector2.zero)
            {
                randomDirection = Vector2.up;
            }

            float safeMin = Mathf.Max(0f, minSpawnDistanceFromPlayer);
            float safeMax = Mathf.Max(safeMin, maxSpawnDistanceFromPlayer);
            float distance = Random.Range(safeMin, safeMax);

            return playerCharacter.transform.position + (Vector3)(randomDirection * distance);
        }

        private bool IsFarEnoughFromOtherBubbles(Vector3 candidate)
        {
            float minDistance = Mathf.Max(0f, minDistanceBetweenBubbles);

            for (int i = 0; i < activeBubbles.Count; i++)
            {
                AntacidBubbleZone bubble = activeBubbles[i];

                if (bubble == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(candidate, bubble.transform.position);

                if (distance < minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private void TickOutsideDamage()
        {
            Character playerCharacter = levelManager.PlayerCharacter;

            if (playerCharacter == null)
            {
                return;
            }

            bool playerInsideBubble = IsPlayerInsideAnyBubble(playerCharacter);

            if (playerInsideBubble)
            {
                outsideDamageTimer = 0f;
                return;
            }

            outsideDamageTimer += Time.deltaTime;

            if (outsideDamageTimer < Mathf.Max(0.05f, outsideDamageTickInterval))
            {
                return;
            }

            outsideDamageTimer = 0f;

            float damage = Mathf.Max(0f, outsideDamagePerTick);

            if (damage <= 0f)
            {
                return;
            }

            playerCharacter.TakeDamage(damage, Vector2.zero, false);

            if (debugLog)
            {
                Debug.Log($"[제산 거품 폭주] 거품 밖 피해 | Damage={damage}", playerCharacter);
            }
        }

        private bool IsPlayerInsideAnyBubble(Character playerCharacter)
        {
            Vector2 playerPosition = playerCharacter.CenterTransform != null
                ? playerCharacter.CenterTransform.position
                : playerCharacter.transform.position;

            CleanNullBubbles();

            for (int i = 0; i < activeBubbles.Count; i++)
            {
                AntacidBubbleZone bubble = activeBubbles[i];

                if (bubble == null)
                {
                    continue;
                }

                if (bubble.ContainsPoint(playerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanNullBubbles()
        {
            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                if (activeBubbles[i] == null)
                {
                    activeBubbles.RemoveAt(i);
                }
            }
        }

        private void ShowToast(string message)
        {
            if (eventToastUI != null)
            {
                eventToastUI.Show(message);
            }

            if (debugLog)
            {
                Debug.Log($"[제산 거품 폭주 UI] {message}", this);
            }
        }

        private void SetWarningEdgeAlpha(float alpha)
        {
            if (warningEdgeCanvasGroup == null)
            {
                return;
            }

            warningEdgeCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        [ContextMenu("Test Start Antacid Bubble Surge Now")]
        private void TestStartNow()
        {
            if (started && !finished)
            {
                return;
            }

            warningShown = true;
            started = false;
            finished = false;
            SetWarningEdgeAlpha(1f);

            float currentTime = levelManager != null ? levelManager.CurrentLevelTime : 0f;
            StartEvent(currentTime);
        }
    }
}