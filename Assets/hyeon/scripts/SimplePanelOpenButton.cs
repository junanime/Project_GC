using UnityEngine;

namespace Vampire
{
    public class SimplePanelOpenButton : MonoBehaviour
    {
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private GameObject[] panelsToClose;

        public void Open()
        {
            for (int i = 0; i < panelsToClose.Length; i++)
            {
                if (panelsToClose[i] != null)
                {
                    panelsToClose[i].SetActive(false);
                }
            }

            if (targetPanel != null)
            {
                targetPanel.SetActive(true);
            }
        }

        public void Close()
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }
        }
    }
}