using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 실제 침 발사 성공 이벤트만 받아 날개 한 쪽을 재생한다.
    /// 몸/다리/머리띠 걷기 애니메이션은 기존 SpriteAnimator가 계속 담당한다.
    /// </summary>
    public sealed class CharacterSyringeAttackAnimator : MonoBehaviour
    {
        private Character owner;
        private CharacterBlueprint blueprint;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer wingRenderer;
        private Sprite[] activeSequence;
        private bool nextUseFrontWing = true;
        private bool activeWingIsFront;
        private float elapsed;
        private float duration;
        private int lastShotFrame = -1;

        public bool IsPlaying => activeSequence != null && activeSequence.Length > 0;
        public bool ActiveWingIsFront => activeWingIsFront;

        public void Init(Character character, CharacterBlueprint characterBlueprint, SpriteRenderer characterRenderer)
        {
            owner = character;
            blueprint = characterBlueprint;
            bodyRenderer = characterRenderer;
            EnsureRenderer();
            Hide();
        }

        public void PlayShot(float effectiveShotInterval)
        {
            if (blueprint == null || bodyRenderer == null)
            {
                return;
            }

            // 샷건 펠릿이나 양극침 한 쌍처럼 같은 프레임에 생성된 발사체는
            // 날개 동작 하나로 묶는다.
            if (lastShotFrame == Time.frameCount)
            {
                return;
            }

            lastShotFrame = Time.frameCount;
            activeWingIsFront = nextUseFrontWing;
            nextUseFrontWing = !nextUseFrontWing;
            activeSequence = activeWingIsFront
                ? blueprint.syringeFrontWingAttackSpriteSequence
                : blueprint.syringeRearWingAttackSpriteSequence;

            if (activeSequence == null || activeSequence.Length == 0)
            {
                Hide();
                return;
            }

            float minDuration = Mathf.Max(0.01f, blueprint.syringeAttackMinDuration);
            float maxDuration = Mathf.Max(minDuration, blueprint.syringeAttackMaxDuration);
            duration = Mathf.Clamp(
                Mathf.Max(0.01f, effectiveShotInterval) * blueprint.syringeAttackDurationRatio,
                minDuration,
                maxDuration);
            elapsed = 0f;

            EnsureRenderer();
            ApplyPresentation();
            wingRenderer.sprite = activeSequence[0];
            wingRenderer.enabled = owner == null || !owner.IsDashing;
        }

        private void LateUpdate()
        {
            if (!IsPlaying || wingRenderer == null)
            {
                return;
            }

            if (owner != null && owner.IsDashing)
            {
                wingRenderer.enabled = false;
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Hide();
                return;
            }

            wingRenderer.enabled = true;
            ApplyPresentation();

            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            int frameIndex = Mathf.Min(
                activeSequence.Length - 1,
                Mathf.FloorToInt(normalizedTime * activeSequence.Length));
            wingRenderer.sprite = activeSequence[frameIndex];
        }

        private void EnsureRenderer()
        {
            if (wingRenderer != null)
            {
                return;
            }

            Transform visualParent = bodyRenderer != null
                ? bodyRenderer.transform
                : transform;
            Transform existing = visualParent.Find("SyringeAttackWingVisual");
            GameObject visual = existing != null
                ? existing.gameObject
                : new GameObject("SyringeAttackWingVisual");
            visual.transform.SetParent(visualParent, false);
            wingRenderer = visual.GetComponent<SpriteRenderer>();
            if (wingRenderer == null)
            {
                wingRenderer = visual.AddComponent<SpriteRenderer>();
            }
        }

        private void ApplyPresentation()
        {
            bool bodyFacesLeft = bodyRenderer.flipX;
            float facingSign = bodyFacesLeft ? -1f : 1f;
            float sideSign = activeWingIsFront ? 1f : -1f;

            wingRenderer.flipX = activeWingIsFront ? !bodyFacesLeft : bodyFacesLeft;
            wingRenderer.color = bodyRenderer.color;
            wingRenderer.sharedMaterial = bodyRenderer.sharedMaterial;
            wingRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            wingRenderer.sortingOrder = bodyRenderer.sortingOrder + (activeWingIsFront ? 1 : -1);
            wingRenderer.transform.localPosition = new Vector3(
                sideSign * facingSign * blueprint.syringeAttackWingAnchorDistance,
                blueprint.syringeAttackWingVerticalOffset,
                0f);
        }

        public void Hide()
        {
            activeSequence = null;
            elapsed = 0f;
            if (wingRenderer != null)
            {
                wingRenderer.enabled = false;
                wingRenderer.sprite = null;
            }
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
