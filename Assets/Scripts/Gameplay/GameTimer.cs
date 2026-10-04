using UnityEngine;
using TMPro;

namespace Vampire
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class GameTimer : MonoBehaviour
    {
        private TextMeshProUGUI timerText;

        void Awake()
        {
            timerText = GetComponent<TextMeshProUGUI>();
        }

        public void SetTime(float t)
        {
            System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(Mathf.CeilToInt(Mathf.Max(0,t)));
            timerText.text = timeSpan.ToString(@"mm\:ss");
        }
    }
}
