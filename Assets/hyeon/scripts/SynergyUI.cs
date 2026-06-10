using TMPro;
using UnityEngine;

namespace Vampire
{
    public class SynergyUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text synergyText;

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (synergyText == null) return;
            if (SynergyManager.Instance == null)
            {
                synergyText.text = "";
                return;
            }

            string text = "";

            foreach (string synergyName in SynergyManager.Instance.ActiveSynergyNames)
            {
                text += synergyName + "\n";
            }

            synergyText.text = text;
        }
    }
}