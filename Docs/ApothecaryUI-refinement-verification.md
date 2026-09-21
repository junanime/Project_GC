# UI refinement verification

Date: 2026-09-21 (KST)
Unity: 2022.3.62f3
Branch: Junhan2

## Passed

- Editor scene integration: 157 assertions, FINISHED failed=False.
- Windows development build: Succeeded, errors=0.
- Windows player verification: 8 assertions, FINISHED passed=True.
- Git whitespace validation: passed.

The scene test covers actual pointer hit targets, hover/press/touch release and cancellation, touch slider input, silver deduction and rejected purchases, one relic and two distinct consumables, delivery into gameplay, TAB pause/resume, success/failure return, independent audio volumes, persistence, cancellation, display confirmation rollback and settings during a paused run.

The standalone player test applies windowed 1280x720, previews 960x540, restores 1280x720 on confirmation timeout, applies borderless fullscreen and saves the confirmed mode. It also checks Unity quality, VSync, frame cap and independent audio source levels. Tests restore existing progression/settings saves.

## Limits

Mobile was checked using editor touch events and wide/tablet aspect ratios. No physical Android/iOS device test or mobile build was performed. Character skill remains the requested future placeholder. Existing non-Ashi test-character art and names remain catalog data.

## Proof

Run `Vampire.Tests.Editor.ApothecaryUIPlaySmoke.Run` for scene coverage and captures in `Library/ApothecaryUIProof`. Run `Vampire.Tests.Editor.ApothecaryBuildCheck.Run` for the development player. Launch that player with `-apothecarySmoke` for native display checks; the runner is excluded from release builds and never runs without the explicit flag.
