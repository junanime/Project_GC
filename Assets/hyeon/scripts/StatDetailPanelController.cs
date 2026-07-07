using UnityEngine;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire
{
    public class StatsDetailPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject statsDetailPanel;

        private void Awake()
        {
            if (statsDetailPanel != null)
            {
                statsDetailPanel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // TAB √¢¿ª ¥›¿∏∏È ªÛºº Ω∫≈» √¢µµ ∞∞¿Ã ¥›»˚
            if (statsDetailPanel != null)
            {
                statsDetailPanel.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (statsDetailPanel == null)
            {
                return;
            }

            statsDetailPanel.SetActive(!statsDetailPanel.activeSelf);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void Close()
        {
            if (statsDetailPanel != null)
            {
                statsDetailPanel.SetActive(false);
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}