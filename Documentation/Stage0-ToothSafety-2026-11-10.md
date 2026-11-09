# Stage 0: tooth surfaces, safe seams and return slide

The approved 1403 × 1121 oral map is the shared art/geometry coordinate system. The floor image contains no baked actors or props. The tongue remains a deformable mesh and continues to support liquid and ceiling events.

- Only the flat chewing surfaces and the narrow incisor ridge are walkable. Tooth sides, gum and the intervening chasm are not floor.
- Four rear teeth on each side allow direct jumps from the tongue. Canines and the four incisors must be approached along the tooth route.
- Each of the fourteen teeth has its own jaw damage polygon. Warnings trace these exact polygons. The thirteen seams between teeth remain walkable and outside the damage regions; safety is determined by the actor's ground point, not by its entire artwork.
- Walking, dashing or being pushed toward the inner edge of a canine/incisor triggers a 1.12 second return: 0.65 second accelerating slide followed by a 0.47 second small hop to a validated tongue landing.
- Manual movement, a second dash, a new active skill and portal travel cannot interrupt the slide. Pause freezes progress; death, teardown and teleport restore the renderer and Rigidbody state.
- Combat bars follow the visual travel height during the jump/hop, keeping the character visible beneath the health bar. The stable idle-art landmarks and normal walking HUD layout are retained.
- The upper jaw uses the approved superior-view artwork. Chewing faces point downward rather than toward the camera. Upper gum and teeth descend together after the warning, close once, retract and hide. Jaw contact retains the existing 25% maximum-health player damage.

## Implementation

`OralMapGeometry` owns authored tooth sections, per-tooth walk/damage contours, seam positions, jump restrictions and slide entry/landing queries. `OralStageRuntime` owns traversal and cleanup. `OralFieldEvents` applies the same per-tooth damage regions that `OralMapVisual` displays.

`ApprovedFloor.png` was edited from the approved map solely to remove baked tiny props. `UpperJawTop.png` is the approved generated upper-jaw layer, rendered with the reviewed superior-view projection. Both were produced using the built-in image generation tool; no downloaded third-party artwork was added.

## Reproducible checks

Run Unity 2022.3.62f3 with `-batchmode -executeMethod Vampire.Tests.Editor.OralToothSafetySmoke.Run -oralCapture=1` and the project path. Results and actual play-mode camera PNG sequences are written under the project's `Library` proof folder. Scripted setup isolates the mechanisms but uses the real gameplay movement, jaw event, health and transition code. The separate `OralStageSmoke` suite exercises other Stage 0 events and stage transitions.

The complete Stage 0 regression suite passed 918 checks with no logged runtime errors on 2026-11-10. Captured previews use a fixed 20 fps simulation step and actual camera renders. Travel HUD checks compare the visible sprite pixel bounds against the displayed health/dash bars, including the hop peak and landing.

## Compact tongue object packing

A real entry run with seed `341562277` exhausted random placement while adding the final silver jar. Stage 0 now uses `GenerateOral`: cache safe hexagonal candidate seats, assign blood clots, elite altars and merchants first with bounded backtracking, then fill chests and currency jars. Geometric phase/orientation searches accommodate blockers without reducing counts or relaxing clearances; Stage 1 and Stage 2 retain their existing placement algorithms.

The six kind counts remain `6 / 2 / 3 / 3 / 4 / 2` (chests, merchants, blood clots, elite altars, gold jars, silver jars). Required tongue-floor clearance is 1.5, spawn clearance 3, ordinary pair spacing 4.4, same-kind blood-clot spacing 8.8, and same-kind merchant/elite spacing 7.15 world units.

A standalone mathematical reproduction of the packing algorithm passed 1,128 deterministic seeds: 1,000 unobstructed and 128 with two circular blocked regions. Every result retained all counts, floor/spawn clearance and pair spacing. Regression seed `341562277` succeeded on the first grid with 24 valid candidate seats and all 20 objects placed. Across the obstacle cases, the latest successful proposal was index 2; the special-object search needed at most eight assignment nodes. These figures are standalone packing checks, separate from Unity play-mode validation.

Final Unity play-mode verification passed 1,545 checks with zero logged errors after the packing and travel-HUD fixes. This includes 128 deterministic placement seeds (including `341562277`), all 20 actually instantiated props, exact per-kind counts and clearances, jaw hits and safe-gap escapes, restricted jumps, slide/hop/landing, pause, teleport cancellation and stage-exit cleanup. The final GIFs were encoded from this successful run's camera frames at 20 fps; they are gameplay captures with scripted setup, not generated motion mockups.
