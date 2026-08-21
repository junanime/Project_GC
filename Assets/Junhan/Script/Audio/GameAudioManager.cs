using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    /// <summary>
    /// 게임 전체에서 사용하는 전역 사운드 관리자입니다.
    ///
    /// BGM 규칙:
    /// - 기본 BGM은 Main Menu / Ingame 중 하나만 재생합니다.
    /// - MiniStage / Boss BGM이 시작되면 기본 BGM을 Pause합니다.
    /// - MiniStage 종료 시 Pause됐던 Ingame BGM을 기존 위치부터 재개합니다.
    /// - Boss 종료 또는 게임 종료 시에는 모든 BGM을 정지합니다.
    ///
    /// SFX:
    /// - PlayOneShot 방식으로 여러 효과음을 동시에 재생할 수 있습니다.
    /// - Needle / Monster Hit처럼 호출 빈도가 높은 효과음은
    ///   최소 재생 간격을 적용해 사운드가 과도하게 겹치지 않도록 합니다.
    /// </summary>
    public sealed class GameAudioManager : MonoBehaviour
    {
        public enum GameSfxId
        {
            BossAppear = 0,
            DangerWave = 1,
            EliteSpawn = 2,
            FieldEventStart = 3,
            MiniStageEnter = 4,
            MonsterHit = 5,
            NeedleAttack = 6,
            PlayerDeath = 7,
            PlayerHit = 8,
            RewardEvent = 9
        }

        private enum OverrideBgmType
        {
            None = 0,
            MiniStage = 1,
            Boss = 2
        }

        public static GameAudioManager Instance { get; private set; }

        [Header("Audio Sources")]

        [Tooltip(
            "Lobby / Ingame 기본 BGM을 재생하는 AudioSource입니다. " +
            "비워 두면 런타임에 자동 생성합니다.")]
        [SerializeField]
        private AudioSource baseBgmSource;

        [Tooltip(
            "MiniStage / Boss처럼 기본 BGM 위에 임시로 재생할 " +
            "BGM 전용 AudioSource입니다. 비워 두면 자동 생성합니다.")]
        [SerializeField]
        private AudioSource overrideBgmSource;

        [Tooltip(
            "효과음을 PlayOneShot으로 재생하는 AudioSource입니다. " +
            "비워 두면 런타임에 자동 생성합니다.")]
        [SerializeField]
        private AudioSource sfxSource;

        [Header("BGM Clips")]

        [Tooltip("메인 메뉴에서 반복 재생할 lobby_bgm_10입니다.")]
        [SerializeField]
        private AudioClip lobbyBgm;

        [Tooltip("일반 게임 플레이 중 반복 재생할 ingame_bgm_10입니다.")]
        [SerializeField]
        private AudioClip ingameBgm;

        [Tooltip("미니 스테이지 내부에서 반복 재생할 mini_stage_bgm_10입니다.")]
        [SerializeField]
        private AudioClip miniStageBgm;

        [Tooltip("최종 보스 전투 중 반복 재생할 boss_bgm_10입니다.")]
        [SerializeField]
        private AudioClip bossBgm;

        [Header("SFX Clips")]

        [Tooltip("보스 등장 효과음 boss_appear_10입니다.")]
        [SerializeField]
        private AudioClip bossAppearSfx;

        [Tooltip("위험 파도 경고 효과음 danger_wave_10입니다.")]
        [SerializeField]
        private AudioClip dangerWaveSfx;

        [Tooltip("엘리트 몬스터 등장 효과음 elite_spawn_10입니다.")]
        [SerializeField]
        private AudioClip eliteSpawnSfx;

        [Tooltip("필드 이벤트 시작 효과음 field_event_start_10입니다.")]
        [SerializeField]
        private AudioClip fieldEventStartSfx;

        [Tooltip("미니 스테이지 진입 효과음 mini_stage_enter_10입니다.")]
        [SerializeField]
        private AudioClip miniStageEnterSfx;

        [Tooltip("몬스터 피격 효과음 monster_hit_10입니다.")]
        [SerializeField]
        private AudioClip monsterHitSfx;

        [Tooltip("주사기 기본 발사 효과음 needle_attack_10입니다.")]
        [SerializeField]
        private AudioClip needleAttackSfx;

        [Tooltip("플레이어 사망 효과음 player_death_10입니다.")]
        [SerializeField]
        private AudioClip playerDeathSfx;

        [Tooltip("플레이어 피격 효과음 player_hit_10입니다.")]
        [SerializeField]
        private AudioClip playerHitSfx;

        [Tooltip("보상 획득 효과음 reward_event_10입니다.")]
        [SerializeField]
        private AudioClip rewardEventSfx;

        [Header("Volume")]

        [Tooltip("전체 사운드 볼륨입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float masterVolume = 1f;

        [Tooltip("BGM 볼륨입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float bgmVolume = 0.55f;

        [Tooltip("효과음 볼륨입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float sfxVolume = 0.8f;

        [Header("High Frequency SFX Protection")]

        [Tooltip(
            "주사기 발사 효과음이 너무 많이 겹치지 않도록 하는 최소 간격입니다. " +
            "다발/샷건/양극침에서도 효과음 폭주를 방지합니다.")]
        [SerializeField, Min(0f)]
        private float needleAttackMinInterval = 0.06f;

        [Tooltip(
            "몬스터 여러 마리가 동시에 맞을 때 monster_hit 효과음이 " +
            "지나치게 겹치지 않도록 하는 최소 간격입니다.")]
        [SerializeField, Min(0f)]
        private float monsterHitMinInterval = 0.04f;

        [Tooltip(
            "엘리트 몬스터 여러 마리가 동시에 생성될 때 " +
            "등장 효과음이 지나치게 겹치지 않도록 하는 최소 간격입니다.")]
        [SerializeField, Min(0f)]
        private float eliteSpawnMinInterval = 0.1f;

        [Tooltip(
            "같은 시점에 여러 필드 이벤트가 시작될 경우 " +
            "시작 효과음이 완전히 겹치는 것을 막는 최소 간격입니다.")]
        [SerializeField, Min(0f)]
        private float fieldEventStartMinInterval = 0.1f;

        [Header("Scene BGM Routing")]

        [Tooltip(
            "Scene이 로드될 때 Lobby / Gameplay BGM을 자동으로 선택합니다.")]
        [SerializeField]
        private bool autoRouteSceneBgm = true;

        [Tooltip(
            "메인 메뉴 Scene의 Build Index입니다. " +
            "현재 LevelManager.ReturnToMainMenu()가 Scene 0을 사용하므로 기본값은 0입니다.")]
        [SerializeField]
        private int lobbySceneBuildIndex = 0;

        [Tooltip(
            "일반 게임 플레이 Scene 이름입니다. " +
            "현재 프로젝트의 게임 Scene에 맞춰 입력하세요.")]
        [SerializeField]
        private string gameplaySceneName = "Level 1";

        [Header("Persistence")]

        [Tooltip(
            "Scene 전환 시 AudioManager를 유지합니다. " +
            "메인 메뉴 → 게임 → 메인 메뉴 사이 BGM 관리에 사용합니다.")]
        [SerializeField]
        private bool dontDestroyOnLoad = true;

        [Header("Debug")]

        [Tooltip("BGM 전환과 주요 사운드 상태를 Console에 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private OverrideBgmType currentOverrideBgm =
            OverrideBgmType.None;

        private bool baseBgmPausedByOverride;

        private float lastNeedleAttackTime =
            float.NegativeInfinity;

        private float lastMonsterHitTime =
            float.NegativeInfinity;

        private float lastEliteSpawnTime =
            float.NegativeInfinity;

        private float lastFieldEventStartTime =
            float.NegativeInfinity;

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            ResolveAudioSources();
            ApplySourceSettings();

            SceneManager.sceneLoaded +=
                HandleSceneLoaded;
        }

        private void Start()
        {
            if (autoRouteSceneBgm)
            {
                RouteSceneBgm(
                    SceneManager.GetActiveScene());
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -=
                HandleSceneLoaded;

            Instance = null;
        }

        private void OnValidate()
        {
            masterVolume =
                Mathf.Clamp01(masterVolume);

            bgmVolume =
                Mathf.Clamp01(bgmVolume);

            sfxVolume =
                Mathf.Clamp01(sfxVolume);

            ApplySourceVolumes();
        }

        private void ResolveAudioSources()
        {
            if (baseBgmSource == null)
            {
                baseBgmSource =
                    gameObject.AddComponent<AudioSource>();
            }

            if (overrideBgmSource == null)
            {
                overrideBgmSource =
                    gameObject.AddComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                sfxSource =
                    gameObject.AddComponent<AudioSource>();
            }
        }

        private void ApplySourceSettings()
        {
            if (baseBgmSource != null)
            {
                baseBgmSource.playOnAwake = false;
                baseBgmSource.loop = true;
                baseBgmSource.spatialBlend = 0f;
            }

            if (overrideBgmSource != null)
            {
                overrideBgmSource.playOnAwake = false;
                overrideBgmSource.loop = true;
                overrideBgmSource.spatialBlend = 0f;
            }

            if (sfxSource != null)
            {
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
            }

            ApplySourceVolumes();
        }

        private void ApplySourceVolumes()
        {
            if (baseBgmSource != null)
            {
                baseBgmSource.volume =
                    masterVolume * bgmVolume;
            }

            if (overrideBgmSource != null)
            {
                overrideBgmSource.volume =
                    masterVolume * bgmVolume;
            }

            if (sfxSource != null)
            {
                sfxSource.volume =
                    masterVolume * sfxVolume;
            }
        }

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (!autoRouteSceneBgm)
            {
                return;
            }

            RouteSceneBgm(scene);
        }

        private void RouteSceneBgm(
            Scene scene)
        {
            if (scene.buildIndex ==
                lobbySceneBuildIndex)
            {
                PlayLobbyBgm();
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    gameplaySceneName) &&
                scene.name == gameplaySceneName)
            {
                PlayIngameBgm();
            }
        }

        // =========================================================
        // Public Static API
        // =========================================================

        public static void PlayLobbyMusic()
        {
            if (Instance != null)
            {
                Instance.PlayLobbyBgm();
            }
        }

        public static void PlayIngameMusic()
        {
            if (Instance != null)
            {
                Instance.PlayIngameBgm();
            }
        }

        public static void EnterMiniStageAudio()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.PlaySfxInternal(
                GameSfxId.MiniStageEnter);

            Instance.BeginOverrideBgm(
                Instance.miniStageBgm,
                OverrideBgmType.MiniStage);
        }

        public static void ExitMiniStageAudio()
        {
            if (Instance == null)
            {
                return;
            }

            if (Instance.currentOverrideBgm !=
                OverrideBgmType.MiniStage)
            {
                return;
            }

            Instance.EndOverrideBgm(
                true);
        }

        public static void PlayBossAppearOnly()
        {
            if (Instance != null)
            {
                Instance.PlaySfxInternal(
                    GameSfxId.BossAppear);
            }
        }

        public static void StartBossAudio()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.PlaySfxInternal(
                GameSfxId.BossAppear);

            Instance.BeginOverrideBgm(
                Instance.bossBgm,
                OverrideBgmType.Boss);
        }

        public static void EndRunAudio(
            bool victory)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.StopAllBgmInternal();

            if (victory)
            {
                Instance.PlaySfxInternal(
                    GameSfxId.RewardEvent);
            }
        }

        public static void PlaySfx(
            GameSfxId id)
        {
            if (Instance != null)
            {
                Instance.PlaySfxInternal(id);
            }
        }

        // =========================================================
        // Base BGM
        // =========================================================

        private void PlayLobbyBgm()
        {
            PlayBaseBgm(
                lobbyBgm,
                "Lobby");
        }

        private void PlayIngameBgm()
        {
            PlayBaseBgm(
                ingameBgm,
                "Ingame");
        }

        private void PlayBaseBgm(
            AudioClip clip,
            string label)
        {
            if (clip == null)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        $"[GameAudio] {label} BGM Clip이 비어 있습니다.",
                        this);
                }

                return;
            }

            // Scene 변경 시 이전 MiniStage / Boss 상태를 모두 제거합니다.
            StopOverrideBgmWithoutResume();

            if (baseBgmSource == null)
            {
                return;
            }

            if (baseBgmSource.clip == clip &&
                baseBgmSource.isPlaying)
            {
                return;
            }

            baseBgmSource.Stop();
            baseBgmSource.clip = clip;
            baseBgmSource.loop = true;
            baseBgmSource.time = 0f;
            baseBgmSource.Play();

            if (debugLog)
            {
                Debug.Log(
                    $"[GameAudio] Base BGM START | {label} | Clip={clip.name}",
                    this);
            }
        }

        // =========================================================
        // Override BGM
        // =========================================================

        private void BeginOverrideBgm(
            AudioClip clip,
            OverrideBgmType type)
        {
            if (clip == null)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        $"[GameAudio] Override BGM Clip이 비어 있습니다. Type={type}",
                        this);
                }

                return;
            }

            if (overrideBgmSource == null)
            {
                return;
            }

            // 이미 다른 Override가 재생 중이면
            // Base를 잠깐 재개하지 않고 바로 교체합니다.
            if (currentOverrideBgm !=
                OverrideBgmType.None)
            {
                overrideBgmSource.Stop();
            }
            else
            {
                baseBgmPausedByOverride =
                    baseBgmSource != null &&
                    baseBgmSource.isPlaying;

                if (baseBgmPausedByOverride)
                {
                    baseBgmSource.Pause();
                }
            }

            currentOverrideBgm = type;

            overrideBgmSource.Stop();
            overrideBgmSource.clip = clip;
            overrideBgmSource.loop = true;
            overrideBgmSource.time = 0f;
            overrideBgmSource.Play();

            if (debugLog)
            {
                Debug.Log(
                    $"[GameAudio] Override BGM START | " +
                    $"Type={type} | Clip={clip.name}",
                    this);
            }
        }

        private void EndOverrideBgm(
            bool resumeBase)
        {
            if (overrideBgmSource != null)
            {
                overrideBgmSource.Stop();
                overrideBgmSource.clip = null;
            }

            OverrideBgmType previousType =
                currentOverrideBgm;

            currentOverrideBgm =
                OverrideBgmType.None;

            if (resumeBase &&
                baseBgmPausedByOverride &&
                baseBgmSource != null)
            {
                baseBgmSource.UnPause();
            }

            baseBgmPausedByOverride = false;

            if (debugLog)
            {
                Debug.Log(
                    $"[GameAudio] Override BGM END | " +
                    $"Type={previousType} | ResumeBase={resumeBase}",
                    this);
            }
        }

        private void StopOverrideBgmWithoutResume()
        {
            if (overrideBgmSource != null)
            {
                overrideBgmSource.Stop();
                overrideBgmSource.clip = null;
            }

            currentOverrideBgm =
                OverrideBgmType.None;

            baseBgmPausedByOverride = false;
        }

        private void StopAllBgmInternal()
        {
            if (overrideBgmSource != null)
            {
                overrideBgmSource.Stop();
                overrideBgmSource.clip = null;
            }

            if (baseBgmSource != null)
            {
                baseBgmSource.Stop();
            }

            currentOverrideBgm =
                OverrideBgmType.None;

            baseBgmPausedByOverride = false;

            if (debugLog)
            {
                Debug.Log(
                    "[GameAudio] 모든 BGM 정지.",
                    this);
            }
        }

        // =========================================================
        // SFX
        // =========================================================

        private void PlaySfxInternal(
            GameSfxId id)
        {
            if (sfxSource == null)
            {
                return;
            }

            float now =
                Time.unscaledTime;

            switch (id)
            {
                case GameSfxId.NeedleAttack:
                    if (now - lastNeedleAttackTime <
                        needleAttackMinInterval)
                    {
                        return;
                    }

                    lastNeedleAttackTime = now;
                    break;

                case GameSfxId.MonsterHit:
                    if (now - lastMonsterHitTime <
                        monsterHitMinInterval)
                    {
                        return;
                    }

                    lastMonsterHitTime = now;
                    break;

                case GameSfxId.EliteSpawn:
                    if (now - lastEliteSpawnTime <
                        eliteSpawnMinInterval)
                    {
                        return;
                    }

                    lastEliteSpawnTime = now;
                    break;

                case GameSfxId.FieldEventStart:
                    if (now - lastFieldEventStartTime <
                        fieldEventStartMinInterval)
                    {
                        return;
                    }

                    lastFieldEventStartTime = now;
                    break;
            }

            AudioClip clip =
                GetSfxClip(id);

            if (clip == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip);
        }

        private AudioClip GetSfxClip(
            GameSfxId id)
        {
            switch (id)
            {
                case GameSfxId.BossAppear:
                    return bossAppearSfx;

                case GameSfxId.DangerWave:
                    return dangerWaveSfx;

                case GameSfxId.EliteSpawn:
                    return eliteSpawnSfx;

                case GameSfxId.FieldEventStart:
                    return fieldEventStartSfx;

                case GameSfxId.MiniStageEnter:
                    return miniStageEnterSfx;

                case GameSfxId.MonsterHit:
                    return monsterHitSfx;

                case GameSfxId.NeedleAttack:
                    return needleAttackSfx;

                case GameSfxId.PlayerDeath:
                    return playerDeathSfx;

                case GameSfxId.PlayerHit:
                    return playerHitSfx;

                case GameSfxId.RewardEvent:
                    return rewardEventSfx;
            }

            return null;
        }
    }
}