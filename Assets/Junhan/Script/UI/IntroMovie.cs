using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Vampire
{
    // Owns only the opening presentation: no scene transition, timeScale or save-game mutation.
    public sealed class IntroMovie : MonoBehaviour
    {
        public static bool Active => current != null;
        public static bool BlocksMenuInput => Active || Time.frameCount <= releaseFrame;
        public bool ConfirmationOpen => confirmation != null && confirmation.activeSelf;
        public VideoPlayer Player => player;
        static IntroMovie current;
        static bool attempted;
        static int releaseFrame = -1;
        internal RectTransform overlay, safeArea, dialog;
        internal GameObject confirmation, startGate, menu;
        internal TextMeshProUGUI hint;
        internal RawImage picture;
        internal Button continueButton, startButton;
        IntroMovieSettings settings;
        VideoPlayer player;
        AudioSource audioSource;
        RenderTexture target;
        float prepareStarted, lastProgress, hintUntil, armedUntil, popupStarted;
        long lastFrame = -1;
        bool finished, started, focusPaused;
        const float DoublePressWindow = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { current = null; attempted = false; releaseFrame = -1; }

        public static void TryShow(ApothecaryUI ui)
        {
            if (attempted || Active) return;
            attempted = true;
            var config = Resources.Load<IntroMovieSettings>("IntroMovieSettings");
            if (config == null || !config.playOnLaunch || string.IsNullOrWhiteSpace(config.relativePath)) return;
            var host = new GameObject("Opening intro");
            host.transform.SetParent(ui.transform, false);
            current = host.AddComponent<IntroMovie>();
            current.settings = config;
            ui.BuildIntroPresentation(current);
            current.Begin();
        }

        void Begin()
        {
            menu.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            GameAudioManager.RefreshIntroVolume();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.source = VideoSource.Url;
            string path = Application.streamingAssetsPath.TrimEnd('/') + "/" + settings.relativePath.TrimStart('/');
            if (!path.Contains("://") && !File.Exists(path)) { Failed(player, "Movie file is missing."); return; }
            player.url = path.Contains("://") ? path : new Uri(Path.GetFullPath(path)).AbsoluteUri;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = Application.platform == RuntimePlatform.WebGLPlayer ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            if (player.audioOutputMode == VideoAudioOutputMode.AudioSource) player.SetTargetAudioSource(0, audioSource);
            player.prepareCompleted += Prepared;
            player.loopPointReached += Completed;
            player.errorReceived += Failed;
            prepareStarted = Time.realtimeSinceStartup;
            hintUntil = prepareStarted + 5;
            hint.text = "인트로 준비 중…";
            player.Prepare();
        }

        void Prepared(VideoPlayer source)
        {
            if (finished) return;
            // Preserve the source aspect ratio, including portrait / ultrawide viewports.
            int w = Mathf.Max(1, (int)source.width), h = Mathf.Max(1, (int)source.height);
            float scale = Mathf.Min(1, 1920f / Mathf.Max(w, h));
            target = new RenderTexture(Mathf.Max(1, Mathf.RoundToInt(w * scale)), Mathf.Max(1, Mathf.RoundToInt(h * scale)), 0, RenderTextureFormat.ARGB32);
            target.Create();
            var previousTarget = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previousTarget;
            player.targetTexture = target;
            picture.texture = target;
            picture.color = Color.white;
            picture.GetComponent<AspectRatioFitter>().aspectRatio = (float)w / h;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // Browser audio/video playback must begin in an actual pointer gesture.
                startGate.SetActive(true);
                hint.text = "화면을 눌러 인트로를 시작하세요";
                hintUntil = float.PositiveInfinity;
            }
            else if (!ConfirmationOpen && !focusPaused) StartPlayback();
        }

        public void StartPlayback()
        {
            if (finished || player == null || !player.isPrepared || ConfirmationOpen) return;
            startGate.SetActive(false);
            started = true;
            lastProgress = Time.realtimeSinceStartup;
            hintUntil = lastProgress + 5;
            hint.text = GamePlatform.UsesTouchControls ? "화면 두 번 터치 · 건너뛰기" : "Esc 두 번 / 화면 두 번 클릭 · 건너뛰기";
            ApplyVolume();
            player.Play();
        }

        public void RequestSkip()
        {
            if (finished || startGate.activeSelf) return;
            if (ConfirmationOpen) { ContinueWatching(); return; }
            float now = Time.realtimeSinceStartup;
            if (armedUntil > now)
            {
                armedUntil = 0;
                confirmation.SetActive(true);
                popupStarted = now;
                if (player.isPlaying) player.Pause();
                EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
            }
            else
            {
                armedUntil = now + DoublePressWindow;
                hintUntil = armedUntil;
                hint.text = GamePlatform.UsesTouchControls ? "한 번 더 터치하면 건너뛰기 확인창이 열립니다" : "Esc 또는 화면을 한 번 더 누르면 건너뛰기 확인창이 열립니다";
            }
        }

        public void ContinueWatching()
        {
            if (finished || !ConfirmationOpen) return;
            confirmation.SetActive(false);
            armedUntil = 0;
            EventSystem.current?.SetSelectedGameObject(null);
            if (player.isPrepared && !focusPaused) StartPlayback();
        }

        public void ConfirmSkip() { if (ConfirmationOpen) Finish(); }
        void Completed(VideoPlayer source) { Finish(); }
        void Failed(VideoPlayer source, string error)
        {
            Debug.LogWarning("[IntroMovie] Playback unavailable; opening menu. " + error);
            Finish();
        }

        void ApplyVolume()
        {
            audioSource.volume = settings.volume;
            if (player.audioOutputMode == VideoAudioOutputMode.Direct && player.isPrepared && player.audioTrackCount > 0)
            {
                var prefs = GamePreferences.Current;
                bool mute = prefs.muted || !Application.isFocused && prefs.muteOnFocusLoss;
                player.SetDirectAudioMute(0, mute);
                if (player.canSetDirectAudioVolume) player.SetDirectAudioVolume(0, settings.volume * prefs.master);
            }
        }

        void Update()
        {
            if (finished) return;
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            if (GameInput.GetKeyDown(KeyCode.Escape)) RequestSkip();
            float now = Time.realtimeSinceStartup;
            hint.gameObject.SetActive(!ConfirmationOpen && now < hintUntil);
            if (ConfirmationOpen)
            {
                float t = Mathf.Clamp01((now - popupStarted) / .18f);
                dialog.localScale = Vector3.one * (GamePreferences.Current.reducedMotion ? 1 : Mathf.Lerp(.86f, 1, 1 - Mathf.Pow(1 - t, 3)));
            }
            ApplyVolume();
            if (!focusPaused && !player.isPrepared && now - prepareStarted > Mathf.Max(5, settings.timeoutSeconds))
                Failed(player, "Preparation timed out.");
            else if (started && !ConfirmationOpen && !focusPaused)
            {
                if (player.frame != lastFrame) { lastFrame = player.frame; lastProgress = now; }
                if (now - lastProgress > Mathf.Max(5, settings.timeoutSeconds)) Failed(player, "Playback stopped progressing.");
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (finished || player == null) return;
            focusPaused = !focused;
            if (!focused && player.isPlaying) player.Pause();
            if (focused && !ConfirmationOpen && player.isPrepared && !startGate.activeSelf) StartPlayback();
            // Losing focus should not consume the bounded preparation timeout.
            prepareStarted = Time.realtimeSinceStartup;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            Release();
            Destroy(gameObject);
        }

        void Release(bool restoreMenu = true)
        {
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.loopPointReached -= Completed;
                player.errorReceived -= Failed;
                player.Stop();
                player.targetTexture = null;
            }
            if (target != null) { target.Release(); Destroy(target); target = null; }
            if (overlay != null && restoreMenu) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
            if (menu != null && restoreMenu) menu.SetActive(true);
            if (current == this) { current = null; releaseFrame = Time.frameCount + 1; }
            EventSystem.current?.SetSelectedGameObject(null);
            GameAudioManager.RefreshIntroVolume();
        }
        void OnDestroy() { if (!finished) Release(false); }
    }
}
