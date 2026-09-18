using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class EscapeToQuit : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        void Update()
        {
            if (Vampire.GameInput.GetKeyDown(KeyCode.Escape))
            {
                // Android's system Back is not an unconditional desktop quit.
                if (Application.isMobilePlatform)
                {
                    var map=FindObjectOfType<MapPanelToggle>();
                    if(map!=null && map.IsOpen()) { map.Toggle(); return; }
                    var selection=FindObjectOfType<AbilitySelectionDialog>();
                    if(selection!=null && selection.MenuOpen) return;
                    var pause=FindObjectOfType<PauseMenu>();
                    if(pause!=null) pause.PlayPause();
                    return;
                }
                Application.Quit();
            }
        }
    }
}
