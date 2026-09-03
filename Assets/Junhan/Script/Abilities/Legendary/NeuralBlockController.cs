using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Screen Overlay / 화면 표시")]
        [Tooltip("체크하면 신경차단 발동 중 화면에 투명한 분홍색 오버레이를 표시합니다.")]
        [SerializeField] private bool showScreenOverlay = true;

        [Tooltip("신경차단 발동 중 화면에 깔리는 색상입니다.")]
        [SerializeField] private Color overlayColor = new Color(1f, 0.18f, 0.62f, 0.22f);

        [Tooltip("오버레이가 나타날 때 걸리는 시간입니다.")]
        [SerializeField] private float overlayFadeInDuration = 0.08f;

        [Tooltip("오버레이가 사라질 때 걸리는 시간입니다.")]
        [SerializeField] private float overlayFadeOutDuration = 0.16f;

        [Tooltip("오버레이 Canvas의 Sorting Order입니다. 높을수록 UI 위에 보입니다.")]
        [SerializeField] private int overlaySortingOrder = 5000;

        private Camera mainCamera;
        private float timer = 0f;
        private float activeUntilTime = -1f;

        private Canvas overlayCanvas;
        private Image overlayImage;
        private Coroutine overlayRoutine;

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

            if (showScreenOverlay)
            {
                EnsureOverlayExists();
            }
        }

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (overlayCanvas != null)
            {
                Destroy(overlayCanvas.gameObject);
            }
        }

        private void Update()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            timer += Time.deltaTime;

            if (timer >= interval)
            {
                timer -= interval;
                ActivateNeuralBlock();
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

            PlayScreenOverlay(freezeDuration);
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

                status.Apply(remainingDuration);
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

        private void EnsureOverlayExists()
        {
            if (overlayCanvas != null && overlayImage != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Neural Block Screen Overlay Canvas");
            canvasObject.transform.SetParent(transform, false);

            overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = overlaySortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject imageObject = new GameObject("Neural Block Pink Overlay");
            imageObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayImage = imageObject.AddComponent<Image>();
            overlayImage.raycastTarget = false;

            Color hiddenColor = overlayColor;
            hiddenColor.a = 0f;
            overlayImage.color = hiddenColor;

            canvasObject.SetActive(false);
        }

        private void PlayScreenOverlay(float duration)
        {
            if (!showScreenOverlay)
            {
                return;
            }

            EnsureOverlayExists();

            if (overlayImage == null || overlayCanvas == null)
            {
                return;
            }

            if (overlayRoutine != null)
            {
                StopCoroutine(overlayRoutine);
            }

            overlayRoutine = StartCoroutine(ScreenOverlayRoutine(duration));
        }

        private IEnumerator ScreenOverlayRoutine(float duration)
        {
            overlayCanvas.gameObject.SetActive(true);

            float totalDuration = Mathf.Max(0.05f, duration);
            float fadeIn = Mathf.Min(Mathf.Max(0.01f, overlayFadeInDuration), totalDuration * 0.4f);
            float fadeOut = Mathf.Min(Mathf.Max(0.01f, overlayFadeOutDuration), totalDuration * 0.5f);

            float startTime = Time.time;
            float endTime = startTime + totalDuration;

            while (Time.time < endTime)
            {
                float elapsed = Time.time - startTime;
                float remaining = endTime - Time.time;

                float alphaMultiplier = 1f;

                if (elapsed < fadeIn)
                {
                    alphaMultiplier = Mathf.InverseLerp(0f, fadeIn, elapsed);
                }
                else if (remaining < fadeOut)
                {
                    alphaMultiplier = Mathf.InverseLerp(0f, fadeOut, remaining);
                }

                Color c = overlayColor;
                c.a = overlayColor.a * Mathf.Clamp01(alphaMultiplier);
                overlayImage.color = c;

                yield return null;
            }

            Color hiddenColor = overlayColor;
            hiddenColor.a = 0f;
            overlayImage.color = hiddenColor;

            overlayCanvas.gameObject.SetActive(false);
            overlayRoutine = null;
        }
    }
}