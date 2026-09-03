using System;
using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class MiniStageFallingFoodStrike : MonoBehaviour
    {
        [Header("Visual References")]
        [Tooltip("낙석이 떨어지기 전 표시되는 경고 원 SpriteRenderer입니다.")]
        [SerializeField] private SpriteRenderer warningCircleRenderer;

        [Tooltip("위에서 아래로 떨어지는 음식물 SpriteRenderer입니다. Sprite와 크기는 Room에서 랜덤으로 지정합니다.")]
        [SerializeField] private SpriteRenderer fallingFoodRenderer;

        [Tooltip("낙하 충돌 순간에 잠깐 보여줄 이펙트 오브젝트입니다. 없어도 됩니다.")]
        [SerializeField] private GameObject impactEffectObject;

        [Header("Warning Visual")]
        [Tooltip("경고 원의 기본 색상입니다.")]
        [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.05f, 0.35f);

        [Tooltip("경고 시간이 끝나기 직전 깜빡일 때 사용할 색상입니다.")]
        [SerializeField] private Color warningBlinkColor = new Color(1f, 1f, 1f, 0.55f);

        [Tooltip("경고 원이 깜빡이는 속도입니다.")]
        [SerializeField] private float warningBlinkSpeed = 12f;

        [Tooltip("경고 원 Sprite가 지름 1짜리 원이라고 가정하고 피격 반지름에 맞춰 Scale을 조절합니다.")]
        [SerializeField] private bool autoScaleWarningCircle = true;

        [Header("Fall Motion")]
        [Tooltip("음식물이 위에서 아래로 떨어지는 이동 연출을 사용할지 여부입니다.")]
        [SerializeField] private bool useFallMotion = true;

        [Tooltip("음식물이 떨어지기 시작하는 로컬 위치입니다. Y값이 클수록 더 위에서 떨어지는 것처럼 보입니다.")]
        [SerializeField] private Vector2 fallStartLocalOffset = new Vector2(0f, 8f);

        [Tooltip("음식물이 최종적으로 도착할 로컬 위치입니다. 보통 0,0으로 둡니다.")]
        [SerializeField] private Vector2 fallEndLocalOffset = Vector2.zero;

        [Tooltip("음식물이 위에서 아래로 떨어지는 데 걸리는 시간입니다.")]
        [SerializeField] private float fallMotionDuration = 0.35f;

        [Tooltip("낙하 이동 보간 곡선입니다. 뒤쪽이 가파르면 점점 빨라지는 낙하 느낌이 납니다.")]
        [SerializeField] private AnimationCurve fallMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("음식물이 떨어지기 시작할 때 경고 원을 바로 숨길지 여부입니다. false면 착지 순간까지 경고 원이 유지됩니다.")]
        [SerializeField] private bool hideWarningWhenFallStarts = false;

        [Tooltip("음식물이 떨어지는 동안 크기를 살짝 키운 상태에서 시작할지 여부입니다.")]
        [SerializeField] private bool useFallScaleMotion = true;

        [Tooltip("낙하 시작 시 음식물 크기 배율입니다.")]
        [SerializeField] private float fallStartScaleMultiplier = 1.15f;

        [Tooltip("낙하 도착 시 음식물 크기 배율입니다.")]
        [SerializeField] private float fallEndScaleMultiplier = 1f;

        [Header("Impact Visual")]
        [Tooltip("낙하 후 음식물 이미지가 남아 있는 시간입니다.")]
        [SerializeField] private float fallingFoodVisibleTime = 0.18f;

        [Tooltip("피격 이펙트가 보이는 시간입니다.")]
        [SerializeField] private float impactEffectVisibleTime = 0.25f;

        [Tooltip("음식물이 떨어질 때 무작위 회전값을 적용할지 여부입니다.")]
        [SerializeField] private bool applyFoodRotation = true;

        [Header("Debug")]
        [Tooltip("낙석 개별 오브젝트 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Character targetPlayer;
        private Sprite selectedFoodSprite;
        private Vector2 selectedFoodScale;
        private float selectedFoodRotationZ;

        private float damageRadius;
        private float warningDuration;
        private float damage;
        private float stunDuration;
        private Vector2 knockback;
        private Action<MiniStageFallingFoodStrike> onFinished;

        private bool initialized;
        private bool finished;

        public void Setup(
            Character player,
            Sprite foodSprite,
            Vector2 foodScale,
            float foodRotationZ,
            float radius,
            float warningTime,
            float damageAmount,
            float stunTime,
            Vector2 knockbackForce,
            Action<MiniStageFallingFoodStrike> finishedCallback
        )
        {
            targetPlayer = player;
            selectedFoodSprite = foodSprite;
            selectedFoodScale = foodScale;
            selectedFoodRotationZ = foodRotationZ;

            damageRadius = Mathf.Max(0.1f, radius);
            warningDuration = Mathf.Max(0f, warningTime);
            damage = Mathf.Max(0f, damageAmount);
            stunDuration = Mathf.Max(0f, stunTime);
            knockback = knockbackForce;
            onFinished = finishedCallback;

            initialized = true;
            finished = false;

            ApplyInitialVisualState();

            StartCoroutine(StrikeRoutine());
        }

        private void ApplyInitialVisualState()
        {
            if (warningCircleRenderer != null)
            {
                warningCircleRenderer.enabled = true;
                warningCircleRenderer.color = warningColor;

                if (autoScaleWarningCircle)
                {
                    float diameter = damageRadius * 2f;
                    warningCircleRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
                }
            }

            if (fallingFoodRenderer != null)
            {
                fallingFoodRenderer.enabled = false;

                if (selectedFoodSprite != null)
                {
                    fallingFoodRenderer.sprite = selectedFoodSprite;
                }

                fallingFoodRenderer.transform.localPosition = fallStartLocalOffset;

                fallingFoodRenderer.transform.localScale = new Vector3(
                    Mathf.Max(0.01f, selectedFoodScale.x),
                    Mathf.Max(0.01f, selectedFoodScale.y),
                    1f
                );

                if (applyFoodRotation)
                {
                    fallingFoodRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, selectedFoodRotationZ);
                }
            }

            if (impactEffectObject != null)
            {
                impactEffectObject.SetActive(false);
            }
        }

        private IEnumerator StrikeRoutine()
        {
            if (!initialized)
            {
                yield break;
            }

            float timer = 0f;

            while (timer < warningDuration)
            {
                UpdateWarningVisual(timer);
                timer += Time.deltaTime;
                yield return null;
            }

            if (hideWarningWhenFallStarts && warningCircleRenderer != null)
            {
                warningCircleRenderer.enabled = false;
            }

            yield return StartCoroutine(FallMotionRoutine());

            ResolveImpact();

            if (warningCircleRenderer != null)
            {
                warningCircleRenderer.enabled = false;
            }

            if (impactEffectObject != null)
            {
                impactEffectObject.SetActive(true);
            }

            float remainTime = Mathf.Max(fallingFoodVisibleTime, impactEffectVisibleTime);

            yield return new WaitForSeconds(remainTime);

            FinishStrike();
        }

        private void UpdateWarningVisual(float timer)
        {
            if (warningCircleRenderer == null)
            {
                return;
            }

            float blink = Mathf.PingPong(timer * warningBlinkSpeed, 1f);
            warningCircleRenderer.color = Color.Lerp(warningColor, warningBlinkColor, blink);
        }

        private IEnumerator FallMotionRoutine()
        {
            if (fallingFoodRenderer == null)
            {
                yield break;
            }

            fallingFoodRenderer.enabled = true;

            Vector3 startPosition = new Vector3(fallStartLocalOffset.x, fallStartLocalOffset.y, 0f);
            Vector3 endPosition = new Vector3(fallEndLocalOffset.x, fallEndLocalOffset.y, 0f);

            Vector3 baseScale = new Vector3(
                Mathf.Max(0.01f, selectedFoodScale.x),
                Mathf.Max(0.01f, selectedFoodScale.y),
                1f
            );

            if (!useFallMotion || fallMotionDuration <= 0f)
            {
                fallingFoodRenderer.transform.localPosition = endPosition;
                fallingFoodRenderer.transform.localScale = baseScale;
                yield break;
            }

            float timer = 0f;

            while (timer < fallMotionDuration)
            {
                float normalizedTime = Mathf.Clamp01(timer / fallMotionDuration);
                float curveValue = fallMotionCurve != null
                    ? fallMotionCurve.Evaluate(normalizedTime)
                    : normalizedTime;

                fallingFoodRenderer.transform.localPosition = Vector3.Lerp(
                    startPosition,
                    endPosition,
                    curveValue
                );

                if (useFallScaleMotion)
                {
                    float scaleMultiplier = Mathf.Lerp(
                        fallStartScaleMultiplier,
                        fallEndScaleMultiplier,
                        curveValue
                    );

                    fallingFoodRenderer.transform.localScale = baseScale * scaleMultiplier;
                }
                else
                {
                    fallingFoodRenderer.transform.localScale = baseScale;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            fallingFoodRenderer.transform.localPosition = endPosition;
            fallingFoodRenderer.transform.localScale = baseScale * fallEndScaleMultiplier;
        }

        private void ResolveImpact()
        {
            if (targetPlayer == null)
            {
                return;
            }

            float distance = Vector2.Distance(targetPlayer.transform.position, transform.position);

            if (distance > damageRadius)
            {
                if (debugLog)
                {
                    Debug.Log($"[MiniStageFallingFoodStrike] 회피 성공. distance={distance:0.00}, radius={damageRadius:0.00}");
                }

                return;
            }

            targetPlayer.TakeDamage(damage, knockback, false);

            PlayerMiniStageStunRuntime stunRuntime = PlayerMiniStageStunRuntime.GetOrCreate(targetPlayer);

            if (stunRuntime != null)
            {
                stunRuntime.ApplyStun(stunDuration);
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageFallingFoodStrike] 플레이어 피격. damage={damage}, stun={stunDuration}, radius={damageRadius}");
            }
        }

        private void FinishStrike()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            onFinished?.Invoke(this);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (!finished)
            {
                finished = true;
                onFinished?.Invoke(this);
            }
        }
    }
}