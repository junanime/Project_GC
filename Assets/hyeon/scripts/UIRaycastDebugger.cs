using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastDebugger : MonoBehaviour
{
    private void Update()
    {
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if (mouse==null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current == null)
        {
            Debug.LogError("[UI Debug] EventSystem 없음");
            return;
        }

        PointerEventData data =
            new PointerEventData(EventSystem.current);

        data.position = mouse.position.ReadValue();

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(data, results);

        Debug.Log(
            $"[UI Debug] 클릭 위치 Raycast 수: {results.Count}"
        );

        for (int i = 0; i < results.Count; i++)
        {
            Debug.Log(
                $"[UI Debug] {i} : " +
                results[i].gameObject.name
            );
        }
    }
}
