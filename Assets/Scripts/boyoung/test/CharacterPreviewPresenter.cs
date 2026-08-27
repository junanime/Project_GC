using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vampire;

namespace Vampire
{
    public class CharacterPreviewPresenter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image image2D;
    [SerializeField] private RawImage rawImage3D;

    [Header("3D Preview")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform modelRoot;

    private RenderTexture renderTexture;
    private GameObject currentModel;

    private const int RenderTextureSize = 512;

    public RectTransform RectTransform
    {
        get { return transform as RectTransform; }
    }

    private void Awake()
    {
        CreateRenderTexture();
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private void CreateRenderTexture()
    {
        if (previewCamera == null || rawImage3D == null)
            return;

        renderTexture = new RenderTexture(
            RenderTextureSize,
            RenderTextureSize,
            24,
            RenderTextureFormat.ARGB32
        );

        renderTexture.name = gameObject.name + "_RuntimeRT";
        renderTexture.Create();

        previewCamera.targetTexture = renderTexture;
        rawImage3D.texture = renderTexture;
    }

    public void ShowCharacter(CharacterSelectionCarousel.CharacterData data)
    {
        ClearModel();

        if (data.displayType ==
            CharacterSelectionCarousel.CharacterDisplayType.Sprite2D)
        {
            Show2D(data);
        }
        else
        {
            Show3D(data);
        }
    }

    private void Show2D(
        CharacterSelectionCarousel.CharacterData data)
    {
        if (image2D != null)
        {
            image2D.gameObject.SetActive(true);
            image2D.sprite = data.characterSprite;
            image2D.preserveAspect = true;
        }

        if (rawImage3D != null)
        {
            rawImage3D.gameObject.SetActive(false);
        }

        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }
    }

    private void Show3D(
        CharacterSelectionCarousel.CharacterData data)
    {
        if (image2D != null)
        {
            image2D.gameObject.SetActive(false);
        }

        if (rawImage3D != null)
        {
            rawImage3D.gameObject.SetActive(true);
        }

        if (previewCamera != null)
        {
            previewCamera.enabled = true;
        }

        if (data.characterModelPrefab == null)
        {
            Debug.LogWarning(
                data.characterName +
                "의 Character Model Prefab이 없습니다."
            );

            return;
        }

        currentModel = Instantiate(
            data.characterModelPrefab,
            modelRoot
        );

        currentModel.name =
            data.characterName + "_PreviewModel";

        currentModel.transform.localPosition =
            data.previewPosition;

        currentModel.transform.localEulerAngles =
            data.previewRotation;

        Vector3 scale = data.previewScale;

        if (scale == Vector3.zero)
        {
            scale = Vector3.one;
        }

        currentModel.transform.localScale = scale;

        int previewLayer =
            LayerMask.NameToLayer("CharacterPreview");

        if (previewLayer >= 0)
        {
            SetLayerRecursively(
                currentModel,
                previewLayer
            );
        }

        FitCameraToModel();
    }

    private void FitCameraToModel()
    {
        if (currentModel == null ||
            previewCamera == null)
        {
            return;
        }

        Renderer[] renderers =
            currentModel.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning(
                currentModel.name +
                "에서 Renderer를 찾지 못했습니다."
            );

            return;
        }

        Bounds bounds =
            renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(
                renderers[i].bounds
            );
        }

        Vector3 center = bounds.center;

        float radius =
            Mathf.Max(
                bounds.extents.x,
                bounds.extents.y,
                bounds.extents.z
            );

        radius = Mathf.Max(radius, 0.1f);

        float halfFov =
            previewCamera.fieldOfView *
            0.5f *
            Mathf.Deg2Rad;

        float distance =
            radius / Mathf.Tan(halfFov);

        distance *= 1.8f;

        previewCamera.transform.position =
            center +
            new Vector3(
                0f,
                radius * 0.05f,
                -distance
            );

        previewCamera.transform.LookAt(center);

        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane =
            Mathf.Max(100f, distance * 5f);
    }

    private void ClearModel()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }
    }

    private void SetLayerRecursively(
        GameObject obj,
        int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(
                child.gameObject,
                layer
            );
        }
    }
}
}
