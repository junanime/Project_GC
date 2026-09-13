using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 특수증강: 점막 요새
    /// 플레이어가 일정 시간 동안 실제 피해를 받지 않으면 점막 실드를 1개 생성한다.
    /// 최대 스택까지 누적되며, 피격 시 스택 1개를 소모하고 데미지를 0으로 만든다.
    ///
    /// 기존 Character.cs의 hasShield는 건드리지 않고,
    /// 이 특수증강 전용 실드를 별도 컴포넌트에서 관리한다.
    /// </summary>
    public class MucosalFortressShieldController : MonoBehaviour
    {
        private float noDamageSeconds = 5f;
        private int maxStacks = 3;
        private Color shieldColor = new Color(0.85f, 1f, 0.75f, 0.6f);
        private bool debugLog = false;

        private int currentStacks;
        private float lastDamageOrShieldConsumeTime;
        private bool configured;

        private readonly List<SyringeAugmentVfx> shieldVisuals = new List<SyringeAugmentVfx>();
        private Character ownerCharacter;
        [SerializeField, Tooltip("Radius of the innermost shield stack in world units.")]
        private float shieldRadius = 0.65f;
        [SerializeField, Tooltip("Spacing between visible shield stack membranes.")]
        private float stackRadiusStep = 0.12f;

        public int CurrentStacks => currentStacks;
        public int MaxStacks => maxStacks;

        /// <summary>
        /// SyringeDartAbility에서 호출한다.
        /// 수치 조절은 SyringeDartAbility 인스펙터에서 관리한다.
        /// </summary>
        public void Configure(
            float noDamageSeconds,
            int maxStacks,
            Color shieldColor,
            bool debugLog)
        {
            this.noDamageSeconds = Mathf.Max(0.5f, noDamageSeconds);
            this.maxStacks = Mathf.Max(1, maxStacks);
            this.shieldColor = shieldColor;
            this.debugLog = debugLog;

            ownerCharacter = GetComponent<Character>() ?? GetComponentInParent<Character>();
            configured = true;
            lastDamageOrShieldConsumeTime = Time.time;

            CreateVisualIfNeeded();
            UpdateVisual();

            if (debugLog)
            {
                Debug.Log("[점막 요새] 컨트롤러 설정 완료");
            }
        }

        private void Update()
        {
            if (!configured)
                return;

            if (ownerCharacter == null || ownerCharacter.CurrentHealth <= 0f || !ownerCharacter.gameObject.activeInHierarchy)
            {
                ReleaseVisuals();
                currentStacks = 0;
                return;
            }

            if (currentStacks >= maxStacks)
                return;

            float elapsed = Time.time - lastDamageOrShieldConsumeTime;

            if (elapsed >= noDamageSeconds)
            {
                currentStacks++;
                PlayShieldPulse("MucosalFortressCreate", currentStacks);
                lastDamageOrShieldConsumeTime = Time.time;

                UpdateVisual();

                if (debugLog)
                {
                    Debug.Log($"[점막 요새] 실드 생성: {currentStacks}/{maxStacks}");
                }
            }
        }

        /// <summary>
        /// Character.TakeDamage()에서 호출한다.
        /// 실드가 있으면 1개 소모하고 true를 반환한다.
        /// true가 반환되면 Character.TakeDamage()는 즉시 return해서 피해를 막는다.
        /// </summary>
        public bool TryConsumeShieldStack()
        {
            if (!configured)
                return false;

            if (currentStacks <= 0)
                return false;

            PlayShieldPulse("MucosalFortressBreak", currentStacks);
            currentStacks--;
            lastDamageOrShieldConsumeTime = Time.time;

            UpdateVisual();

            if (debugLog)
            {
                Debug.Log($"[점막 요새] 피격 차단, 남은 실드: {currentStacks}/{maxStacks}");
            }

            return true;
        }

        /// <summary>
        /// 실드 없이 실제 피해를 받았을 때 호출한다.
        /// 피해를 받았으므로 무피해 누적 시간을 다시 시작한다.
        /// </summary>
        public void NotifyPlayerDamaged()
        {
            if (!configured)
                return;

            lastDamageOrShieldConsumeTime = Time.time;

            if (debugLog)
            {
                Debug.Log("[점막 요새] 실제 피해 발생, 실드 충전 타이머 초기화");
            }
        }

        private void CreateVisualIfNeeded()
        {
            if (ownerCharacter == null || ownerCharacter.CurrentHealth <= 0f) return;
            while (shieldVisuals.Count < currentStacks)
            {
                var effect = SyringeAugmentVfx.Play("MucosalFortress", ownerCharacter.CenterTransform.position, SyringeAugmentVfx.FindTarget(ownerCharacter));
                if (effect == null) break;
                effect.BindTo(ownerCharacter.CenterTransform);
                shieldVisuals.Add(effect);
            }
        }

        private void PlayShieldPulse(string effectName, int stack)
        {
            if (ownerCharacter == null || ownerCharacter.CurrentHealth <= 0f) return;
            var effect = SyringeAugmentVfx.Play(effectName, ownerCharacter.CenterTransform.position, SyringeAugmentVfx.FindTarget(ownerCharacter));
            if (effect == null) return;
            effect.BindTo(ownerCharacter.CenterTransform);
            float diameter = 2f * (shieldRadius + Mathf.Max(0, stack - 1) * stackRadiusStep);
            effect.SetWorldSize(Vector2.one * diameter, Vector2.one * 0.86f);
        }

        private void UpdateVisual()
        {
            while (shieldVisuals.Count > currentStacks)
            {
                int last = shieldVisuals.Count - 1;
                shieldVisuals[last].Release();
                shieldVisuals.RemoveAt(last);
            }
            CreateVisualIfNeeded();
            for (int i = 0; i < shieldVisuals.Count; i++)
            {
                shieldVisuals[i].SetWorldSize(Vector2.one * (2f * (shieldRadius + i * stackRadiusStep)), Vector2.one * 0.86f);
                shieldVisuals[i].SetStrength(shieldColor.a);
            }
        }

        private void ReleaseVisuals()
        {
            foreach (var effect in shieldVisuals) if (effect != null) effect.Release();
            shieldVisuals.Clear();
        }

        private void OnDisable()
        {
            ReleaseVisuals();
            currentStacks = 0;
            lastDamageOrShieldConsumeTime = Time.time;
        }
        private void OnDestroy() { ReleaseVisuals(); }
    }
}
