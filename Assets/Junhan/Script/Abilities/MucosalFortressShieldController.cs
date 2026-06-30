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

        private Transform visualRoot;
        private LineRenderer[] shieldRings;

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

            if (currentStacks >= maxStacks)
                return;

            float elapsed = Time.time - lastDamageOrShieldConsumeTime;

            if (elapsed >= noDamageSeconds)
            {
                currentStacks++;
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
            if (visualRoot != null)
                return;

            GameObject rootObject = new GameObject("Mucosal_Fortress_Shield_Visual");
            rootObject.transform.SetParent(transform);
            rootObject.transform.localPosition = Vector3.zero;

            visualRoot = rootObject.transform;
            shieldRings = new LineRenderer[maxStacks];

            for (int i = 0; i < maxStacks; i++)
            {
                GameObject ringObject = new GameObject($"Shield_Ring_{i + 1}");
                ringObject.transform.SetParent(visualRoot);
                ringObject.transform.localPosition = Vector3.zero;

                LineRenderer line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 72;
                line.startWidth = 0.035f;
                line.endWidth = 0.035f;
                line.startColor = shieldColor;
                line.endColor = shieldColor;
                line.sortingOrder = 60;
                line.material = new Material(Shader.Find("Sprites/Default"));

                float radius = 0.65f + i * 0.12f;
                ApplyRingPositions(line, radius);

                shieldRings[i] = line;
            }
        }

        private void ApplyRingPositions(LineRenderer line, float radius)
        {
            int count = line.positionCount;

            for (int i = 0; i < count; i++)
            {
                float angle = ((float)i / count) * Mathf.PI * 2f;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);

                line.SetPosition(i, position);
            }
        }

        private void UpdateVisual()
        {
            CreateVisualIfNeeded();

            for (int i = 0; i < shieldRings.Length; i++)
            {
                if (shieldRings[i] != null)
                {
                    shieldRings[i].gameObject.SetActive(i < currentStacks);
                }
            }
        }
    }
}