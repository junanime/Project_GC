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
    ///
    /// 4. 전체 지도 Navigation:
    /// - FullMapImage의 RectTransform 자체는 움직이지 않는다.
    /// - RawImage.uvRect를 이용해 지도 내부 시점만 이동/확대한다.
    /// - 플레이어와 전체 지도 마커는 현재 uvRect 기준으로 위치를 다시 계산한다.
    /// </summary>
    public class ExplorationMapSystem : MonoBehaviour
    {
        public static ExplorationMapSystem Instance { get; private set; }

        // ========================================
        // References
        // ========================================

        [Header("References")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Character playerCharacter;


        // ========================================
        // Mini Map UI
        // ========================================

        [Header("Mini Map UI")]
        [SerializeField] private GameObject miniMapRoot;
        [SerializeField] private RawImage miniMapImage;
        [SerializeField] private RectTransform miniMapMarkerRoot;
        [SerializeField] private RectTransform miniMapPlayerIcon;


        // ========================================
        // Full Map UI
        // ========================================

        [Header("Full Map UI")]
        [SerializeField] private GameObject fullMapPanelRoot;
        [SerializeField] private RawImage fullMapImage;
        [SerializeField] private RectTransform fullMapMarkerRoot;
        [SerializeField] private RectTransform fullMapPlayerIcon;


        // ========================================
        // Map World Settings
        // ========================================

        [Header("Map World Settings")]

        [Tooltip(
            "전체 지도가 표현할 월드 크기입니다. " +
            "160이면 시작 지점을 중심으로 가로 160, 세로 160 월드를 지도에 담습니다."
        )]
        [SerializeField] private float mapWorldSize = 160f;

        [Tooltip(
            "true면 게임 시작 시 플레이어 위치를 전체 지도 중심으로 잡습니다."
        )]
        [SerializeField] private bool centerMapOnPlayerStart = true;

        [Tooltip(
            "centerMapOnPlayerStart가 false일 때 사용하는 지도 중심 좌표입니다."
        )]
        [SerializeField] private Vector2 customMapCenter = Vector2.zero;


        // ========================================
        // Exploration Settings
        // ========================================

        [Header("Exploration Settings")]

        [Tooltip(
            "지도 텍스처 해상도입니다. 256이면 256x256 픽셀 지도입니다."
        )]
        [SerializeField] private int textureResolution = 256;

        [Tooltip(
            "플레이어 주변에서 한 번에 밝혀지는 월드 반경입니다."
        )]
        [SerializeField] private float revealWorldRadius = 7f;

        [Tooltip(
            "지도 갱신 간격입니다. 너무 낮으면 성능 부담이 커질 수 있습니다."
        )]
        [SerializeField] private float updateInterval = 0.12f;


        // ========================================
        // Mini Map Settings
        // ========================================

        [Header("Mini Map Settings")]

        [Tooltip(
            "미니맵이 플레이어 주변 몇 월드 반경을 보여줄지 결정합니다."
        )]
        [SerializeField] private float miniMapWorldRadius = 18f;

        [Tooltip(
            "미니맵에 마커를 표시할 최대 거리입니다. " +
            "보통 miniMapWorldRadius와 같게 두면 됩니다."
        )]
        [SerializeField] private float miniMapMarkerVisibleRadius = 18f;


        // ========================================
        // Map Colors
        // ========================================

        [Header("Map Colors")]

        [SerializeField]
        private Color unexploredColor =
            new Color(0f, 0f, 0f, 1f);

        [SerializeField]
        private Color exploredColor =
            new Color(0.15f, 0.16f, 0.20f, 1f);

        [SerializeField]
        private Color currentRevealColor =
            new Color(0.23f, 0.24f, 0.30f, 1f);


        // ========================================
        // Player Icon
        // ========================================

        [Header("Player Icon")]

        [SerializeField] private Color playerIconColor = Color.white;

        [SerializeField]
        private Vector2 miniPlayerIconSize =
            new Vector2(12f, 12f);

        [SerializeField]
        private Vector2 fullPlayerIconSize =
            new Vector2(10f, 10f);


        // ========================================
        // Map Icon Size Correction
        // ========================================

        [Header("Map Icon Size Correction")]

        [SerializeField]
        private bool compensateParentScale = true;

        [SerializeField]
        private float miniMarkerSizeMultiplier = 1f;

        [SerializeField]
        private float fullMarkerSizeMultiplier = 0.45f;

        [SerializeField]
        private float miniPlayerIconSizeMultiplier = 1f;

        [SerializeField]
        private float fullPlayerIconSizeMultiplier = 0.55f;


        // ========================================
        // Debug
        // ========================================

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;


        // ========================================
        // Runtime
        // ========================================

        private Texture2D explorationTexture;

        private bool[] exploredPixels;

        private Vector2 mapOriginWorld;

        private float updateTimer;


        private readonly List<MapMarker> markers =
            new List<MapMarker>();


        private readonly Dictionary<MapMarker, RectTransform>
            miniMarkerIcons =
                new Dictionary<MapMarker, RectTransform>();


        private readonly Dictionary<MapMarker, RectTransform>
            fullMarkerIcons =
                new Dictionary<MapMarker, RectTransform>();


        private Sprite runtimeWhiteSprite;


        // ========================================
        // Unity
        // ========================================

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
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


            updateTimer +=
                Time.unscaledDeltaTime;


            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;

                RevealAroundPlayer();

                UpdateMarkerDiscovery();

                RefreshAllUI();
            }
        }


        // ========================================
        // References
        // ========================================

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager =
                    FindObjectOfType<LevelManager>();
            }


            if (playerCharacter == null)
            {
                if (levelManager != null &&
                    levelManager.PlayerCharacter != null)
                {
                    playerCharacter =
                        levelManager.PlayerCharacter;
                }
                else
                {
                    playerCharacter =
                        FindObjectOfType<Character>();
                }
            }
        }


        // ========================================
        // Exploration Texture
        // ========================================

        private void CreateExplorationTexture()
        {
            textureResolution =
                Mathf.Clamp(
                    textureResolution,
                    32,
                    2048
                );


            explorationTexture = new Texture2D(
    textureResolution,
    textureResolution,
    TextureFormat.RGBA32,
    false

    );

            explorationTexture.filterMode = FilterMode.Bilinear;
            explorationTexture.wrapMode = TextureWrapMode.Clamp;


            exploredPixels =
                new bool[
                    textureResolution *
                    textureResolution
                ];


            Color[] colors =
                new Color[
                    textureResolution *
                    textureResolution
                ];


            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] =
                    unexploredColor;

                exploredPixels[i] =
                    false;
            }


            explorationTexture.SetPixels(
                colors
            );

            explorationTexture.Apply(false);


            if (miniMapImage != null)
            {
                miniMapImage.texture =
                    explorationTexture;
            }


            if (fullMapImage != null)
            {
                fullMapImage.texture =
                    explorationTexture;

                // 전체 지도 초기 View
                fullMapImage.uvRect =
                    new Rect(
                        0f,
                        0f,
                        1f,
                        1f
                    );
            }
        }


        // ========================================
        // Map Origin
        // ========================================

        private void SetupMapOrigin()
        {
            Vector2 center;


            if (centerMapOnPlayerStart &&
                playerCharacter != null)
            {
                center =
                    playerCharacter.transform.position;
            }
            else
            {
                center =
                    customMapCenter;
            }


            mapWorldSize =
                Mathf.Max(
                    10f,
                    mapWorldSize
                );


            mapOriginWorld =
                center -
                Vector2.one *
                (mapWorldSize * 0.5f);


            if (debugLog)
            {
                Debug.Log(
                    $"[ExplorationMapSystem] " +
                    $"Map Origin: {mapOriginWorld} " +
                    $"| Size: {mapWorldSize}"
                );
            }
        }


        // ========================================
        // Runtime UI
        // ========================================

        private void SetupRuntimeUI()
        {
            // ========================================
            // Mini Map
            // ========================================

            if (miniMapImage != null)
            {
                if (miniMapRoot == null)
                {
                    miniMapRoot =
                        miniMapImage.gameObject;
                }


                if (miniMapMarkerRoot == null)
                {
                    miniMapMarkerRoot =
                        CreateStretchRect(
                            "MiniMap_MarkerRoot",
                            miniMapImage.transform
                        );
                }


                if (miniMapPlayerIcon == null)
                {
                    miniMapPlayerIcon =
                        CreateIcon(
                            "MiniMap_PlayerIcon",
                            miniMapMarkerRoot,
                            playerIconColor,
                            miniPlayerIconSize
                        );
                }
            }


            // ========================================
            // Full Map
            // ========================================

            if (fullMapImage != null)
            {
                if (fullMapPanelRoot == null)
                {
                    fullMapPanelRoot =
                        fullMapImage.gameObject;
                }


                if (fullMapMarkerRoot == null)
                {
                    fullMapMarkerRoot =
                        CreateStretchRect(
                            "FullMap_MarkerRoot",
                            fullMapImage.transform
                        );
                }


                if (fullMapPlayerIcon == null)
                {
                    fullMapPlayerIcon =
                        CreateIcon(
                            "FullMap_PlayerIcon",
                            fullMapMarkerRoot,
                            playerIconColor,
                            fullPlayerIconSize
                        );
                }
            }
        }


        // ========================================
        // Runtime Rect
        // ========================================

        private RectTransform CreateStretchRect(
            string objectName,
            Transform parent)
        {
            GameObject go =
                new GameObject(
                    objectName,
                    typeof(RectTransform)
                );


            RectTransform rectTransform =
                go.GetComponent<RectTransform>();


            rectTransform.SetParent(
                parent,
                false
            );


            rectTransform.anchorMin =
                Vector2.zero;

            rectTransform.anchorMax =
                Vector2.one;

            rectTransform.pivot =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rectTransform.offsetMin =
                Vector2.zero;

            rectTransform.offsetMax =
                Vector2.zero;


            return rectTransform;
        }


        // ========================================
        // Runtime Icon
        // ========================================

        private RectTransform CreateIcon(
            string objectName,
            Transform parent,
            Color color,
            Vector2 size)
        {
            GameObject go =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(Image)
                );


            RectTransform rectTransform =
                go.GetComponent<RectTransform>();


            rectTransform.SetParent(
                parent,
                false
            );


            rectTransform.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rectTransform.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f
                );

            rectTransform.pivot =
                new Vector2(
                    0.5f,
                    0.5f
                );


            rectTransform.sizeDelta =
                size;


            Image image =
                go.GetComponent<Image>();


            image.sprite =
                GetRuntimeWhiteSprite();

            image.color =
                color;

            image.raycastTarget =
                false;


            return rectTransform;
        }


        // ========================================
        // Runtime White Sprite
        // ========================================

        private Sprite GetRuntimeWhiteSprite()
        {
            if (runtimeWhiteSprite != null)
            {
                return runtimeWhiteSprite;
            }


            Texture2D texture =
                new Texture2D(
                    1,
                    1,
                    TextureFormat.RGBA32,
                    false
                );


            texture.SetPixel(
                0,
                0,
                Color.white
            );


            texture.Apply(false);

            texture.wrapMode =
                TextureWrapMode.Clamp;

            texture.filterMode =
                FilterMode.Point;


            runtimeWhiteSprite =
                Sprite.Create(
                    texture,
                    new Rect(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    )
                );


            return runtimeWhiteSprite;
        }


        // ========================================
        // Icon Size Correction
        // ========================================

        private Vector2 GetCorrectedIconSize(
            Vector2 baseSize,
            RectTransform parent,
            float multiplier)
        {
            Vector2 size =
                baseSize *
                Mathf.Max(
                    0.01f,
                    multiplier
                );


            if (!compensateParentScale ||
                parent == null)
            {
                return size;
            }


            Vector3 scale =
                parent.lossyScale;


            float scaleX =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(scale.x)
                );

            float scaleY =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(scale.y)
                );


            return new Vector2(
                size.x / scaleX,
                size.y / scaleY
            );
        }


        // ========================================
        // Scene Marker Scan
        // ========================================

        private void ScanSceneMarkers()
        {
            MapMarker[] sceneMarkers =
                FindObjectsOfType<MapMarker>(true);


            for (int i = 0;
                 i < sceneMarkers.Length;
                 i++)
            {
                RegisterMarker(
                    sceneMarkers[i]
                );
            }
        }


        // ========================================
        // Marker Register
        // ========================================

        public void RegisterMarker(
            MapMarker marker)
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


        // ========================================
        // Marker Unregister
        // ========================================

        public void UnregisterMarker(
            MapMarker marker)
        {
            if (marker == null)
            {
                return;
            }


            markers.Remove(marker);


            if (miniMarkerIcons.TryGetValue(
                    marker,
                    out RectTransform miniIcon))
            {
                if (miniIcon != null)
                {
                    Destroy(
                        miniIcon.gameObject
                    );
                }

                miniMarkerIcons.Remove(
                    marker
                );
            }


            if (fullMarkerIcons.TryGetValue(
                    marker,
                    out RectTransform fullIcon))
            {
                if (fullIcon != null)
                {
                    Destroy(
                        fullIcon.gameObject
                    );
                }

                fullMarkerIcons.Remove(
                    marker
                );
            }
        }


        // ========================================
        // Ensure Marker Icon
        // ========================================

        private void EnsureMarkerIcons(
            MapMarker marker)
        {
            if (marker == null)
            {
                return;
            }


            // ========================================
            // Mini
            // ========================================

            if (marker.ShowOnMiniMap &&
                miniMapMarkerRoot != null &&
                !miniMarkerIcons.ContainsKey(marker))
            {
                RectTransform icon =
                    CreateIcon(
                        "MiniMapMarker_" +
                        marker.DisplayName,

                        miniMapMarkerRoot,

                        marker.MiniMapColor,

                        GetCorrectedIconSize(
                            Vector2.one *
                            marker.MiniMapSize,

                            miniMapMarkerRoot,

                            miniMarkerSizeMultiplier
                        )
                    );


                miniMarkerIcons.Add(
                    marker,
                    icon
                );
            }


            // ========================================
            // Full
            // ========================================

            if (marker.ShowOnFullMap &&
                fullMapMarkerRoot != null &&
                !fullMarkerIcons.ContainsKey(marker))
            {
                RectTransform icon =
                    CreateIcon(
                        "FullMapMarker_" +
                        marker.DisplayName,

                        fullMapMarkerRoot,

                        marker.FullMapColor,

                        GetCorrectedIconSize(
                            Vector2.one *
                            marker.FullMapSize,

                            fullMapMarkerRoot,

                            fullMarkerSizeMultiplier
                        )
                    );


                fullMarkerIcons.Add(
                    marker,
                    icon
                );
            }
        }


        // ========================================
        // Force Reveal
        // ========================================

        private void ForceRevealCurrentPosition()
        {
            RevealAroundPlayer();

            RefreshAllUI();
        }


        // ========================================
        // Reveal
        // ========================================

        private void RevealAroundPlayer()
        {
            if (playerCharacter == null ||
                explorationTexture == null ||
                exploredPixels == null)
            {
                return;
            }


            Vector2 playerPosition =
                playerCharacter.transform.position;


            Vector2 normalized =
                WorldToMapNormalized(
                    playerPosition
                );


            int centerX =
                Mathf.RoundToInt(
                    normalized.x *
                    (textureResolution - 1)
                );


            int centerY =
                Mathf.RoundToInt(
                    normalized.y *
                    (textureResolution - 1)
                );


            float pixelRadius =
                revealWorldRadius /
                mapWorldSize *
                textureResolution;


            int radius =
                Mathf.CeilToInt(
                    pixelRadius
                );


            float radiusSqr =
                pixelRadius *
                pixelRadius;


            int xMin =
                Mathf.Clamp(
                    centerX - radius,
                    0,
                    textureResolution - 1
                );


            int xMax =
                Mathf.Clamp(
                    centerX + radius,
                    0,
                    textureResolution - 1
                );


            int yMin =
                Mathf.Clamp(
                    centerY - radius,
                    0,
                    textureResolution - 1
                );


            int yMax =
                Mathf.Clamp(
                    centerY + radius,
                    0,
                    textureResolution - 1
                );


            for (int y = yMin;
                 y <= yMax;
                 y++)
            {
                for (int x = xMin;
                     x <= xMax;
                     x++)
                {
                    float dx =
                        x - centerX;

                    float dy =
                        y - centerY;


                    if (dx * dx +
                        dy * dy >
                        radiusSqr)
                    {
                        continue;
                    }


                    int index =
                        x +
                        y *
                        textureResolution;


                    exploredPixels[index] =
                        true;


                    float innerRatio =
                        Mathf.Clamp01(
                            (
                                dx * dx +
                                dy * dy
                            )
                            /
                            Mathf.Max(
                                1f,
                                radiusSqr
                            )
                        );


                    Color color =
                        Color.Lerp(
                            currentRevealColor,
                            exploredColor,
                            innerRatio
                        );


                    explorationTexture.SetPixel(
                        x,
                        y,
                        color
                    );
                }
            }


            explorationTexture.Apply(
                false
            );
        }


        // ========================================
        // Marker Discovery
        // ========================================

        private void UpdateMarkerDiscovery()
        {
            if (playerCharacter == null)
            {
                return;
            }


            Vector2 playerPosition =
                playerCharacter.transform.position;


            for (int i = markers.Count - 1;
                 i >= 0;
                 i--)
            {
                MapMarker marker =
                    markers[i];


                if (marker == null)
                {
                    markers.RemoveAt(i);
                    continue;
                }


                if (marker.IsDiscovered)
                {
                    continue;
                }


                float distance =
                    Vector2.Distance(
                        playerPosition,
                        marker.transform.position
                    );


                if (distance <=
                    marker.DiscoverDistance ||
                    IsWorldPositionExplored(
                        marker.transform.position
                    ))
                {
                    marker.SetDiscovered(
                        true
                    );
                }
            }
        }


        // ========================================
        // Refresh All
        // ========================================

        private void RefreshAllUI()
        {
            UpdateMiniMapView();

            UpdatePlayerIcons();

            UpdateMarkerIcons();
        }


        // ========================================
        // Full Map Navigation 전용 즉시 갱신
        // ========================================

        /// <summary>
        /// FullMapNavigationController가
        /// uvRect를 변경한 직후 호출한다.
        ///
        /// 현재 FullMap uvRect 기준으로
        /// 플레이어와 전체 지도 마커 위치를 즉시 다시 계산한다.
        /// </summary>
        public void RefreshFullMapView()
        {
            UpdateFullMapPlayerIcon();

            UpdateFullMapMarkerIcons();
        }


        // ========================================
        // Mini Map View
        // ========================================

        private void UpdateMiniMapView()
        {
            if (miniMapImage == null ||
                playerCharacter == null)
            {
                return;
            }


            Vector2 playerNormalized =
                WorldToMapNormalized(
                    playerCharacter.transform.position
                );


            float uvSize =
                Mathf.Clamp01(
                    (miniMapWorldRadius * 2f)
                    /
                    mapWorldSize
                );


            Rect uvRect =
                new Rect(
                    playerNormalized.x -
                    uvSize * 0.5f,

                    playerNormalized.y -
                    uvSize * 0.5f,

                    uvSize,

                    uvSize
                );


            miniMapImage.uvRect =
                uvRect;
        }


        // ========================================
        // Player Icons
        // ========================================

        private void UpdatePlayerIcons()
        {
            if (playerCharacter == null)
            {
                return;
            }


            // ========================================
            // Mini
            // ========================================

            if (miniMapPlayerIcon != null)
            {
                miniMapPlayerIcon.anchoredPosition =
                    Vector2.zero;


                miniMapPlayerIcon.sizeDelta =
                    GetCorrectedIconSize(
                        miniPlayerIconSize,
                        miniMapMarkerRoot,
                        miniPlayerIconSizeMultiplier
                    );
            }


            // ========================================
            // Full
            // ========================================

            UpdateFullMapPlayerIcon();
        }


        // ========================================
        // Full Player Icon
        // ========================================

        private void UpdateFullMapPlayerIcon()
        {
            if (playerCharacter == null ||
                fullMapPlayerIcon == null ||
                fullMapMarkerRoot == null)
            {
                return;
            }


            fullMapPlayerIcon.anchoredPosition =
                WorldToFullMapAnchoredPosition(
                    playerCharacter.transform.position,
                    fullMapMarkerRoot
                );


            fullMapPlayerIcon.sizeDelta =
                GetCorrectedIconSize(
                    fullPlayerIconSize,
                    fullMapMarkerRoot,
                    fullPlayerIconSizeMultiplier
                );


            // 현재 확대/이동된 지도 영역 밖이면
            // 플레이어 아이콘을 숨김
            bool insideView =
                IsWorldPositionInsideCurrentFullMapView(
                    playerCharacter.transform.position
                );


            fullMapPlayerIcon.gameObject.SetActive(
                insideView
            );
        }


        // ========================================
        // Marker Icons
        // ========================================

        private void UpdateMarkerIcons()
        {
            if (playerCharacter == null)
            {
                return;
            }


            Vector2 playerPosition =
                playerCharacter.transform.position;


            for (int i = markers.Count - 1;
                 i >= 0;
                 i--)
            {
                MapMarker marker =
                    markers[i];


                if (marker == null)
                {
                    markers.RemoveAt(i);
                    continue;
                }


                EnsureMarkerIcons(
                    marker
                );


                bool explored =
                    IsWorldPositionExplored(
                        marker.transform.position
                    );


                bool visibleByDiscovery =
                    marker.AlwaysVisible ||
                    marker.IsDiscovered ||
                    !marker.HideUntilDiscovered;


                bool fullVisible =
                    marker.ShowOnFullMap &&
                    explored &&
                    visibleByDiscovery;


                bool miniVisible =
                    marker.ShowOnMiniMap &&
                    explored &&
                    visibleByDiscovery;


                // ========================================
                // Mini
                // ========================================

                if (miniMarkerIcons.TryGetValue(
                        marker,
                        out RectTransform miniIcon))
                {
                    float distance =
                        Vector2.Distance(
                            playerPosition,
                            marker.transform.position
                        );


                    bool withinMiniRadius =
                        distance <=
                        miniMapMarkerVisibleRadius;


                    miniIcon.gameObject.SetActive(
                        miniVisible &&
                        withinMiniRadius
                    );


                    if (miniIcon.gameObject.activeSelf)
                    {
                        miniIcon.anchoredPosition =
                            WorldToMiniMapAnchoredPosition(
                                marker.transform.position,
                                playerPosition,
                                miniMapMarkerRoot
                            );


                        miniIcon.sizeDelta =
                            GetCorrectedIconSize(
                                Vector2.one *
                                marker.MiniMapSize,

                                miniMapMarkerRoot,

                                miniMarkerSizeMultiplier
                            );


                        Image image =
                            miniIcon.GetComponent<Image>();


                        if (image != null)
                        {
                            image.color =
                                marker.MiniMapColor;
                        }
                    }
                }
            }


            // Full Map은 별도로 갱신
            UpdateFullMapMarkerIcons();
        }


        // ========================================
        // Full Map Marker Icons
        // ========================================

        private void UpdateFullMapMarkerIcons()
        {
            for (int i = markers.Count - 1;
                 i >= 0;
                 i--)
            {
                MapMarker marker =
                    markers[i];


                if (marker == null)
                {
                    markers.RemoveAt(i);
                    continue;
                }


                EnsureMarkerIcons(
                    marker
                );


                if (!fullMarkerIcons.TryGetValue(
                        marker,
                        out RectTransform fullIcon))
                {
                    continue;
                }


                bool explored =
                    IsWorldPositionExplored(
                        marker.transform.position
                    );


                bool visibleByDiscovery =
                    marker.AlwaysVisible ||
                    marker.IsDiscovered ||
                    !marker.HideUntilDiscovered;


                bool insideCurrentView =
                    IsWorldPositionInsideCurrentFullMapView(
                        marker.transform.position
                    );


                bool fullVisible =
                    marker.ShowOnFullMap &&
                    explored &&
                    visibleByDiscovery &&
                    insideCurrentView;


                fullIcon.gameObject.SetActive(
                    fullVisible
                );


                if (!fullVisible)
                {
                    continue;
                }


                fullIcon.anchoredPosition =
                    WorldToFullMapAnchoredPosition(
                        marker.transform.position,
                        fullMapMarkerRoot
                    );


                fullIcon.sizeDelta =
                    GetCorrectedIconSize(
                        Vector2.one *
                        marker.FullMapSize,

                        fullMapMarkerRoot,

                        fullMarkerSizeMultiplier
                    );


                Image image =
                    fullIcon.GetComponent<Image>();


                if (image != null)
                {
                    image.color =
                        marker.FullMapColor;
                }
            }
        }


        // ========================================
        // World → Map Normalized
        // ========================================

        private Vector2 WorldToMapNormalized(
            Vector2 worldPosition)
        {
            Vector2 normalized =
                (worldPosition -
                 mapOriginWorld)
                /
                mapWorldSize;


            return new Vector2(
                Mathf.Clamp01(
                    normalized.x
                ),

                Mathf.Clamp01(
                    normalized.y
                )
            );
        }


        // ========================================
        // World → Full Map
        // ========================================

        /// <summary>
        /// 월드 좌표를 현재 FullMapImage의 uvRect 기준으로
        /// 전체 지도 UI 좌표로 변환한다.
        ///
        /// uvRect = (0,0,1,1)이면 기존 전체 지도와 동일.
        ///
        /// 예:
        /// uvRect = (0.25, 0.25, 0.5, 0.5)
        /// → 전체 지도 가운데 50% 영역을 확대해서 보고 있는 상태.
        /// </summary>
        private Vector2 WorldToFullMapAnchoredPosition(
            Vector2 worldPosition,
            RectTransform mapRoot)
        {
            if (mapRoot == null)
            {
                return Vector2.zero;
            }


            // 전체 지도 기준 0~1 위치
            Vector2 normalized =
                WorldToMapNormalized(
                    worldPosition
                );


            // ========================================
            // 현재 FullMap에서 보고 있는 UV 범위
            // ========================================

            Rect viewRect =
                new Rect(
                    0f,
                    0f,
                    1f,
                    1f
                );


            if (fullMapImage != null)
            {
                viewRect =
                    fullMapImage.uvRect;
            }


            float viewWidth =
                Mathf.Max(
                    0.0001f,
                    viewRect.width
                );


            float viewHeight =
                Mathf.Max(
                    0.0001f,
                    viewRect.height
                );


            // ========================================
            // 전체 지도 0~1 좌표를
            // 현재 uvRect 내부의 0~1 좌표로 변환
            // ========================================

            float viewX =
                (
                    normalized.x -
                    viewRect.xMin
                )
                /
                viewWidth;


            float viewY =
                (
                    normalized.y -
                    viewRect.yMin
                )
                /
                viewHeight;


            Rect rect =
                mapRoot.rect;


            return new Vector2(
                (viewX - 0.5f) *
                rect.width,

                (viewY - 0.5f) *
                rect.height
            );
        }


        // ========================================
        // 현재 Full Map View 안에 있는지
        // ========================================

        private bool IsWorldPositionInsideCurrentFullMapView(
            Vector2 worldPosition)
        {
            if (fullMapImage == null)
            {
                return true;
            }


            Vector2 normalized =
                WorldToMapNormalized(
                    worldPosition
                );


            Rect uv =
                fullMapImage.uvRect;


            return
                normalized.x >= uv.xMin &&
                normalized.x <= uv.xMax &&
                normalized.y >= uv.yMin &&
                normalized.y <= uv.yMax;
        }


        // ========================================
        // World → Mini Map
        // ========================================

        private Vector2 WorldToMiniMapAnchoredPosition(
            Vector2 markerWorldPosition,
            Vector2 playerWorldPosition,
            RectTransform mapRoot)
        {
            if (mapRoot == null)
            {
                return Vector2.zero;
            }


            Vector2 relative =
                markerWorldPosition -
                playerWorldPosition;


            float diameter =
                Mathf.Max(
                    0.1f,
                    miniMapWorldRadius * 2f
                );


            Rect rect =
                mapRoot.rect;


            return new Vector2(
                relative.x /
                diameter *
                rect.width,

                relative.y /
                diameter *
                rect.height
            );
        }


        // ========================================
        // Explored?
        // ========================================

        public bool IsWorldPositionExplored(
            Vector2 worldPosition)
        {
            if (exploredPixels == null)
            {
                return false;
            }


            Vector2 normalized =
                WorldToMapNormalized(
                    worldPosition
                );


            int x =
                Mathf.RoundToInt(
                    normalized.x *
                    (textureResolution - 1)
                );


            int y =
                Mathf.RoundToInt(
                    normalized.y *
                    (textureResolution - 1)
                );


            x =
                Mathf.Clamp(
                    x,
                    0,
                    textureResolution - 1
                );


            y =
                Mathf.Clamp(
                    y,
                    0,
                    textureResolution - 1
                );


            int index =
                x +
                y *
                textureResolution;


            if (index < 0 ||
                index >= exploredPixels.Length)
            {
                return false;
            }


            return exploredPixels[index];
        }


        // ========================================
        // Mini Map Visible
        // ========================================

        public void SetMiniMapVisible(
            bool visible)
        {
            if (miniMapRoot != null)
            {
                miniMapRoot.SetActive(
                    visible
                );
            }
        }


        // ========================================
        // Full Map Visible
        // ========================================

        public void SetFullMapPanelVisible(
            bool visible)
        {
            if (fullMapPanelRoot != null)
            {
                fullMapPanelRoot.SetActive(
                    visible
                );
            }


            RefreshAllUI();
        }


        // ========================================
        // Recenter Map
        // ========================================

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


            mapOriginWorld =
                (Vector2)
                playerCharacter.transform.position
                -
                Vector2.one *
                (mapWorldSize * 0.5f);


            CreateExplorationTexture();

            ForceRevealCurrentPosition();
        }
    }

}