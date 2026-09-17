using UnityEngine;

public class BossVisualFloatMotion : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("비워두면 이 컴포넌트가 붙은 Transform 자체를 흔듭니다.")]
    [SerializeField] private Transform target;

    [Header("Vertical Float")]
    [Tooltip("기본 위치 기준 위아래로 움직이는 거리입니다.")]
    [SerializeField] private float verticalAmplitude = 0.08f;

    [Tooltip("상하 흔들림 속도입니다. 1이면 1초에 1주기 수준입니다.")]
    [SerializeField] private float verticalFrequency = 1.2f;

    [Header("Tilt")]
    [Tooltip("둥둥 떠있는 느낌을 위한 Z축 기울기 최대 각도입니다.")]
    [SerializeField] private float rotationAmplitude = 2f;

    [Tooltip("기울기 흔들림 속도입니다.")]
    [SerializeField] private float rotationFrequency = 0.8f;

    [Header("Options")]
    [Tooltip("여러 개체가 완전히 같은 박자로 움직이지 않도록 시작 위상을 랜덤화합니다.")]
    [SerializeField] private bool randomizePhase = true;

    [Tooltip("일시정지 영향을 받지 않는 시간으로 재생할지 여부입니다.")]
    [SerializeField] private bool useUnscaledTime = false;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private float verticalPhase;
    private float rotationPhase;

    private void Awake()
    {
        if (target == null)
            target = transform;

        CacheInitialState();

        if (randomizePhase)
        {
            verticalPhase = Random.Range(0f, Mathf.PI * 2f);
            rotationPhase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void OnEnable()
    {
        if (target == null)
            target = transform;

        CacheInitialState();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float t = useUnscaledTime ? Time.unscaledTime : Time.time;

        float verticalOffset =
            Mathf.Sin((t * verticalFrequency * Mathf.PI * 2f) + verticalPhase) * verticalAmplitude;

        float zAngle =
            Mathf.Sin((t * rotationFrequency * Mathf.PI * 2f) + rotationPhase) * rotationAmplitude;

        target.localPosition = initialLocalPosition + new Vector3(0f, verticalOffset, 0f);
        target.localRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, zAngle);
    }

    private void OnDisable()
    {
        if (target == null)
            return;

        target.localPosition = initialLocalPosition;
        target.localRotation = initialLocalRotation;
    }

    private void CacheInitialState()
    {
        initialLocalPosition = target.localPosition;
        initialLocalRotation = target.localRotation;
    }
}