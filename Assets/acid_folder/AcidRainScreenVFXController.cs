using System.Collections;
using UnityEngine;

public class AcidRainScreenVFXController : MonoBehaviour
{
    [Header("파티클 설정")]
    [Tooltip("위산비 효과에 사용할 파티클 시스템들입니다. 비워두면 자식 오브젝트에서 자동으로 찾습니다.")]
    [SerializeField] private ParticleSystem[] particleSystems;

    [Tooltip("위산비가 시작될 때 파티클 방출량이 서서히 증가하는 시간입니다.")]
    [SerializeField] private float fadeInTime = 0.25f;

    [Tooltip("위산비가 종료될 때 파티클 방출량이 서서히 감소하는 시간입니다.")]
    [SerializeField] private float fadeOutTime = 0.8f;

    [Tooltip("위산비 종료 후 남아 있는 파티클이 자연스럽게 사라질 때까지 기다리는 시간입니다.")]
    [SerializeField] private float waitBeforeClearTime = 1.2f;

    [Header("렌더링 순서")]
    [Tooltip("체크하면 자식 파티클 렌더러들의 Sorting Layer와 Order in Layer를 코드에서 강제로 적용합니다.")]
    [SerializeField] private bool forceRendererSorting = true;

    [Tooltip("위산비 파티클에 적용할 Sorting Layer 이름입니다.")]
    [SerializeField] private string rainSortingLayerName = "Default";

    [Tooltip("위산비 파티클의 Order in Layer입니다. 캐릭터와 몬스터 위에 보이게 하려면 높은 값을 사용하세요.")]
    [SerializeField] private int rainOrderInLayer = 100;

    [Header("시작 옵션")]
    [Tooltip("게임 시작과 동시에 위산비를 재생할지 여부입니다. 위산분비 이벤트 때만 켤 예정이면 꺼두세요.")]
    [SerializeField] private bool playOnStart = false;

    [Tooltip("게임 시작 시 파티클을 완전히 정지하고 비워둘지 여부입니다.")]
    [SerializeField] private bool clearOnStart = true;

    [Header("테스트 옵션")]
    [Tooltip("체크하면 플레이 모드에서 테스트 키로 위산비를 켜고 끌 수 있습니다. 이벤트 연결 후에는 꺼도 됩니다.")]
    [SerializeField] private bool useKeyboardTest = true;

    [Tooltip("위산비 테스트에 사용할 키입니다.")]
    [SerializeField] private KeyCode testToggleKey = KeyCode.V;

    private float[] originalEmissionMultipliers;
    private Coroutine fadeCoroutine;
    private bool isPlaying;
    private bool initialized;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        InitializeIfNeeded();
        ApplyRendererSorting();
    }

    private void Start()
    {
        if (clearOnStart)
        {
            StopImmediately();
        }

        if (playOnStart)
        {
            PlayRain();
        }
    }

    private void Update()
    {
        if (!useKeyboardTest)
        {
            return;
        }

        if (Input.GetKeyDown(testToggleKey))
        {
            if (isPlaying)
            {
                StopRain();
            }
            else
            {
                PlayRain();
            }
        }
    }

    private void OnValidate()
    {
        ApplyRendererSorting();
    }

    private void OnDisable()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    [ContextMenu("Refresh Particle List")]
    public void RefreshParticleList()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        initialized = false;
        InitializeIfNeeded();
        ApplyRendererSorting();
    }

    [ContextMenu("Play Acid Rain")]
    public void PlayRain()
    {
        InitializeIfNeeded();
        ApplyRendererSorting();

        if (particleSystems == null || particleSystems.Length == 0)
        {
            Debug.LogWarning("[AcidRainScreenVFX] 재생할 ParticleSystem이 없습니다.");
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(PlayRainRoutine());
    }

    [ContextMenu("Stop Acid Rain")]
    public void StopRain()
    {
        InitializeIfNeeded();

        if (particleSystems == null || particleSystems.Length == 0)
        {
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(StopRainRoutine());
    }

    [ContextMenu("Stop Immediately")]
    public void StopImmediately()
    {
        InitializeIfNeeded();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (particleSystems == null)
        {
            isPlaying = false;
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                continue;
            }

            SetEmissionMultiplierScale(ps, i, 0f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        isPlaying = false;
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        if (particleSystems == null)
        {
            particleSystems = new ParticleSystem[0];
        }

        originalEmissionMultipliers = new float[particleSystems.Length];

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                originalEmissionMultipliers[i] = 1f;
                continue;
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            originalEmissionMultipliers[i] = emission.rateOverTimeMultiplier;

            if (Mathf.Approximately(originalEmissionMultipliers[i], 0f))
            {
                originalEmissionMultipliers[i] = 1f;
            }
        }

        initialized = true;
    }

    private IEnumerator PlayRainRoutine()
    {
        isPlaying = true;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                continue;
            }

            SetEmissionMultiplierScale(ps, i, 0f);
            ps.Play(true);
        }

        float timer = 0f;

        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            float t = fadeInTime <= 0f ? 1f : Mathf.Clamp01(timer / fadeInTime);

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];

                if (ps == null)
                {
                    continue;
                }

                SetEmissionMultiplierScale(ps, i, t);
            }

            yield return null;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                continue;
            }

            SetEmissionMultiplierScale(ps, i, 1f);
        }

        fadeCoroutine = null;
    }

    private IEnumerator StopRainRoutine()
    {
        isPlaying = false;

        float timer = 0f;
        float[] startScales = new float[particleSystems.Length];

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                startScales[i] = 0f;
                continue;
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            float originalMultiplier = GetOriginalEmissionMultiplier(i);

            if (originalMultiplier <= 0f)
            {
                startScales[i] = 1f;
            }
            else
            {
                startScales[i] = emission.rateOverTimeMultiplier / originalMultiplier;
            }
        }

        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float t = fadeOutTime <= 0f ? 1f : Mathf.Clamp01(timer / fadeOutTime);

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];

                if (ps == null)
                {
                    continue;
                }

                float scale = Mathf.Lerp(startScales[i], 0f, t);
                SetEmissionMultiplierScale(ps, i, scale);
            }

            yield return null;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                continue;
            }

            SetEmissionMultiplierScale(ps, i, 0f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        yield return new WaitForSeconds(waitBeforeClearTime);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
            {
                continue;
            }

            ps.Clear(true);
        }

        fadeCoroutine = null;
    }

    private void SetEmissionMultiplierScale(ParticleSystem ps, int index, float scale)
    {
        if (ps == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTimeMultiplier = GetOriginalEmissionMultiplier(index) * Mathf.Clamp01(scale);
    }

    private float GetOriginalEmissionMultiplier(int index)
    {
        if (originalEmissionMultipliers == null)
        {
            return 1f;
        }

        if (index < 0 || index >= originalEmissionMultipliers.Length)
        {
            return 1f;
        }

        return originalEmissionMultipliers[index];
    }

    private void ApplyRendererSorting()
    {
        if (!forceRendererSorting)
        {
            return;
        }

        ParticleSystemRenderer[] renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);

        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].sortingLayerName = rainSortingLayerName;
            renderers[i].sortingOrder = rainOrderInLayer;
        }
    }
}