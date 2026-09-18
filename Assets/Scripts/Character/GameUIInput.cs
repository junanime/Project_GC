using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Vampire
{
    // Existing scenes retain their serialized EventSystem/navigation settings.
    // Only the legacy mouse/touch reader is replaced, before the first UI update.
    public static class GameUIInput
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var system in root.GetComponentsInChildren<EventSystem>(true))
                    UseInputSystem(system);
        }

        public static void UseInputSystem(EventSystem system)
        {
            if (system == null) return;
            var legacy = system.GetComponent<StandaloneInputModule>();
            if (legacy == null) return;
            legacy.enabled = false;
            var input = system.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = system.gameObject.AddComponent<InputSystemUIInputModule>();
            if (input.actionsAsset == null) input.AssignDefaultActions();
            input.enabled = true;
            Object.Destroy(legacy);
        }
    }
}
