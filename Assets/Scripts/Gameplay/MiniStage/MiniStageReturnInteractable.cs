using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageReturnInteractable : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("미니 스테이지 진행을 관리하는 MiniStageDirector입니다.")]
        [SerializeField] private MiniStageDirector miniStageDirector;

        [Tooltip("상호작용 가능 상태일 때 보여줄 안내 UI입니다. 예: 'E 복귀'. 없어도 작동합니다.")]
        [SerializeField] private GameObject unlockedGuide;

        [Tooltip("아직 상호작용 불가능할 때 보여줄 잠금 안내 UI입니다. 예: '스나이퍼를 모두 처치하고 보상을 획득하세요'. 없어도 작동합니다.")]
        [SerializeField] private GameObject lockedGuide;

        [Header("Interaction")]
        [Tooltip("원래 필드로 돌아갈 때 사용할 키입니다.")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Tooltip("상호작용 가능한 상태로 시작할지 여부입니다. 미니 스테이지 중앙 혈전은 false로 두는 것이 좋습니다.")]
        [SerializeField] private bool startUnlocked = false;

        [Tooltip("귀환 오브젝트 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private bool unlocked;
        private LineRenderer openingRipple;
        public const float OpeningPulseDuration = .34f;
        public bool ShowingOpeningPulse => openingRipple != null && openingRipple.enabled;
        private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
        private bool playerInside => playerColliders.Count > 0;

        [Header("Portal Visual")]
        [SerializeField] private SpriteRenderer portalRenderer;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedSprite;
        private bool travelOpen;
        private bool enhanced;
        private Color? authoredTint;
        public void SetTravelVisual(bool open, bool overcharged)
        {
            travelOpen=open; enhanced=overcharged;
            RefreshSprite();
        }
        private void RefreshSprite()
        {
            if(portalRenderer==null)portalRenderer=GetComponentInChildren<SpriteRenderer>(true);
            if (portalRenderer == null) return;
            if (!authoredTint.HasValue) authoredTint = portalRenderer.color;
            bool open = unlocked || travelOpen;
            var sprite = open ? unlockedSprite : lockedSprite;
            if (sprite != null) portalRenderer.sprite = sprite;
            // The enhanced art depicts an open hole. Keep the sealed silhouette during its mission.
            portalRenderer.color = enhanced && !open ? authoredTint.Value * new Color(.90f,.78f,1f) : authoredTint.Value;
            if (enhanced && open) BloodClotOverchargedArt.Apply(gameObject);
        }

        public bool IsUnlocked => unlocked;
        public bool CanInteract => isActiveAndEnabled && unlocked && playerInside &&
            miniStageDirector != null && miniStageDirector.CanReturnFromInteractable(this);

        private void OnDisable()
        {
            playerColliders.Clear();
            if (openingRipple != null) openingRipple.enabled = false;
            SetGuideVisible(false);
        }

        private void Awake()
        {
            BloodClotObstacle.Ensure(gameObject);
            InteractionFocus.Register(this, () => CanInteract);
            if (miniStageDirector == null)
            {
                miniStageDirector = FindObjectOfType<MiniStageDirector>();
            }

            SetUnlocked(startUnlocked);
            SetGuideVisible(false);
        }

        private void Update()
        {
            playerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            SetGuideVisible(CanInteract);
            if (!CanInteract || Time.timeScale <= 0f) return;
            if (!playerInside)
            {
                return;
            }

            if (!unlocked)
            {
                return;
            }

            if (Vampire.GameInput.TryConsumeInteraction(interactionKey, this))
            {
                if (debugLog)
                {
                    Debug.Log("[MiniStageReturnInteractable] E 입력 확인. 원래 필드 복귀 요청.");
                }

                if (miniStageDirector != null)
                {
                    miniStageDirector.ReturnToFieldFromInteractable(this);
                }
            }
        }

        public void SetUnlocked(bool value)
        {
            bool opening = value && !unlocked;
            unlocked = value;
            RefreshSprite();
            if (opening && Application.isPlaying && isActiveAndEnabled && portalRenderer != null)
                StartCoroutine(ShowOpeningPulse());

            if (debugLog)
            {
                Debug.Log($"[MiniStageReturnInteractable] 상호작용 가능 상태 변경: {unlocked}");
            }

            if (playerInside)
            {
                SetGuideVisible(true);
            }
            else
            {
                SetGuideVisible(false);
            }
        }

        private IEnumerator ShowOpeningPulse()
        {
            if (openingRipple == null)
            {
                var wave = new GameObject("혈전 개방 물결");
                wave.transform.SetParent(transform, false);
                openingRipple = wave.AddComponent<LineRenderer>();
                var material = new Material(Shader.Find("Sprites/Default"));
                wave.AddComponent<RemakeMaterialCleanup>().Material = material;
                openingRipple.sharedMaterial = material;
                openingRipple.useWorldSpace = false;
                openingRipple.loop = true;
                openingRipple.positionCount = 40;
                openingRipple.sortingLayerID = portalRenderer.sortingLayerID;
                openingRipple.sortingOrder = portalRenderer.sortingOrder + 2;
                for (int i = 0; i < openingRipple.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / openingRipple.positionCount;
                    openingRipple.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
                }
            }

            var geometry = PromptSpriteGeometry.WorldBounds(portalRenderer);
            var anchor = openingRipple.transform;
            anchor.position = geometry.center;
            var scale = transform.lossyScale;
            float width = Mathf.Clamp(geometry.extents.x, .42f, 1.25f) / Mathf.Max(.01f, Mathf.Abs(scale.x));
            float height = Mathf.Clamp(geometry.extents.y * .68f, .24f, .65f) / Mathf.Max(.01f, Mathf.Abs(scale.y));
            var color = enhanced ? new Color(1f,.85f,.44f) : new Color(.65f,1f,.83f);
            openingRipple.enabled = true;
            for (float elapsed = 0f; elapsed < OpeningPulseDuration && unlocked; elapsed += Time.deltaTime)
            {
                float t = elapsed / OpeningPulseDuration;
                float size = GamePreferences.Current.reducedMotion ? 1.15f : Mathf.Lerp(.60f, 1.65f, 1f - (1f-t)*(1f-t));
                anchor.localScale = new Vector3(width * size, height * size, 1f);
                color.a = 1f - t;
                openingRipple.startColor = openingRipple.endColor = color;
                openingRipple.widthMultiplier = Mathf.Lerp(.10f, .025f, t);
                yield return null;
            }
            openingRipple.enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null || other == character.CollectableCollider)
            {
                return;
            }

            playerColliders.Add(other);
            SetGuideVisible(true);

            if (debugLog)
            {
                if (unlocked)
                {
                    Debug.Log("[MiniStageReturnInteractable] 플레이어 진입. E키로 복귀 가능.");
                }
                else
                {
                    Debug.Log("[MiniStageReturnInteractable] 플레이어 진입. 아직 복귀 조건이 충족되지 않았습니다.");
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            playerColliders.Remove(other);
            SetGuideVisible(CanInteract);
        }

        private void SetGuideVisible(bool visible)
        {
            if (lockedGuide != null && lockedGuide != gameObject) lockedGuide.SetActive(false);
            PixelInteractionPrompt.Show(this, visible && CanInteract, unlockedGuide);
        }
    }
}
