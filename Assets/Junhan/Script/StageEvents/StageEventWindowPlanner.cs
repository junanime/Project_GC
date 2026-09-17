using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public class StageEventWindowTrack
    {
        public bool enabled = true;
        [Min(1f), Tooltip("각 구간 안에서 한 번 발생합니다. 이벤트 종료 후 대기 시간이 아닙니다.")]
        public float windowSeconds = 120f;
    }

    public sealed class StageEventTemplate
    {
        public string kind;
        public float duration;
        public Action<float> schedule;
        public static T Copy<T>(T source) => JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
    }

    public static class StageEventWindowPlanner
    {
        public struct Slot { public float time; public float windowEnd; public int track; }

        public static List<Slot> Build(float duration, StageEventWindowTrack[] tracks, Func<float> random)
        {
            var slots = new List<Slot>();
            for (int i = 0; i < tracks.Length; i++)
            {
                var track = tracks[i];
                if (track == null || !track.enabled) continue;
                float width = Mathf.Max(1f, track.windowSeconds);
                for (float start = 0f; start < duration; start += width)
                {
                    float end = Mathf.Min(duration, start + width);
                    slots.Add(new Slot { time = Mathf.Lerp(start, end, Mathf.Clamp(random(), 0f, 0.999999f)), windowEnd = end, track = i });
                }
            }
            slots.Sort((a, b) => a.time.CompareTo(b.time));
            return slots;
        }
    }
}
