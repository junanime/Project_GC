using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // The skill preview is 24 fps. Display its exact frames without depending on the PC's media decoder.
    public sealed class PreparationSkillMovie : MonoBehaviour
    {
        const int FrameRate = 24;
        const int Columns = 7;
        const int FramesPerSheet = Columns * Columns;
        readonly Texture2D[] sheets = new Texture2D[3];
        RawImage image;
        string characterKey;
        double started;
        public int Frame { get; private set; }
        public int Loops { get; private set; }

        public void Show(string key)
        {
            var aspect = gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 16f / 9f;
            image = gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            characterKey = key;
            StartFilm();
        }

        void StartFilm()
        {
            for (int i = 0; i < sheets.Length; i++)
                sheets[i] = Resources.Load<Texture2D>($"CombatPresentation/SkillFilms/{characterKey}/{i}");
            started = Time.unscaledTimeAsDouble;
            Frame = -1;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (image == null) return;
            int elapsedFrames = (int)((Time.unscaledTimeAsDouble - started) * FrameRate);
            int length = sheets.Length * FramesPerSheet;
            Loops = elapsedFrames / length;
            int next = elapsedFrames % length;
            if (next == Frame) return;
            Frame = next;
            var sheet = sheets[next / FramesPerSheet];
            if (sheet == null) return;
            int cell = next % FramesPerSheet;
            float span = 1f / Columns;
            float x = cell % Columns * span;
            float y = (Columns - 1 - cell / Columns) * span;
            // Stop half a texel inside the frame so scaled artwork cannot sample its neighbour.
            float insetX = .5f / sheet.width, insetY = .5f / sheet.height;
            image.texture = sheet;
            image.uvRect = new Rect(x + insetX, y + insetY, span - 2 * insetX, span - 2 * insetY);
        }

        void OnEnable()
        {
            if (characterKey != null) StartFilm();
        }

        void OnDisable()
        {
            if (image != null) image.texture = null;
            for (int i = 0; i < sheets.Length; i++)
            {
                if (sheets[i] != null) Resources.UnloadAsset(sheets[i]);
                sheets[i] = null;
            }
        }
    }
}
