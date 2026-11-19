using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Tests.Editor
{
    public static class PreparationSkillMovieSmoke
    {
        public static IEnumerator CheckAll()
        {
            const string output = "Library/GStarUIQA/skills";
            Directory.CreateDirectory(output);
            var ui = ApothecaryUI.Instance;
            ui.Show("prepare", 0);
            yield return null;
            var characters = ui.Config.characters;
            int original = Array.IndexOf(characters, ui.SelectedCharacter);
            for (int i = 0; i < characters.Length; i++)
            {
                Select(ui, i);
                yield return null;
                string key = OctoberArt.CharacterKey(characters[i]);
                var preview = ui.GetComponentsInChildren<PreparationSkillMovie>().Single();
                var image = preview.GetComponent<RawImage>();
                Require(image.texture == Resources.Load<Texture2D>("GStarDemo/SkillFilms/" + key + "/" + preview.Frame / 49), key + " selected film sheet");
                Require(preview.GetComponent<AudioSource>() == null, key + " silent loop");
                Canvas.ForceUpdateCanvases();
                var description = ui.GetComponentsInChildren<TextMeshProUGUI>().Single(text => text.name == "Selection description");
                var button = ui.GetComponentsInChildren<Button>().Single(item => item.name == "Selection action");
                var screenRect = Bounds(image.rectTransform);
                Require(!screenRect.Overlaps(Bounds(description.rectTransform)) && !screenRect.Overlaps(Bounds((RectTransform)button.transform)), key + " movie clear of description and input");
                GStarUISmoke.Capture("skills/" + key, 1280, 720);
                var renderedFrames = new HashSet<uint>();
                for (int frame = 0; frame < 8; frame++)
                {
                    renderedFrames.Add(VisiblePixels(image));
                    GStarUISmoke.Capture($"skills/{i}-{frame:00}", 960, 540);
                    yield return new WaitForSecondsRealtime(.15f);
                }
                Require(renderedFrames.Count >= 3, key + " displays different footage pixels");
                int initialLoops = preview.Loops;
                float deadline = Time.realtimeSinceStartup + 10f;
                while (preview.Loops == initialLoops && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Require(preview.Loops > initialLoops, key + " loop restarted");
            }
            ui.Show("prepare", 1);
            yield return null;
            Require(ui.GetComponentInChildren<PreparationSkillMovie>() == null, "movie stops on another tab");
            ui.Show("prepare", 0);
            Select(ui, Mathf.Max(0, original));
            yield return null;
        }

        static void Select(ApothecaryUI ui, int index)
        {
            ui.GetComponentsInChildren<Button>().Single(button => button.name == "Choice 0 " + index).onClick.Invoke();
        }

        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static uint VisiblePixels(RawImage image)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(32, 18, 0);
            var sample = new Texture2D(32, 18, TextureFormat.RGB24, false);
            try
            {
                var uv = image.uvRect;
                Graphics.Blit(image.texture, target, uv.size, uv.position);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, 32, 18), 0, 0);
                uint hash = 2166136261;
                foreach (byte channel in sample.GetRawTextureData<byte>())
                    hash = unchecked((hash ^ channel) * 16777619);
                return hash;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(sample);
            }
        }

        static void Require(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("Skill preview: " + message);
        }
    }
}
