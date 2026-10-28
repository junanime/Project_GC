using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Nonlethal run-ending poses. One calibrated scale is used for the entire clip;
    // wings, flags and ice must never shrink the underlying character to fit a frame.
    [DefaultExecutionOrder(1000)]
    public sealed class CharacterExitSequence : MonoBehaviour
    {
        [Serializable]
        sealed class ClipData
        {
            public int cellSize, columns, frameCount;
            public float pivotX, pivotY, referenceHeight, endHold;
            public int[] order;
            public float[] durations;
            public bool hideAtEnd;
        }
        sealed class Clip { public ClipData data; public Sprite[] frames; }
        static readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        SpriteRenderer original, visual, shadow;
        Color shadowColor;
        Vector3 shadowScale;
        Vector3 anchor;
        bool flying, vanishing;
        public bool IsPlaying { get; private set; }
        public bool Completed { get; private set; }
        public int FrameIndex { get; private set; }
        public float Elapsed { get; private set; }
        public float BodyHeight { get; private set; }
        public SpriteRenderer Visual => visual;

        public static string ClipName(Character actor)
        {
            var skill = actor.Blueprint != null ? actor.Blueprint.skills : null;
            return skill == null ? "ashi" : skill.kind.ToString().ToLowerInvariant();
        }

        static Clip Load(string name)
        {
            if (clips.TryGetValue(name, out var clip) && clip.frames[0] != null) return clip;
            var texture = Resources.Load<Texture2D>("CharacterExits/" + name);
            var json = Resources.Load<TextAsset>("CharacterExits/" + name);
            if (texture == null || json == null) return null;
            var data = JsonUtility.FromJson<ClipData>(json.text);
            if (data == null || data.order == null || data.durations == null ||
                data.order.Length != data.durations.Length || data.referenceHeight <= 0) return null;
            clip = new Clip { data = data, frames = new Sprite[data.frameCount] };
            for (int i = 0; i < data.frameCount; i++)
            {
                var rect = new Rect(i % data.columns * data.cellSize,
                    texture.height - (i / data.columns + 1) * data.cellSize, data.cellSize, data.cellSize);
                clip.frames[i] = Sprite.Create(texture, rect, new Vector2(data.pivotX, data.pivotY), 100, 0, SpriteMeshType.FullRect);
                clip.frames[i].name = name + " exit " + i;
            }
            clips[name] = clip;
            return clip;
        }

        public static Sprite ResultPortrait(CharacterBlueprint blueprint)
        {
            string name = blueprint?.skills != null ? blueprint.skills.kind.ToString().ToLowerInvariant() : "ashi";
            var clip = Load(name);
            if (clip == null) return null;
            // Keep the result illustration consistent with the nonlethal field exit.
            // Flying/vanishing characters use a readable frame before disappearing.
            int index = name == "ari" ? 9 : name == "hyuki" ? 15 : name == "shini" ? 8 : 5;
            return clip.frames[Mathf.Min(index, clip.frames.Length - 1)];
        }

        public static Bounds PaintedBounds(Sprite sprite)
        {
            var vertices = sprite.vertices;
            if (vertices.Length == 0) return sprite.bounds;
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            return bounds;
        }

        public IEnumerator Play(Character actor, SpriteRenderer body)
        {
            IsPlaying = true; Completed = false; Elapsed = 0; original = body;
            string name = ClipName(actor);
            flying = name == "ari";
            vanishing = flying || name == "shini";
            shadow = actor.transform.Find("Circle")?.GetComponent<SpriteRenderer>();
            if (shadow != null) { shadowColor = shadow.color; shadowScale = shadow.transform.localScale; }
            var clip = Load(name);
            if (clip == null || body == null)
            {
                Debug.LogWarning("Character exit art unavailable for " + ClipName(actor));
                // A future character with no clip still reaches the result panel without a dissolve.
                yield return new WaitForSecondsRealtime(.5f);
            }
            else
            {
                var idle = actor.Blueprint.idleSpriteSequence;
                var reference = idle != null && idle.Length > 0 ? idle[0] : body.sprite;
                var bounds = PaintedBounds(reference);
                if(name=="hyuki")
                {
                    var visible=VisibleBodyGeometry.Bounds(reference);
                    bounds=new Bounds(visible.center,visible.size);
                }
                BodyHeight = bounds.size.y * Mathf.Abs(body.transform.lossyScale.y);
                anchor = body.transform.TransformPoint(new Vector3(0, bounds.min.y, 0));
                visual = new GameObject("Character run exit").AddComponent<SpriteRenderer>();
                visual.transform.SetParent(transform, false);
                visual.transform.position = anchor;
                float worldScale = BodyHeight / clip.data.referenceHeight;
                visual.transform.localScale = new Vector3(worldScale / Mathf.Abs(transform.lossyScale.x), worldScale / Mathf.Abs(transform.lossyScale.y), 1);
                visual.sortingLayerID = body.sortingLayerID;
                visual.sortingOrder = body.sortingOrder + 1;
                visual.sharedMaterial = body.sharedMaterial;
                visual.color = Color.white;
                visual.flipX = actor.LookDirection.x < 0;
                body.enabled = false;
                for (int i = 0; i < clip.data.order.Length; i++)
                {
                    FrameIndex = clip.data.order[i];
                    visual.sprite = clip.frames[FrameIndex];
                    for (float t = 0; t < clip.data.durations[i];)
                    {
                        yield return null;
                        float dt = Time.unscaledDeltaTime;
                        t += dt; Elapsed += dt;
                    }
                }
                if (clip.data.hideAtEnd)
                {
                    visual.enabled = false;
                    if (shadow != null) shadow.enabled = false;
                }
                yield return new WaitForSecondsRealtime(clip.data.endHold);
            }
            Completed = true;
            IsPlaying = false;
        }

        void LateUpdate()
        {
            if (original != null) original.enabled = false;
            if (vanishing && shadow != null)
            {
                float fade = Mathf.Clamp01((Elapsed - 1f) / .85f);
                shadow.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * (1 - fade));
                if (flying) shadow.transform.localScale = shadowScale * (1 - .6f * fade);
            }
            if (visual != null)
            {
                float lift = flying ? Mathf.Pow(Mathf.Clamp01((Elapsed - 1f) / 1.1f), 1.6f) * BodyHeight * 4 : 0;
                visual.transform.position = anchor + Vector3.up * lift;
            }
            // A focus/pause callback cannot resume combat while this unscaled clip is playing.
            if (IsPlaying) Time.timeScale = 0;
        }
    }
}
