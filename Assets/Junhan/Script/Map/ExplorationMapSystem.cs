using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>
    /// 미니맵 + 전체 지도 + 탐험 안개 + 오브젝트/NPC 마커를 관리하는 지도 시스템.
    ///
    /// 1. 미니맵:
    /// - 플레이어 중심 일정 반경만 보여준다.
    ///
    /// 2. 전체 지도:
    /// - Tab 패널 안에서 전체 사각형 지도를 보여준다.
    /// - 플레이어가 지나간 영역만 밝아지고, 지나가지 않은 영역은 검정색으로 유지된다.
    ///
    /// 3. 마커:
    /// - MapMarker 컴포넌트가 붙은 오브젝트/NPC/포탈 등을 지도에 표시한다.
    /// - 기본 설정은 "탐험한 지역에 있는 마커만 표시"다.
    /// </summary>
    public class ExplorationMapSystem : MonoBehaviour
    {
        public static ExplorationMapSystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Character playerCharacter;

        [Header("Mini Map UI")]
        [SerializeField] private GameObject miniMapRoot;
        [SerializeField] private RawImage miniMapImage;
        [SerializeField] private RectTransform miniMapMarkerRoot;
        [SerializeField] private RectTransform miniMapPlayerIcon;

        [Header("Full Map UI")]
        [SerializeField] private GameObject fullMapPanelRoot;
        [SerializeField] private RawImage fullMapImage;
        [SerializeField] private RectTransform fullMapMarkerRoot;
        [SerializeField] private RectTransform fullMapPlayerIcon;

        [Header("Map World Settings")]
        [Tooltip("전체 지도가 표현할 월드 크기입니다. 160이면 시작 지점을 중심으로 가로 160, 세로 160 월드를 지도에 담습니다.")]
        [SerializeField] private float mapWorldSize = 160f;

        [Tooltip("true면 게임 시작 시 플레이어 위치를 전체 지도 중심으로 잡습니다.")]
        [SerializeField] private bool centerMapOnPlayerStart = true;

        [Tooltip("centerMapOnPlayerStart가 false일 때 사용하는 지도 중심 좌표입니다.")]
        [SerializeField] private Vector2 customMapCenter = Vector2.zero;

        [Header("Exploration Settings")]
        [Tooltip("지도 텍스처 해상도입니다. 256이면 256x256 픽셀 지도입니다.")]
        [SerializeField] private int textureResolution = 256;

        [Tooltip("플레이어 주변에서 한 번에 밝혀지는 월드 반경입니다.")]
        [SerializeField] private float revealWorldRadius = 7f;

        [Tooltip("지도 갱신 간격입니다. 너무 낮으면 성능 부담이 커질 수 있습니다.")]
        [SerializeField] private float updateInterval = 0.12f;

        [Header("Mini Map Settings")]
        [Tooltip("미니맵이 플레이어 주변 몇 월드 반경을 보여줄지 결정합니다.")]
        [SerializeField] private float miniMapWorldRadius = 18f;

        [Tooltip("미니맵에 마커를 표시할 최대 거리입니다. 보통 miniMapWorldRadius와 같게 두면 됩니다.")]
        [SerializeField] private float miniMapMarkerVisibleRadius = 18f;

        [Header("Map Colors")]
        [SerializeField] private Color unexploredColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color exploredColor = new Color(0.15f, 0.16f, 0.20f, 1f);
        [SerializeField] private Color currentRevealColor = new Color(0.23f, 0.24f, 0.30f, 1f);

        [Header("Player Icon")]
        [SerializeField] private Color playerIconColor = Color.white;
        [SerializeField] private Vector2 miniPlayerIconSize = new Vector2(12f, 12f);
        [SerializeField] private Vector2 fullPlayerIconSize = new Vector2(10f, 10f);

        [Header("Map Icon Size Correction")]
        [SerializeField] private bool compensateParentScale = true;

        [SerializeField] private float miniMarkerSizeMultiplier = 1f;
        [SerializeField] private float fullMarkerSizeMultiplier = 0.45f;

        [SerializeField] private float miniPlayerIconSizeMultiplier = 1f;
        [SerializeField] private float fullPlayerIconSizeMultiplier = 0.55f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private Texture2D explorationTexture;
        private bool[] exploredPixels;
        private Vector2 mapOriginWorld;
        private float updateTimer;

        private readonly List<MapMarker> markers = new List<MapMarker>();
        private readonly Dictionary<MapMarker, RectTransform> miniMarkerIcons = new Dictionary<MapMarker, RectTransform>();
        private readonly Dictionary<MapMarker, RectTransform> fullMarkerIcons = new Dictionary<MapMarker, RectTransform>();

        private Sprite runtimeWhiteSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            ResolveReferences();
            CreateExplorationTexture();
            SetupMapOrigin();
            SetupRuntimeUI();
            ScanSceneMarkers();
            ForceRevealCurrentPosition();
            RefreshAllUI();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (playerCharacter == null)
            {
                ResolveReferences();

                if (playerCharacter == null)
                {
                    return;
                }
            }

            updateTimer += Time.unscaledDeltaTime;

            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                RevealAroundPlayer();
                UpdateMarkerDiscovery();
                RefreshAllUI();
            }
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (playerCharacter == null)
            {
                if (levelManager != null && levelManager.PlayerCharacter != null)
                {
                    playerCharacter = levelManager.PlayerCharacter;
                }
                else
                {
                    playerCharacter = FindObjectOfType<Character>();
                }
            }
        }

        private void CreateExplorationTexture()
        {
            textureResolution = Mathf.Clamp(textureResolution, 32, 2048);

            explorationTexture = new Texture2D(
                textureResolution,
                textureResolution,
                TextureFormat.RGBA32,
                false
            );

            explorationTexture.filterMode = FilterMode.Point;
            explorationTexture.wrapMode = TextureWrapMode.Clamp;

            exploredPixels = new bool[textureResolution * textureResolution];

            Color[] colors = new Color[textureResolution * textureResolution];

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = unexploredColor;
                exploredPixels[i] = false;
            }

            explorationTexture.SetPixels(colors);
            explorationTexture.Apply(false);

            if (miniMapImage != null)
            {
                miniMapImage.texture = explorationTexture;
            }

            if (fullMapImage != null)
            {
                fullMapImage.texture = explorationTexture;
                fullMapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private void SetupMapOrigin()
        {
            Vector2 center;

            if (centerMapOnPlayerStart && playerCharacter != null)
            {
                center = playerCharacter.transform.position;
            }
            else
            {
                center = customMapCenter;
            }

            mapWorldSize = Mathf.Max(10f, mapWorldSize);
            mapOriginWorld = center - Vector2.one * (mapWorldSize * 0.5f);

            if (debugLog)
            {
                Debug.Log($"[ExplorationMapSystem] Map Origin: {mapOriginWorld} | Size: {mapWorldSize}");
            }
        }

        private void SetupRuntimeUI()
        {
            if (miniMapImage != null)
            {
                if (miniMapRoot == null)
                {
                    miniMapRoot = miniMapImage.gameObject;
                }

                if (miniMapMarkerRoot == null)
                {
                    miniMapMarkerRoot = CreateStretchRect("MiniMap_MarkerRoot", miniMapImage.transform);
                }

                if (miniMapPlayerIcon == null)
                {
                    miniMapPlayerIcon = CreateIcon("MiniMap_PlayerIcon", miniMapMarkerRoot, playerIconColor, miniPlayerIconSize);
                }
            }

            if (fullMapImage != null)
            {
                if (fullMapPanelRoot == null)
                {
                    fullMapPanelRoot = fullMapImage.gameObject;
                }

                if (fullMapMarkerRoot == null)
                {
                    fullMapMarkerRoot = CreateStretchRect("FullMap_MarkerRoot", fullMapImage.transform);
                }

                if (fullMapPlayerIcon == null)
                {
                    fullMapPlayerIcon = CreateIcon("FullMap_PlayerIcon", fullMapMarkerRoot, playerIconColor, fullPlayerIconSize);
                }
            }
        }

        private RectTransform CreateStretchRect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = go.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        private RectTransform CreateIcon(string objectName, Transform parent, Color color, Vector2 size)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            RectTransform rectTransform = go.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.sprite = GetRuntimeWhiteSprite();
            image.color = color;
            image.raycastTarget = false;

            return rectTransform;
        }

        private Sprite GetRuntimeWhiteSprite()
        {
            if (runtimeWhiteSprite != null)
            {
                return runtimeWhiteSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            runtimeWhiteSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f)
            );

            return runtimeWhiteSprite;
        }

        private Vector2 GetCorrectedIconSize(Vector2 baseSize, RectTransform parent, float multiplier)
        {
            Vector2 size = baseSize * Mathf.Max(0.01f, multiplier);

            if (!compensateParentScale || parent == null)
            {
                return size;
            }

            Vector3 scale = parent.lossyScale;

            float scaleX = Mathf.Max(0.01f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.01f, Mathf.Abs(scale.y));

            return new Vector2(size.x / scaleX, size.y / scaleY);
        }

        private void ScanSceneMarkers()
        {
            MapMarker[] sceneMarkers = FindObjectsOfType<MapMarker>(true);

            for (int i = 0; i < sceneMarkers.Length; i++)
            {
                RegisterMarker(sceneMarkers[i]);
            }
        }

        public void RegisterMarker(MapMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            if (!markers.Contains(marker))
            {
                markers.Add(marker);
            }

            EnsureMarkerIcons(marker);
        }

        public void UnregisterMarker(MapMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            markers.Remove(marker);

            if (miniMarkerIcons.TryGetValue(marker, out RectTransform miniIcon))
            {
                if (miniIcon != null)
                {
                    Destroy(miniIcon.gameObject);
                }

                miniMarkerIcons.Remove(marker);
            }

            if (fullMarkerIcons.TryGetValue(marker, out RectTransform fullIcon))
            {
                if (fullIcon != null)
                {
                    Destroy(fullIcon.gameObject);
                }

                fullMarkerIcons.Remove(marker);
            }
        }

        private void EnsureMarkerIcons(MapMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            if (marker.ShowOnMiniMap && miniMapMarkerRoot != null && !miniMarkerIcons.ContainsKey(marker))
            {
                RectTransform icon = CreateIcon(
                    "MiniMapMarker_" + marker.DisplayName,
                    miniMapMarkerRoot,
                    marker.MiniMapColor,
                    GetCorrectedIconSize(
    Vector2.one * marker.MiniMapSize,
    miniMapMarkerRoot,
    miniMarkerSizeMultiplier
)
                );

                miniMarkerIcons.Add(marker, icon);
            }

            if (marker.ShowOnFullMap && fullMapMarkerRoot != null && !fullMarkerIcons.ContainsKey(marker))
            {
                RectTransform icon = CreateIcon(
                    "FullMapMarker_" + marker.DisplayName,
                    fullMapMarkerRoot,
                    marker.FullMapColor,
                    GetCorrectedIconSize(
    Vector2.one * marker.FullMapSize,
    fullMapMarkerRoot,
    fullMarkerSizeMultiplier
)
                );

                fullMarkerIcons.Add(marker, icon);
            }
        }

        private void ForceRevealCurrentPosition()
        {
            RevealAroundPlayer();
            RefreshAllUI();
        }

        private void RevealAroundPlayer()
        {
            if (playerCharacter == null || explorationTexture == null || exploredPixels == null)
            {
                return;
            }

            Vector2 playerPosition = playerCharacter.transform.position;
            Vector2 normalized = WorldToMapNormalized(playerPosition);

            int centerX = Mathf.RoundToInt(normalized.x * (textureResolution - 1));
            int centerY = Mathf.RoundToInt(normalized.y * (textureResolution - 1));

            float pixelRadius = revealWorldRadius / mapWorldSize * textureResolution;
            int radius = Mathf.CeilToInt(pixelRadius);
            float radiusSqr = pixelRadius * pixelRadius;

            int xMin = Mathf.Clamp(centerX - radius, 0, textureResolution - 1);
            int xMax = Mathf.Clamp(centerX + radius, 0, textureResolution - 1);
            int yMin = Mathf.Clamp(centerY - radius, 0, textureResolution - 1);
            int yMax = Mathf.Clamp(centerY + radius, 0, textureResolution - 1);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;

                    if (dx * dx + dy * dy > radiusSqr)
                    {
                        continue;
                    }

                    int index = x + y * textureResolution;
                    exploredPixels[index] = true;

                    float innerRatio = Mathf.Clamp01((dx * dx + dy * dy) / Mathf.Max(1f, radiusSqr));
                    Color color = Color.Lerp(currentRevealColor, exploredColor, innerRatio);

                    explorationTexture.SetPixel(x, y, color);
                }
            }

            explorationTexture.Apply(false);
        }

        private void UpdateMarkerDiscovery()
        {
            if (playerCharacter == null)
            {
                return;
            }

            Vector2 playerPosition = playerCharacter.transform.position;

            for (int i = markers.Count - 1; i >= 0; i--)
            {
                MapMarker marker = markers[i];

                if (marker == null)
                {
                    markers.RemoveAt(i);
                    continue;
                }

                if (marker.IsDiscovered)
                {
                    continue;
                }

                float distance = Vector2.Distance(playerPosition, marker.transform.position);

                if (distance <= marker.DiscoverDistance || IsWorldPositionExplored(marker.transform.position))
                {
                    marker.SetDiscovered(true);
                }
            }
        }

        private void RefreshAllUI()
        {
            UpdateMiniMapView();
            UpdatePlayerIcons();
            UpdateMarkerIcons();
        }

        private void UpdateMiniMapView()
        {
            if (miniMapImage == null || playerCharacter == null)
            {
                return;
            }

            Vector2 playerNormalized = WorldToMapNormalized(playerCharacter.transform.position);
            float uvSize = Mathf.Clamp01((miniMapWorldRadius * 2f) / mapWorldSize);

            Rect uvRect = new Rect(
                playerNormalized.x - uvSize * 0.5f,
                playerNormalized.y - uvSize * 0.5f,
                uvSize,
                uvSize
            );

            miniMapImage.uvRect = uvRect;
        }

        private void UpdatePlayerIcons()
        {
            if (playerCharacter == null)
            {
                return;
            }

            if (miniMapPlayerIcon != null)
            {
                miniMapPlayerIcon.anchoredPosition = Vector2.zero;
                miniMapPlayerIcon.sizeDelta = GetCorrectedIconSize(
    miniPlayerIconSize,
    miniMapMarkerRoot,
    miniPlayerIconSizeMultiplier
);
            }

            if (fullMapPlayerIcon != null && fullMapMarkerRoot != null)
            {
                fullMapPlayerIcon.anchoredPosition =
                    WorldToFullMapAnchoredPosition(playerCharacter.transform.position, fullMapMarkerRoot);
                fullMapPlayerIcon.sizeDelta = GetCorrectedIconSize(
    fullPlayerIconSize,
    fullMapMarkerRoot,
    fullPlayerIconSizeMultiplier
);
            }
        }

        private void UpdateMarkerIcons()
        {
            if (playerCharacter == null)
            {
                return;
            }

            Vector2 playerPosition = playerCharacter.transform.position;

            for (int i = markers.Count - 1; i >= 0; i--)
            {
                MapMarker marker = markers[i];

                if (marker == null)
                {
                    markers.RemoveAt(i);
                    continue;
                }

                EnsureMarkerIcons(marker);

                bool explored = IsWorldPositionExplored(marker.transform.position);
                bool visibleByDiscovery = marker.AlwaysVisible || marker.IsDiscovered || !marker.HideUntilDiscovered;
                bool fullVisible = marker.ShowOnFullMap && explored && visibleByDiscovery;
                bool miniVisible = marker.ShowOnMiniMap && explored && visibleByDiscovery;

                if (miniMarkerIcons.TryGetValue(marker, out RectTransform miniIcon))
                {
                    float distance = Vector2.Distance(playerPosition, marker.transform.position);
                    bool withinMiniRadius = distance <= miniMapMarkerVisibleRadius;

                    miniIcon.gameObject.SetActive(miniVisible && withinMiniRadius);

                    if (miniIcon.gameObject.activeSelf)
                    {
                        miniIcon.anchoredPosition =
                            WorldToMiniMapAnchoredPosition(marker.transform.position, playerPosition, miniMapMarkerRoot);

                        miniIcon.sizeDelta = GetCorrectedIconSize(
    Vector2.one * marker.MiniMapSize,
    miniMapMarkerRoot,
    miniMarkerSizeMultiplier
);

                        Image image = miniIcon.GetComponent<Image>();
                        if (image != null)
                        {
                            image.color = marker.MiniMapColor;
                        }
                    }
                }

                if (fullMarkerIcons.TryGetValue(marker, out RectTransform fullIcon))
                {
                    fullIcon.gameObject.SetActive(fullVisible);

                    if (fullIcon.gameObject.activeSelf)
                    {
                        fullIcon.anchoredPosition =
                            WorldToFullMapAnchoredPosition(marker.transform.position, fullMapMarkerRoot);

                        fullIcon.sizeDelta = GetCorrectedIconSize(
    Vector2.one * marker.FullMapSize,
    fullMapMarkerRoot,
    fullMarkerSizeMultiplier
);

                        Image image = fullIcon.GetComponent<Image>();
                        if (image != null)
                        {
                            image.color = marker.FullMapColor;
                        }
                    }
                }
            }
        }

        private Vector2 WorldToMapNormalized(Vector2 worldPosition)
        {
            Vector2 normalized = (worldPosition - mapOriginWorld) / mapWorldSize;
            return new Vector2(
                Mathf.Clamp01(normalized.x),
                Mathf.Clamp01(normalized.y)
            );
        }

        private Vector2 WorldToFullMapAnchoredPosition(Vector2 worldPosition, RectTransform mapRoot)
        {
            if (mapRoot == null)
            {
                return Vector2.zero;
            }

            Vector2 normalized = WorldToMapNormalized(worldPosition);
            Rect rect = mapRoot.rect;

            return new Vector2(
                (normalized.x - 0.5f) * rect.width,
                (normalized.y - 0.5f) * rect.height
            );
        }

        private Vector2 WorldToMiniMapAnchoredPosition(Vector2 markerWorldPosition, Vector2 playerWorldPosition, RectTransform mapRoot)
        {
            if (mapRoot == null)
            {
                return Vector2.zero;
            }

            Vector2 relative = markerWorldPosition - playerWorldPosition;
            float diameter = Mathf.Max(0.1f, miniMapWorldRadius * 2f);
            Rect rect = mapRoot.rect;

            return new Vector2(
                relative.x / diameter * rect.width,
                relative.y / diameter * rect.height
            );
        }

        public bool IsWorldPositionExplored(Vector2 worldPosition)
        {
            if (exploredPixels == null)
            {
                return false;
            }

            Vector2 normalized = WorldToMapNormalized(worldPosition);

            int x = Mathf.RoundToInt(normalized.x * (textureResolution - 1));
            int y = Mathf.RoundToInt(normalized.y * (textureResolution - 1));

            x = Mathf.Clamp(x, 0, textureResolution - 1);
            y = Mathf.Clamp(y, 0, textureResolution - 1);

            int index = x + y * textureResolution;

            if (index < 0 || index >= exploredPixels.Length)
            {
                return false;
            }

            return exploredPixels[index];
        }

        public void SetMiniMapVisible(bool visible)
        {
            if (miniMapRoot != null)
            {
                miniMapRoot.SetActive(visible);
            }
        }

        public void SetFullMapPanelVisible(bool visible)
        {
            if (fullMapPanelRoot != null)
            {
                fullMapPanelRoot.SetActive(visible);
            }

            RefreshAllUI();
        }

        public void RecenterMapOnPlayer()
        {
            if (playerCharacter == null)
            {
                ResolveReferences();
            }

            if (playerCharacter == null)
            {
                return;
            }

            mapOriginWorld = (Vector2)playerCharacter.transform.position - Vector2.one * (mapWorldSize * 0.5f);
            CreateExplorationTexture();
            ForceRevealCurrentPosition();
        }
    }
}