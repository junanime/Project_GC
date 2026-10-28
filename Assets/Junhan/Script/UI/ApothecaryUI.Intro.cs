using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        internal void BuildIntroPresentation(IntroMovie movie)
        {
            movie.menu = root.gameObject;
            movie.overlay = Rect("Intro screen", safe.parent, 0, 0, 1, 1);
            var black = movie.overlay.gameObject.AddComponent<Image>();
            black.color = Color.black;
            var click = movie.overlay.gameObject.AddComponent<Button>();
            click.targetGraphic = black;
            click.transition = Selectable.Transition.None;
            click.navigation = new Navigation { mode = Navigation.Mode.None };
            click.onClick.AddListener(movie.RequestSkip);
            var film = Rect("Intro video", movie.overlay, 0, 0, 1, 1);
            movie.picture = film.gameObject.AddComponent<RawImage>();
            movie.picture.color = Color.clear;
            movie.picture.raycastTarget = false;
            var aspect = film.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 16f / 9;
            movie.safeArea = Rect("Intro safe area", movie.overlay, 0, 0, 1, 1);
            movie.hint = Label(movie.safeArea, "", .08f, .025f, .92f, .10f, 21);
            movie.hint.color = Color.white;
            movie.hint.outlineWidth = .2f;
            movie.hint.outlineColor = new Color32(0, 0, 0, 255);
            movie.startGate = Rect("Intro browser start", movie.safeArea, 0, 0, 1, 1).gameObject;
            movie.startButton = ActionButton(movie.startGate.transform, "인트로 시작", .32f, .42f, .68f, .55f, movie.StartPlayback, true);
            StitchPatchTheme.Button(movie.startButton.GetComponent<ApothecaryButtonFeedback>(), true);
            movie.startGate.SetActive(false);

            movie.confirmation = Rect("Intro skip confirmation", movie.safeArea, 0, 0, 1, 1).gameObject;
            var dim = movie.confirmation.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, .52f);
            dim.raycastTarget = true;
            movie.dialog = Rect("Intro confirmation panel", movie.confirmation.transform, .23f, .31f, .77f, .69f);
            var panelBlocker = movie.dialog.gameObject.AddComponent<Image>();
            panelBlocker.color = Color.clear;
            var paper = StitchPatchTheme.Attach(movie.dialog, StitchPatchTheme.Coral, true, true);
            paper.radius = 20;
            Label(movie.dialog, "인트로를 건너뛸까요?", .06f, .58f, .94f, .89f, 32);
            Label(movie.dialog, "건너뛰면 메인화면으로 이동합니다.", .06f, .42f, .94f, .62f, 19);
            movie.continueButton = ActionButton(movie.dialog, "계속 보기", .06f, .12f, .48f, .37f, movie.ContinueWatching);
            var skip = ActionButton(movie.dialog, "건너뛰기", .52f, .12f, .94f, .37f, movie.ConfirmSkip, true);
            movie.continueButton.GetComponent<ApothecaryButtonFeedback>().borderOnly = true;
            skip.GetComponent<ApothecaryButtonFeedback>().borderOnly = true;
            StitchPatchTheme.Button(movie.continueButton.GetComponent<ApothecaryButtonFeedback>(), false);
            StitchPatchTheme.Button(skip.GetComponent<ApothecaryButtonFeedback>(), true);
            movie.confirmation.SetActive(false);
        }
    }
}
