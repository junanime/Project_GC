using UnityEngine;
using TMPro;

namespace Vampire
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class GameTimer : MonoBehaviour
    {
        private TextMeshProUGUI timerText;

        int lastSeconds = -1, lastBoost = -1;

        void Awake()
        {
            timerText = GetComponent<TextMeshProUGUI>();
        }

        public void SetTime(float t, float hourglassRemaining = 0f)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, t));
            int boost = Mathf.CeilToInt(hourglassRemaining);
            if (seconds == lastSeconds && boost == lastBoost) return;
            lastSeconds = seconds;
            lastBoost = boost;
            string clock = System.TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            timerText.text = boost > 0 ? $"{clock}  <size=70%><color=#FFCB56>×2 · {boost}s</color></size>" : clock;
        }
    }
}
