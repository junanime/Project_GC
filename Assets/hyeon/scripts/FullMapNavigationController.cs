using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire
{
    public class FullMapNavigationController : MonoBehaviour
    {
        // ========================================
        // References
        // ========================================

        [Header("References")]

        [Tooltip("마우스 입력을 받을 지도 영역")]
        [SerializeField]
        private RectTransform mapArea;

        [Tooltip("실제 전체 지도 RawImage")]
        [SerializeField]
        private RawImage fullMapImage;

        [Tooltip("전체 지도 플레이어/마커 위치를 갱신할 지도 시스템")]
        [SerializeField]
        private ExplorationMapSystem mapSystem;

        [Tooltip("전체 지도의 확대/축소 상태를 관리하는 컨트롤러")]
        [SerializeField]
        private TabMapResizeController resizeController;


        // ========================================
        // Pan
        // ========================================

        [Header("Pan")]

        [Tooltip("Alt + 좌클릭 이동 속도")]
        [SerializeField]
        private float panSpeed = 1f;


        // ========================================
        // Zoom
        // ========================================

        [Header("Zoom")]

        [Tooltip("최소 확대 배율")]
        [SerializeField]
        private float minZoom = 1f;

        [Tooltip("최대 확대 배율")]
        [SerializeField]
        private float maxZoom = 4f;

        [Tooltip("휠 한 번당 확대/축소 비율")]
        [SerializeField]
        private float zoomStep = 0.15f;


        // ========================================
        // Expanded Start View
        // ========================================

        [Header("Expanded Start View")]

        [Tooltip(
            "+ 버튼으로 전체 지도를 확대했을 때 처음 보여줄 UV 범위입니다. " +
            "1이면 지도 전체, 0.7이면 전체 지도의 70%를 보여줘서 " +
            "휠을 돌리지 않아도 바로 드래그할 수 있습니다."
        )]
        [Range(0.25f, 1f)]
        [SerializeField]
        private float expandedStartUvSize = 0.70f;


        // ========================================
        // Options
        // ========================================

        [Header("Options")]

        [Tooltip("전체 지도를 축소할 때 UV 위치와 확대 배율을 초기화")]
        [SerializeField]
        private bool resetWhenShrunk = true;


        // ========================================
        // Debug
        // ========================================

        [Header("Debug")]

        [SerializeField]
        private bool debugLog = true;


        // ========================================
        // Runtime
        // ========================================

        private bool isDragging;

        private bool previousExpandedState;

        private Vector2 previousMousePosition;

        private float currentZoom = 1f;

        private Canvas parentCanvas;

        private Camera uiCamera;


        // ========================================
        // Unity
        // ========================================

        private void Awake()
        {
            if (mapArea == null)
            {
                mapArea =
                    transform as RectTransform;
            }


            if (mapSystem == null)
            {
                mapSystem =
                    FindObjectOfType<ExplorationMapSystem>();
            }


            currentZoom =
                minZoom;


            RefreshCanvasReference();


            if (resizeController != null)
            {
                previousExpandedState =
                    resizeController.IsExpanded;
            }


            // 게임 시작 시에는 전체 지도 상태로 초기화
            ResetView();


            if (debugLog)
            {
                Debug.Log(
                    "[FullMapNav] UV Navigation Ready"
                );
            }
        }


        private void OnEnable()
        {
            isDragging = false;

            RefreshCanvasReference();


            if (resizeController != null)
            {
                previousExpandedState =
                    resizeController.IsExpanded;
            }
        }


        private void Update()
        {
            // ========================================
            // Reference 검사
            // ========================================

            if (mapArea == null ||
                fullMapImage == null ||
                resizeController == null)
            {
                return;
            }


            if (Mouse.current == null ||
                Keyboard.current == null)
            {
                return;
            }


            RefreshCanvasReference();


            // ========================================
            // 확대 상태
            // ========================================

            bool expanded =
                resizeController.IsExpanded;


            // ========================================
            // 확대 → 축소
            // ========================================

            if (previousExpandedState &&
                !expanded)
            {
                isDragging = false;


                if (resetWhenShrunk)
                {
                    ResetView();
                }


                if (debugLog)
                {
                    Debug.Log(
                        "[FullMapNav] Map Shrunk"
                    );
                }
            }


            // ========================================
            // 축소 → 확대
            // ========================================

            if (!previousExpandedState &&
                expanded)
            {
                // ★ 핵심
                //
                // + 버튼으로 전체 지도를 확대하면
                // 처음부터 지도 일부만 보여준다.
                //
                // 따라서 휠 확대를 먼저 하지 않아도
                // Alt + 좌클릭으로 바로 Pan 가능.
                ApplyExpandedStartView();


                if (debugLog)
                {
                    Debug.Log(
                        "[FullMapNav] Map Expanded" +
                        $" | UV={fullMapImage.uvRect}" +
                        $" | Zoom={currentZoom:F2}"
                    );
                }
            }


            previousExpandedState =
                expanded;


            // ========================================
            // 확대 상태가 아니면 조작 금지
            // ========================================

            if (!expanded)
            {
                isDragging = false;
                return;
            }


            // ========================================
            // Mouse
            // ========================================

            Vector2 mousePosition =
                Mouse.current.position.ReadValue();


            // 이전 테스트에서 MapPanel 기준 판정이
            // 정상 작동했으므로 유지
            RectTransform interactionArea =
                resizeController.MapPanel;


            if (interactionArea == null)
            {
                interactionArea =
                    mapArea;
            }


            bool mouseInside =
                IsMouseInside(
                    interactionArea,
                    mousePosition
                );


            // ========================================
            // Alt
            // ========================================

            bool altPressed =
                Keyboard.current.leftAltKey.isPressed ||
                Keyboard.current.rightAltKey.isPressed;


            if (!altPressed)
            {
                isDragging = false;
                return;
            }


            // ========================================
            // Alt + Wheel
            // ========================================

            if (mouseInside)
            {
                float scroll =
                    Mouse.current.scroll.ReadValue().y;


                if (Mathf.Abs(scroll) >
                    0.01f)
                {
                    ZoomAtMouse(
                        mousePosition,
                        scroll
                    );
                }
            }


            // ========================================
            // Alt + Left Click 시작
            // ========================================

            if (Mouse.current.leftButton
                    .wasPressedThisFrame &&
                mouseInside)
            {
                previousMousePosition =
                    mousePosition;

                isDragging =
                    true;


                if (debugLog)
                {
                    Debug.Log(
                        "[FullMapNav] Drag Start"
                    );
                }
            }


            // ========================================
            // Alt + Left Click 드래그
            // ========================================

            if (isDragging)
            {
                if (!Mouse.current.leftButton
                        .isPressed)
                {
                    isDragging =
                        false;


                    if (debugLog)
                    {
                        Debug.Log(
                            "[FullMapNav] Drag End"
                        );
                    }


                    return;
                }


                Vector2 delta =
                    mousePosition -
                    previousMousePosition;


                PanMap(
                    delta
                );


                previousMousePosition =
                    mousePosition;
            }
        }


        // ========================================
        // Pan
        // ========================================

        private void PanMap(
            Vector2 screenDelta)
        {
            if (fullMapImage == null ||
                mapArea == null)
            {
                return;
            }


            Rect uv =
                fullMapImage.uvRect;


            float width =
                Mathf.Max(
                    1f,
                    mapArea.rect.width
                );


            float height =
                Mathf.Max(
                    1f,
                    mapArea.rect.height
                );


            // ========================================
            // 마우스를 오른쪽으로 끌면
            // 지도 내용도 오른쪽으로 따라오는 느낌.
            //
            // 따라서 UV 영역은 반대 방향으로 이동.
            // ========================================

            float uvDeltaX =
                screenDelta.x /
                width *
                uv.width *
                panSpeed;


            float uvDeltaY =
                screenDelta.y /
                height *
                uv.height *
                panSpeed;


            uv.x -=
                uvDeltaX;

            uv.y -=
                uvDeltaY;


            uv =
                ClampUvRect(
                    uv
                );


            fullMapImage.uvRect =
                uv;


            // 플레이어 / 마커 위치도
            // 현재 UV에 맞춰 즉시 갱신
            RefreshMarkers();
        }


        // ========================================
        // Zoom
        // ========================================

        private void ZoomAtMouse(
            Vector2 screenMousePosition,
            float scroll)
        {
            if (fullMapImage == null ||
                mapArea == null)
            {
                return;
            }


            if (!RectTransformUtility
                    .ScreenPointToLocalPointInRectangle(
                        mapArea,
                        screenMousePosition,
                        uiCamera,
                        out Vector2 localMouse
                    ))
            {
                return;
            }


            Rect areaRect =
                mapArea.rect;


            // ========================================
            // 현재 마우스 위치를 0~1 좌표로 변환
            // ========================================

            Vector2 mouseNormalized =
                new Vector2(
                    Mathf.InverseLerp(
                        areaRect.xMin,
                        areaRect.xMax,
                        localMouse.x
                    ),

                    Mathf.InverseLerp(
                        areaRect.yMin,
                        areaRect.yMax,
                        localMouse.y
                    )
                );


            Rect oldUv =
                fullMapImage.uvRect;


            float oldZoom =
                currentZoom;


            // ========================================
            // Zoom 배율 변경
            // ========================================

            if (scroll > 0f)
            {
                currentZoom *=
                    1f + zoomStep;
            }
            else
            {
                currentZoom /=
                    1f + zoomStep;
            }


            currentZoom =
                Mathf.Clamp(
                    currentZoom,
                    minZoom,
                    maxZoom
                );


            if (Mathf.Approximately(
                currentZoom,
                oldZoom))
            {
                return;
            }


            // ========================================
            // Zoom 배율 → UV 크기
            //
            // 1배 = 1.0
            // 2배 = 0.5
            // 4배 = 0.25
            // ========================================

            float newUvSize =
                1f /
                currentZoom;


            // ========================================
            // 확대하기 전에
            // 마우스 커서 아래 있던 실제 Texture 좌표
            // ========================================

            Vector2 mapPointUnderMouse =
                new Vector2(
                    oldUv.x +
                    mouseNormalized.x *
                    oldUv.width,

                    oldUv.y +
                    mouseNormalized.y *
                    oldUv.height
                );


            Rect newUv =
                new Rect(
                    0f,
                    0f,
                    newUvSize,
                    newUvSize
                );


            // ========================================
            // 확대 후에도 같은 지도 좌표가
            // 커서 아래에 있도록 보정
            // ========================================

            newUv.x =
                mapPointUnderMouse.x -
                mouseNormalized.x *
                newUv.width;


            newUv.y =
                mapPointUnderMouse.y -
                mouseNormalized.y *
                newUv.height;


            newUv =
                ClampUvRect(
                    newUv
                );


            fullMapImage.uvRect =
                newUv;


            RefreshMarkers();


            if (debugLog)
            {
                Debug.Log(
                    $"[FullMapNav] Zoom = {currentZoom:F2}" +
                    $" | UV={fullMapImage.uvRect}"
                );
            }
        }


        // ========================================
        // Expanded Start View
        // ========================================

        private void ApplyExpandedStartView()
        {
            if (fullMapImage == null)
            {
                return;
            }


            // ========================================
            // 예:
            //
            // expandedStartUvSize = 0.70
            //
            // Texture 전체의 70% 영역을 보여준다.
            // ========================================

            float requestedUvSize =
                Mathf.Clamp(
                    expandedStartUvSize,
                    0.25f,
                    1f
                );


            // ========================================
            // UV 크기 → Zoom 배율
            //
            // 0.70 → 약 1.428배
            // ========================================

            float requestedZoom =
                1f /
                requestedUvSize;


            currentZoom =
                Mathf.Clamp(
                    requestedZoom,
                    minZoom,
                    maxZoom
                );


            // 실제 Clamp된 Zoom 기준으로
            // UV 크기를 다시 계산
            float actualUvSize =
                1f /
                Mathf.Max(
                    0.0001f,
                    currentZoom
                );


            // ========================================
            // 지도 중앙에서 시작
            //
            // UV 0.70이면:
            //
            // X = 0.15
            // Y = 0.15
            // W = 0.70
            // H = 0.70
            // ========================================

            float start =
                (1f - actualUvSize) *
                0.5f;


            Rect uv =
                new Rect(
                    start,
                    start,
                    actualUvSize,
                    actualUvSize
                );


            fullMapImage.uvRect =
                ClampUvRect(
                    uv
                );


            RefreshMarkers();
        }


        // ========================================
        // UV Clamp
        // ========================================

        private Rect ClampUvRect(
            Rect uv)
        {
            // ========================================
            // UV 크기 제한
            // ========================================

            uv.width =
                Mathf.Clamp01(
                    uv.width
                );


            uv.height =
                Mathf.Clamp01(
                    uv.height
                );


            // ========================================
            // Texture 바깥으로 나가지 못하도록 제한
            // ========================================

            uv.x =
                Mathf.Clamp(
                    uv.x,
                    0f,
                    1f - uv.width
                );


            uv.y =
                Mathf.Clamp(
                    uv.y,
                    0f,
                    1f - uv.height
                );


            return uv;
        }


        // ========================================
        // Mouse Inside
        // ========================================

        private bool IsMouseInside(
            RectTransform area,
            Vector2 screenPosition)
        {
            if (area == null)
            {
                return false;
            }


            if (!RectTransformUtility
                    .ScreenPointToLocalPointInRectangle(
                        area,
                        screenPosition,
                        uiCamera,
                        out Vector2 localPoint
                    ))
            {
                return false;
            }


            return area.rect.Contains(
                localPoint
            );
        }


        // ========================================
        // Marker 즉시 갱신
        // ========================================

        private void RefreshMarkers()
        {
            if (mapSystem != null)
            {
                mapSystem.RefreshFullMapView();
            }
        }


        // ========================================
        // Reset
        // ========================================

        public void ResetView()
        {
            currentZoom =
                minZoom;


            if (fullMapImage != null)
            {
                float uvSize =
                    1f /
                    Mathf.Max(
                        0.0001f,
                        currentZoom
                    );


                float start =
                    (1f - uvSize) *
                    0.5f;


                fullMapImage.uvRect =
                    ClampUvRect(
                        new Rect(
                            start,
                            start,
                            uvSize,
                            uvSize
                        )
                    );
            }


            RefreshMarkers();


            if (debugLog)
            {
                Debug.Log(
                    "[FullMapNav] View Reset"
                );
            }
        }


        // ========================================
        // Canvas
        // ========================================

        private void RefreshCanvasReference()
        {
            if (mapArea == null)
            {
                return;
            }


            parentCanvas =
                mapArea.GetComponentInParent<Canvas>();


            if (parentCanvas == null)
            {
                uiCamera =
                    null;

                return;
            }


            if (parentCanvas.renderMode ==
                RenderMode.ScreenSpaceOverlay)
            {
                uiCamera =
                    null;
            }
            else
            {
                uiCamera =
                    parentCanvas.worldCamera;
            }
        }


        // ========================================
        // Disable
        // ========================================

        private void OnDisable()
        {
            isDragging =
                false;
        }


#if UNITY_EDITOR

        // ========================================
        // Inspector 값 보호
        // ========================================

        private void OnValidate()
        {
            panSpeed =
                Mathf.Max(
                    0.01f,
                    panSpeed
                );


            minZoom =
                Mathf.Max(
                    1f,
                    minZoom
                );


            maxZoom =
                Mathf.Max(
                    minZoom,
                    maxZoom
                );


            zoomStep =
                Mathf.Clamp(
                    zoomStep,
                    0.01f,
                    1f
                );


            expandedStartUvSize =
                Mathf.Clamp(
                    expandedStartUvSize,
                    0.25f,
                    1f
                );
        }

#endif
    }
}