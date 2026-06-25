using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 소화 파동 반사방의 목표 코어.
    ///
    /// Room 스크립트가 빔 경로를 계산한 뒤,
    /// 이 코어에 빔이 닿고 있는지 SetBeamReceiving()으로 알려줍니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MiniStageWaveReceiver : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("목표 코어의 기본 SpriteRenderer입니다.")]
        [SerializeField] private SpriteRenderer receiverRenderer;

        [Tooltip("빔이 닿고 있을 때 켤 이펙트 오브젝트입니다.")]
        [SerializeField] private GameObject receivingEffect;

        [Tooltip("클리어 완료 시 켤 이펙트 오브젝트입니다.")]
        [SerializeField] private GameObject solvedEffect;

        [Header("Visual Color")]
        [Tooltip("평소 색상입니다.")]
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("빔이 닿고 있을 때 색상입니다.")]
        [SerializeField] private Color receivingColor = new Color(1f, 0.9f, 0.25f, 1f);

        [Tooltip("클리어 완료 색상입니다.")]
        [SerializeField] private Color solvedColor = new Color(0.3f, 1f, 0.45f, 1f);

        [Header("Debug")]
        [Tooltip("목표 코어 상태 변경 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private bool isReceivingBeam;
        private bool isSolved;

        public bool IsReceivingBeam => isReceivingBeam;
        public bool IsSolved => isSolved;

        private void Awake()
        {
            ResolveReferences();
            ResetReceiver();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void ResetReceiver()
        {
            isReceivingBeam = false;
            isSolved = false;

            if (receiverRenderer != null)
            {
                receiverRenderer.color = idleColor;
            }

            if (receivingEffect != null)
            {
                receivingEffect.SetActive(false);
            }

            if (solvedEffect != null)
            {
                solvedEffect.SetActive(false);
            }
        }

        public void SetBeamReceiving(bool value)
        {
            if (isSolved)
            {
                return;
            }

            if (isReceivingBeam == value)
            {
                return;
            }

            isReceivingBeam = value;

            if (receiverRenderer != null)
            {
                receiverRenderer.color = isReceivingBeam ? receivingColor : idleColor;
            }

            if (receivingEffect != null)
            {
                receivingEffect.SetActive(isReceivingBeam);
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageWaveReceiver] 빔 수신 상태 변경: {isReceivingBeam}");
            }
        }

        public void SetSolved(bool value)
        {
            isSolved = value;
            isReceivingBeam = value;

            if (receiverRenderer != null)
            {
                receiverRenderer.color = value ? solvedColor : idleColor;
            }

            if (receivingEffect != null)
            {
                receivingEffect.SetActive(false);
            }

            if (solvedEffect != null)
            {
                solvedEffect.SetActive(value);
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageWaveReceiver] 클리어 상태 변경: {isSolved}");
            }
        }

        private void ResolveReferences()
        {
            if (receiverRenderer == null)
            {
                receiverRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }
    }
}