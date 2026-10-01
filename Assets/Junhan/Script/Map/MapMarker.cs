using UnityEngine;

namespace Vampire
{
    public enum MapMarkerKind
    {
        Object,
        NPC,
        Shop,
        Chest,
        Portal,
        Event,
        Boss,
        Custom,
        EliteSpawner
    }

    /// <summary>
    /// 지도에 표시하고 싶은 오브젝트/NPC/상자/포탈에 붙이는 마커 컴포넌트.
    ///
    /// 사용 예:
    /// - 상점 NPC 프리팹에 MapMarker 추가
    /// - 병변 포탈 오브젝트에 MapMarker 추가
    /// - 특별 상자, 이벤트 오브젝트에 MapMarker 추가
    /// </summary>
    public class MapMarker : MonoBehaviour
    {
        [Header("Marker Info")]
        [SerializeField] private string displayName = "Marker";
        [SerializeField] private MapMarkerKind markerKind = MapMarkerKind.Object;

        [Header("Visibility")]
        [Tooltip("미니맵에 표시할지 여부")]
        [SerializeField] private bool showOnMiniMap = true;

        [Tooltip("전체 지도에 표시할지 여부")]
        [SerializeField] private bool showOnFullMap = true;

        [Tooltip("true면 플레이어가 근처를 지나가거나 해당 지역을 탐험한 뒤에만 표시됩니다.")]
        [SerializeField] private bool hideUntilDiscovered = true;

        [Tooltip("true면 탐험 여부와 관계없이 항상 표시됩니다. 보스나 특수 목표에 사용할 수 있습니다.")]
        [SerializeField] private bool alwaysVisible = false;

        [Tooltip("플레이어가 이 거리 안에 들어오면 발견 처리됩니다.")]
        [SerializeField] private float discoverDistance = 5f;

        [Header("Mini Map Style")]
        [SerializeField] private Color miniMapColor = Color.yellow;
        [SerializeField] private float miniMapSize = 6f;

        [Header("Full Map Style")]
        [SerializeField] private Color fullMapColor = Color.yellow;
        [SerializeField] private float fullMapSize = 8f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public MapMarkerKind MarkerKind => markerKind;

        public bool ShowOnMiniMap => showOnMiniMap;
        public bool ShowOnFullMap => showOnFullMap;
        public bool HideUntilDiscovered => hideUntilDiscovered;
        public bool AlwaysVisible => alwaysVisible;
        public float DiscoverDistance => Mathf.Max(0.1f, discoverDistance);

        public Color MiniMapColor => miniMapColor;
        public float MiniMapSize => Mathf.Max(1f, miniMapSize);

        public Color FullMapColor => fullMapColor;
        public float FullMapSize => Mathf.Max(1f, fullMapSize);

        public bool IsDiscovered { get; private set; }

        public Sprite Icon => OctoberMapIcons.For(this);
        public void Configure(MapMarkerKind kind,string label,bool global=false)
        {
            markerKind=kind;displayName=label;alwaysVisible=global;hideUntilDiscovered=!global;
            miniMapSize=12;fullMapSize=16;miniMapColor=fullMapColor=Color.white;
        }

        private void OnEnable()
        {
            if (ExplorationMapSystem.Instance != null)
            {
                ExplorationMapSystem.Instance.RegisterMarker(this);
            }
        }

        private void OnDisable()
        {
            if (ExplorationMapSystem.Instance != null)
            {
                ExplorationMapSystem.Instance.UnregisterMarker(this);
            }
        }

        public void SetDiscovered(bool discovered)
        {
            IsDiscovered = discovered;
        }

        public void ForceDiscover()
        {
            IsDiscovered = true;
        }

        public void ResetDiscovery()
        {
            IsDiscovered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = gameObject.name;
            }

            discoverDistance = Mathf.Max(0.1f, discoverDistance);
            miniMapSize = Mathf.Max(1f, miniMapSize);
            fullMapSize = Mathf.Max(1f, fullMapSize);
        }
#endif
    }
}
