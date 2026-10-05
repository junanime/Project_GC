using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Image pauseButton;
        [SerializeField] private Sprite pauseSprite, playSprite;
        [SerializeField] private GameObject pauseMenu;
        private bool paused = false;
        private bool timeIsFrozen = false;

        public bool TimeIsFrozen { set => timeIsFrozen = value; }

        public void RetireLegacyControls()
        {
            if(pauseMenu!=null)pauseMenu.SetActive(false);
            foreach(var button in FindObjectsOfType<Button>(true))
                for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
                    if(button.onClick.GetPersistentTarget(i)==this)button.gameObject.SetActive(false);
            if(pauseButton!=null)pauseButton.gameObject.SetActive(false);
        }

        public void PlayPause()
        {
            if(ApothecaryUI.Instance!=null){ApothecaryUI.Instance.Back();return;}
            if (ApothecaryUI.Instance != null && ApothecaryUI.Instance.Page == "skillReward") return;
            var skills = FindObjectOfType<CharacterSkillRuntime>();
            if (skills != null && skills.IsCutin) return;
            if (paused = !paused)
            {
                if (!timeIsFrozen)
                    Time.timeScale = 0;
                pauseButton.sprite = playSprite;
                pauseMenu.SetActive(true);
            }
            else
            {
                if (!timeIsFrozen)
                    Time.timeScale = 1;
                pauseButton.sprite = pauseSprite;
                pauseMenu.SetActive(false);
            }
        }
    }
}
