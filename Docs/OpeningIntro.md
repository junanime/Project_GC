# Opening intro

## Playback contract

- On the first lobby entry of each application launch, play the opening movie above the main menu. Returning from a run does not replay it. No persistent viewed flag is written; a new launch plays it again.
- The supplied movie is stored unchanged at `Assets/StreamingAssets/Intro/intro.mp4` (1280 × 720, H.264/AAC, approximately 38 seconds). No Unity movie transcoding, added compression, or change to its audio.
- `Assets/Resources/IntroMovieSettings.asset` controls enablement, relative filename, volume and preparation/stall timeout. To update the movie, replace the MP4 at the same path and rebuild. Keep its `.meta` file. A different filename can be set in the settings asset without editing scenes.
- The video fits the display without stretching or cropping. Unused space is black. UI respects the safe area.
- Main-menu input and music are suppressed while the movie is visible. The existing main menu is revealed immediately on completion or confirmed skip, and its music is restored.
- WebGL uses URL playback and direct audio output. After preparation it presents an intro-start button to obtain a browser playback gesture. Desktop native builds play automatically. Actual phone/browser verification is separate from editor touch-mode verification.

## Skip interaction

1. First Esc / pointer tap shows a short reminder.
2. Second press within three seconds opens a confirmation and pauses the movie at its current position.
3. “계속 보기” resumes; “건너뛰기” opens the menu. Esc while the confirmation is open also resumes.

The cream/coral stitched panel reuses the current game button components: mint continue button, coral skip button, gold focus effect. It scales in over 0.18 seconds; reduced-motion settings disable that animation. The default keyboard selection is continue. The triggering input cannot open menu settings behind the intro.

Focus loss pauses playback. Missing files, playback errors and bounded preparation/progression timeouts return to the usable menu. Render textures, callbacks and the overlay are released on exit. This flow does not change game time, progression, inventory or save data.

## Verification

Run `Vampire.Tests.Editor.IntroMovieSmoke.Run` in Unity 2022.3.62f3 with graphics enabled. The smoke test decodes the actual supplied MP4, exercises Esc through the input backend, checks pointer raycasts, pause/resume, timed double press, existing button effects, confirmed skip, natural completion, lobby reentry, missing-file fallback and the start-game button. Captures are written to `../work/intro-*.png`.

Unity reference for the WebGL audio routing choice: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Video.VideoPlayer-audioOutputMode.html

Verified 2026-10-19: all 35 checks passed in the Unity editor, including full natural playback (0.5, 10.5, 20.5, 30.5 seconds then completion). The movie uses unscaled game time rather than the audio DSP clock. Desktop Esc and pointer/touch-mode raycasts were exercised; physical mobile and WebGL browser playback were not run.

Original/copied MP4 SHA-256: `6d9f1a927faef7c380a52443b2c2ce9c4d010b83bbbfa0f02d739e5b0e843fd7`.
