# Mini roll-cake snail validation — 2026-09-28

Unity 2022.3.62f3 / Junhan2.

- Full EditMode regression suite: 63/63 passed (54 existing + 9 new mini tests), including a final rerun after contact-height correction.
- Real Level 1 smoke: 96 existing asset/rule checks + 122 Play Mode checks = 218 PASS records.
- Smoke finished with SNAIL_PLAY_ALL_PASS and SNAIL_SMOKE_DONE failed=False.
- New runtime coverage: attached field body, movement-driven crawling and mini-stage freeze; all eight kinds in both summon and absorb roles, rigid shell, motion, health and field-progress separation.
- Existing two-phase pattern, slowdown, trap/groggy, spawn/Insert, transition/healing, mini-stage cleanup and death/clear checks still pass.
- Visual review: all eight Unity-rendered rigs; corrected cake/body contact gap without changing collider or combat settings. Actual phase-2 summon scene capture confirms miniature snails in gameplay.
- The walk is continuous transform animation of approved body art plus one rigid ingredient-specific cake layer, not independently redrawn frames. Sixteen preview samples are rendered by Unity.
- No new final-balance or LG Gram/RTX 3060 performance certification.

## Reproduce
1. Tools/Junhan2/Install mini snail art.
2. Unity EditMode tests (SnailMiniRegressionTests plus existing suite).
3. Execute SnailBossPlaySmoke.Run in batch mode (self-terminates after Play Mode checks).
4. Tools/Junhan2/Export mini snail walk; encode Logs/SnailMinis with Tools/Art/encode_snail_walk_preview.py.

## Local evidence (ignored Logs directory)
- Logs/mini-regression-final.xml and mini-regression-final.log.
- Logs/mini-play.log and Logs/SnailQA/phase2-Summon.png.
- Logs/mini-export-final.log and Logs/SnailMinis/walk-00.png through walk-15.png.
