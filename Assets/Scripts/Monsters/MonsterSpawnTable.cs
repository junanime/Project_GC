using UnityEngine;

namespace Vampire
{
    [System.Serializable]
    public class MonsterSpawnTable
    {
        public SpawnRateKeyframe[] spawnRateKeyframes;
        public SpawnChanceKeyframe[] spawnChanceKeyframes;

        public float GetSpawnRate(float t)
        {
            if (spawnRateKeyframes == null || spawnRateKeyframes.Length == 0) return 0f;
            var previous = spawnRateKeyframes[0];
            foreach (var next in spawnRateKeyframes)
            {
                if (next.t > t)
                    return Mathf.Max(0f, Mathf.Lerp(previous.spawnRate, next.spawnRate,
                        Mathf.InverseLerp(previous.t, next.t, t)));
                previous = next;
            }
            return Mathf.Max(0f, previous.spawnRate);
        }

        // Normalize Inspector weights; clamp endpoints and tolerate repeated timestamps.
        public int SelectMonster(float t) => SelectMonster(t, Random.value);
        public int SelectMonster(float t, float sample)
        {
            GetBracket(t, out var a, out var b, out float blend);
            if (a == null || b == null) return -1;
            int count = Mathf.Max(a.Length, b.Length);
            float total = 0f;
            for (int i = 0; i < count; i++) total += Weight(a, b, i, blend);
            if (total <= 0f) return -1;
            float remaining = Mathf.Clamp01(sample) * total;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                float weight = Weight(a, b, i, blend);
                if (weight <= 0f) continue;
                last = i;
                remaining -= weight;
                if (remaining < 0f) return i;
            }
            return last;
        }

        public float GetProbability(float t, int index)
        {
            GetBracket(t, out var a, out var b, out float blend);
            if (a == null || b == null || index < 0) return 0f;
            float total = 0f;
            for (int i = 0; i < Mathf.Max(a.Length, b.Length); i++) total += Weight(a, b, i, blend);
            return total > 0f ? Weight(a, b, index, blend) / total : 0f;
        }

        private void GetBracket(float t, out float[] a, out float[] b, out float blend)
        {
            a = b = null;
            blend = 0f;
            if (spawnChanceKeyframes == null || spawnChanceKeyframes.Length == 0) return;
            var previous = spawnChanceKeyframes[0];
            foreach (var next in spawnChanceKeyframes)
            {
                if (next.t > t)
                {
                    a = previous.spawnChances;
                    b = next.spawnChances;
                    blend = Mathf.InverseLerp(previous.t, next.t, t);
                    return;
                }
                previous = next;
            }
            a = b = previous.spawnChances;
        }

        private static float Weight(float[] a, float[] b, int i, float blend) =>
            Mathf.Lerp(i < a.Length ? Mathf.Max(0f, a[i]) : 0f,
                       i < b.Length ? Mathf.Max(0f, b[i]) : 0f, blend);

        [System.Serializable]
        public class SpawnRateKeyframe { public float t; public float spawnRate; }
        [System.Serializable]
        public class SpawnChanceKeyframe { public float t; public float[] spawnChances; }
    }
}
