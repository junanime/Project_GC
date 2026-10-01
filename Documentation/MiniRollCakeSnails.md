# Mini roll-cake snails — 2026-09-28

## Scope
- Replace cake-only miniature art for field spawns, boss summons and absorption.
- Vanilla types: blueberry, strawberry, melon, mango.
- Chocolate types: ball, Kisses, tablet, caramel/nut bar.
- Each rigid cake cutface and its topping contains only that type's ingredient.
- Attach the approved boss's matching cream/chocolate snail flesh. No feet or lower-face eyes.
- The full-size boss, combat values, spawn counts and field-progress rules remain unchanged.

## Walking
- Shared SnailMiniVisual rig: two SpriteRenderers, body in front of cake.
- Approved Body1/Body2 art is reused; no new face or eye design.
- Movement-distance-driven continuous crawl, one cycle per 0.72 world units.
- Flesh contracts/extends subtly; shell and topping move as one rigid layer, with 1.4-degree sway.
- Direction follows actual horizontal movement. Stationary actors return to neutral.
- Pause/mini-stage suspension stops the cycle; large position jumps do not advance it.
- Uniform visible shell width 0.72 and body canvas width 1.02 for all eight kinds.
- Field pooled respawns disable the outgoing rig before replacing it.

## Assets and reproduction
- Eight original alpha PNG layers: Assets/Resources/SnailBoss/Minis.
- Generated with built-in image_gen from the approved eight concept images.
- Prompts: MiniRollCakeSnails-art-prompts.json.
- Source pixels are kept unchanged. SnailMiniSetup measures alpha bounds to set PPU and bottom-center pivot.
- Body assets and cake assets stay separate; no per-frame regeneration or shell squashing.
- Tools/Junhan2/Install mini snail art calibrates importers.
- Tools/Junhan2/Export mini snail walk renders 16 samples of the actual rig to Logs/SnailMinis.
- Preview layout: blueberry, strawberry, melon, mango / ball, Kisses, tablet, bar.

## QA
- SnailMiniRegressionTests covers eight ingredient/body mappings, imports, immutable shell scale, movement phase, facing, pause delta and teleport handling.
- SnailBossPlaySmoke checks real field movement/suspension plus both summon and absorb roles for all eight kinds, preserving health and field-progress isolation.
- Actual results are recorded after running Unity in MiniRollCakeSnails-validation.md.
