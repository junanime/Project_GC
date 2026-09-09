using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire.EditorTools
{
    public static class ProjectGCMissingScriptScanner
    {
        [MenuItem(
            "Tools/Project GC/Validation/Scan Missing Scripts In Active Scene")]
        private static void ScanMissingScriptsInActiveScene()
        {
            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!activeScene.IsValid())
            {
                Debug.LogWarning(
                    "[ProjectGC Validation] " +
                    "현재 활성 Scene을 찾을 수 없습니다.");

                return;
            }

            GameObject[] rootObjects =
                activeScene.GetRootGameObjects();

            int objectCount = 0;
            int totalMissingCount = 0;

            for (int rootIndex = 0;
                 rootIndex < rootObjects.Length;
                 rootIndex++)
            {
                GameObject rootObject =
                    rootObjects[rootIndex];

                if (rootObject == null)
                {
                    continue;
                }

                Transform[] transforms =
                    rootObject.GetComponentsInChildren
                    <
                        Transform
                    >(true);

                for (int i = 0;
                     i < transforms.Length;
                     i++)
                {
                    Transform targetTransform =
                        transforms[i];

                    if (targetTransform == null)
                    {
                        continue;
                    }

                    GameObject targetObject =
                        targetTransform.gameObject;

                    int missingCount =
                        GameObjectUtility
                            .GetMonoBehavioursWithMissingScriptCount(
                                targetObject);

                    if (missingCount <= 0)
                    {
                        continue;
                    }

                    objectCount++;
                    totalMissingCount +=
                        missingCount;

                    string hierarchyPath =
                        GetHierarchyPath(
                            targetTransform);

                    Debug.LogError(
                        $"[ProjectGC Missing Script] " +
                        $"Object={hierarchyPath} | " +
                        $"MissingCount={missingCount}",
                        targetObject);
                }
            }

            if (totalMissingCount <= 0)
            {
                Debug.Log(
                    $"[ProjectGC Validation] " +
                    $"Missing Script 없음 | " +
                    $"Scene={activeScene.name}");

                return;
            }

            Debug.LogWarning(
                $"[ProjectGC Validation] " +
                $"Missing Script 검색 완료 | " +
                $"Scene={activeScene.name}, " +
                $"Objects={objectCount}, " +
                $"MissingScripts={totalMissingCount}");
        }


        private static string GetHierarchyPath(
            Transform target)
        {
            if (target == null)
            {
                return "(null)";
            }

            string path =
                target.name;

            Transform current =
                target.parent;

            while (current != null)
            {
                path =
                    current.name +
                    "/" +
                    path;

                current =
                    current.parent;
            }

            return path;
        }
    }
}