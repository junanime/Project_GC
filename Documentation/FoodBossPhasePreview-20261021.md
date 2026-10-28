# Boss phase transitions — 2026-10-21

Implemented on Junhan2 using the approved phase-one and phase-two sprites.

| Boss | Duration | Transition and timed cues |
| --- | --- | --- |
| 롤팽이 | 3.2 s | Soft compression → chocolate palette spreads across the shell → toppings settle. |
| 데니카니 | 4.8 s | Cracks spread through the Danish shell → open phase-two shell unfolds → old claw casing splits off while new claws enlarge. |
| 마리비 | 4.6 s | Lid shakes, separates along its actual curved seam, and spins away → embarrassed closed eyes/blush → original angry face and short rebound. |
| 탕뎅이 | 5.2 s | Old strawberry carapace cracks and breaks into six pieces → phase-two body emerges → horns strike at 3.20 s and 4.15 s with dry candy-contact cues. |
| 후루미 | 3.8 s | Bowl braces → additional noodles unfold with staggered delay → noodles settle. |

All transitions retain health and reject both direct and periodic damage throughout.
Contact damage is disabled during transition. Player movement remains available.
Animation and transition audio pause with gameplay; cancellation restores the normal
body and stops voices. Phase-two recovery remains unavailable. Next action waits for
0.5 s after transition plus its own normal warning.

## Presentation

- Shared C-style slim pixel health rail: navy inset, gold chamfered border, 50% notch.
- Boss name centered above the rail and below the timer. No phase-number/status text.
- Approved original sprites, foot pivots, body dimensions and outlines remain the source.
  Runtime meshes articulate existing anatomy without replacing character designs.
- Claw growth and horn motion use a continuous incoming body with pinned joint regions;
  no punched-out holes or rigid cut seams at limb roots.
- Maribi's lid follows its curved lower seam rather than a rectangular crop; detached
  artwork remains in the world and is not clipped by a UI viewport or sprite cell.
- 18 new original, dry game-style transition WAVs in `Audio/Transitions`, scheduled by
  `FoodBossPhaseCues`. They replace the old generic Phase cue during these transitions.
- The approved 67 attack/cry WAVs, especially Tangdengi Skill1, remain byte-identical.
- Transition sounds are synthesized material impressions, not recordings of real candy.

## Verification / reproduction

Run Unity with graphics enabled:
`-batchmode -projectPath D:/Project_GC/Project_GC -executeMethod Vampire.Tests.Editor.FoodBossSmoke.RunPhaseProof -logFile ...`

The harness loads actual Main Menu → Level1, disables unrelated combat for controlled
recording, and captures 185 frames per boss at 25 fps (7.4 s). The normal health threshold
starts each transition. Every transition frame receives attempted lethal direct and
periodic damage. It checks remaining health, completion, all scheduled audio cues,
restored original sprite, maximum duration, pause/cancel behavior and disabled recovery.
This run is excluded from scoring and restores saved currency/tutorial preferences.

Frames: `Library/FoodBossProof/phase-{0..4}-{000..184}.png`.
Results: `checks.txt`, `errors.txt`, `result.txt` in the same directory.
`Tools/Art/encode_boss_phase_previews.cjs` packages these actual Unity renders into GIFs.
GIF previews are silent; sound plays in-game. Audio generation/measurements:
`Tools/Audio/build_food_boss_transitions.py`, `Documentation/FoodBossTransitionAudio.json`.
`Tools/Audio/build_food_boss_audio.py` verifies the 67 approved source hashes.
