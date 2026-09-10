using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 특수증강: 위산 연동파
    /// 플레이어를 중심으로 일정 주기마다 원형 위산 파동을 발사한다.
    ///
    /// 기존 SyringeDartAbility의 발사체 구조를 갈아엎지 않고,
    /// 플레이어에게 런타임 컨트롤러를 붙여 독립적으로 동작하게 만든다.
    /// 피해 대상은 Monster 고정이 아니라 IDamageable 기준으로 찾는다.
    /// </summary>
    public class GastricPeristalsisWaveController : MonoBehaviour
    {
        private SyringeDartAbility sourceAbility;
        private Character ownerCharacter;
        private LayerMask targetLayer;

        private float interval = 6f;
        private float maxRadius = 5.5f;
        private float waveDuration = 0.65f;
        private float waveThickness = 0.35f;
        private Color waveColor = new Color(0.55f, 1f, 0.15f, 0.65f);
        private bool debugLog = false;

        private float timer;
        private bool configured;
        private bool isEmitting;

        /// <summary>
        /// SyringeDartAbility에서 호출한다.
        /// 수치 조절은 SyringeDartAbility 인스펙터 값으로 관리한다.
        /// </summary>
        public void Configure(
            SyringeDartAbility sourceAbility,
            Character ownerCharacter,
            LayerMask targetLayer,
            float interval,
            float maxRadius,
            float waveDuration,
            float waveThickness,
            Color waveColor,
            bool debugLog)
        {
            this.sourceAbility = sourceAbility;
            this.ownerCharacter = ownerCharacter;
            this.targetLayer = targetLayer;
            this.interval = Mathf.Max(0.2f, interval);
            this.maxRadius = Mathf.Max(0.5f, maxRadius);
            this.waveDuration = Mathf.Max(0.1f, waveDuration);
            this.waveThickness = Mathf.Max(0.05f, waveThickness);
            this.waveColor = waveColor;
            this.debugLog = debugLog;

            configured = true;
            timer = this.interval;

            if (debugLog)
            {
                Debug.Log("[위산 연동파] 컨트롤러 설정 완료");
            }
        }

        private void Update()
        {
            if (!configured || sourceAbility == null || ownerCharacter == null)
                return;

            if (isEmitting)
                return;

            timer += Time.deltaTime;

            if (timer >= interval)
            {
                timer = 0f;
                StartCoroutine(EmitWaveRoutine());
            }
        }

        /// <summary>
        /// 실제 원형 파동을 발사한다.
        /// 같은 파동에서는 같은 대상이 한 번만 피해를 받는다.
        /// </summary>
        private IEnumerator EmitWaveRoutine()
        {
            isEmitting = true;

            GameObject visualObject = CreateWaveVisualObject();
            LineRenderer lineRenderer = visualObject.GetComponent<LineRenderer>();

            HashSet<MonoBehaviour> hitTargets = new HashSet<MonoBehaviour>();

            float elapsed = 0f;

            if (debugLog)
            {
                Debug.Log("[위산 연동파] 파동 발사");
            }

            while (elapsed < waveDuration)
            {
                if (ownerCharacter == null)
                    break;

                elapsed += Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / waveDuration);
                float currentRadius = Mathf.Lerp(0.15f, maxRadius, progress);

                Vector3 center = ownerCharacter.transform.position;

                visualObject.transform.position = center;
                UpdateRingVisual(lineRenderer, currentRadius);
                DamageTargetsOnRing(center, currentRadius, hitTargets);

                yield return null;
            }

            Destroy(visualObject);
            isEmitting = false;
        }

        /// <summary>
        /// 별도 프리팹 없이 테스트 가능한 원형 LineRenderer를 만든다.
        /// </summary>
        private GameObject CreateWaveVisualObject()
        {
            GameObject obj = new GameObject("Gastric_Peristalsis_Wave_Visual");
            LineRenderer line = obj.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 72;
            line.startWidth = waveThickness;
            line.endWidth = waveThickness;
            line.startColor = waveColor;
            line.endColor = waveColor;
            line.sortingOrder = 50;
            line.material = new Material(Shader.Find("Sprites/Default"));

            return obj;
        }

        private void UpdateRingVisual(LineRenderer line, float radius)
        {
            if (line == null)
                return;

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

        /// <summary>
        /// 현재 링 영역에 닿은 IDamageable 대상에게 피해와 넉백을 준다.
        /// </summary>
        private void DamageTargetsOnRing(
            Vector3 center,
            float currentRadius,
            HashSet<MonoBehaviour> hitTargets)
        {
            float checkRadius = currentRadius + waveThickness;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, checkRadius, targetLayer);

            foreach (Collider2D hit in hits)
            {
                if (hit == null)
                    continue;

                MonoBehaviour damageableBehaviour = FindDamageableBehaviour(hit);

                if (damageableBehaviour == null)
                    continue;

                if (hitTargets.Contains(damageableBehaviour))
                    continue;

                float distance = Vector2.Distance(
                    center,
                    damageableBehaviour.transform.position
                );

                if (distance < currentRadius - waveThickness ||
                    distance > currentRadius + waveThickness)
                {
                    continue;
                }

                IDamageable damageable = damageableBehaviour as IDamageable;

                if (damageable == null)
                    continue;

                Vector2 knockbackDirection =
                    ((Vector2)damageableBehaviour.transform.position - (Vector2)center).normalized;

                if (knockbackDirection.sqrMagnitude <= 0.001f)
                {
                    knockbackDirection = Vector2.up;
                }

                // 기존 위산 연동파 피해 계산을 그대로 사용한다.
                // 실제 TakeDamage에 넘기는 이 값을 최종 계산 피해량으로 기록한다.
                float finalDamage =
                    sourceAbility.GetGastricPeristalsisWaveDamage();

                float knockbackPower =
                    sourceAbility.GetGastricPeristalsisWaveKnockback();

                damageable.TakeDamage(
                    finalDamage,
                    knockbackDirection * knockbackPower,
                    false
                );

                // 위산 연동파는 Projectile.OnHitDamageable을 거치지 않는 독립 피해이므로
                // 기존 전체 피해량 시스템에도 여기서 정확히 1회 전달한다.
                if (finalDamage > 0f &&
                    ownerCharacter != null &&
                    ownerCharacter.OnDealDamage != null)
                {
                    ownerCharacter.OnDealDamage.Invoke(
                        finalDamage
                    );
                }

                // 결과 화면의 "가장 피해를 많이 준 증강" 계산에 기록한다.
                if (finalDamage > 0f &&
                    AugmentDamageTracker.Instance != null)
                {
                    AugmentDamageTracker.Instance.RecordDamage(
                        "위산 연동파",
                        finalDamage
                    );
                }

                hitTargets.Add(damageableBehaviour);

                if (debugLog)
                {
                    Debug.Log(
                        $"[위산 연동파] {damageableBehaviour.name} 피해 {finalDamage}, 넉백 {knockbackPower}"
                    );
                }
            }
        }

        /// <summary>
        /// Collider가 자식 오브젝트에 붙어 있어도 부모에서 IDamageable을 찾는다.
        /// </summary>
        private MonoBehaviour FindDamageableBehaviour(Collider2D collider)
        {
            MonoBehaviour[] behaviours =
                collider.GetComponentsInParent<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IDamageable)
                {
                    return behaviour;
                }
            }

            return null;
        }
    }
}
