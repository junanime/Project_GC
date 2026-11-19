using UnityEngine;

namespace Vampire
{
    internal static class GStarImpactAudio
    {
        static readonly string[] sounds = { "CandyHit", "CandyBreak", "GummyHit", "GummyPop", "GroupFinish" };
        static readonly AudioClip[] clips = new AudioClip[sounds.Length];
        static readonly float[] nextHit = new float[4];
        static float windowStart, nextAccent;
        static int kills;
        static void Play(int index, float gain)
        {
            if (clips[index] == null) clips[index] = Resources.Load<AudioClip>("GStarImpact/" + sounds[index]);
            GameAudioManager.PlayImpactClip(clips[index], gain);
        }

        public static void MaterialHit(bool gummy, bool lethal)
        {
            int index = (gummy ? 2 : 0) + (lethal ? 1 : 0);
            if (Time.unscaledTime < nextHit[index]) return;
            nextHit[index] = Time.unscaledTime + (lethal ? .08f : .10f);
            Play(index, lethal ? .58f : .42f);
        }

        public static void FieldKill()
        {
            float now = Time.unscaledTime;
            if (now < nextAccent) return;
            if (kills == 0 || now - windowStart > .38f) { windowStart = now; kills = 0; }
            if (++kills < 3) return;
            kills = 0; nextAccent = now + .65f;
            Play(4, .48f);
        }
    }
}
