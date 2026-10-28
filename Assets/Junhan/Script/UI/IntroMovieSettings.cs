using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(menuName = "24 Hours/Intro Movie")]
    public sealed class IntroMovieSettings : ScriptableObject
    {
        public bool playOnLaunch = true;
        [Tooltip("Path relative to Assets/StreamingAssets. Replace this file to update the intro without editing scenes.")]
        public string relativePath = "Intro/intro.mp4";
        [Range(0, 1)] public float volume = 1;
        [Min(5)] public float timeoutSeconds = 25;
    }
}
