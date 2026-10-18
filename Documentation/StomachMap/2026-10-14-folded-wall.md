# Folded stomach wall — 2026-10-14

Replaces the smooth gradient wall with the approved **01 · 고운 도트** concept. The existing bean-shaped perimeter, map scale, inlet/outlet, and flat-floor tile scale are preserved.

## Presentation

- `Assets/Art/StomachMap/FoldedWall.png`: uncompressed, generated wall material with large rounded folds and clean stepped pink/peach shading.
- The wall is mapped along the actual perimeter using periodic sine/cosine coordinates. The loop and mesh chunks share coordinates. Texture-edge crossfading avoids mirrored pointed folds.
- The eight-unit-high wall band curls into the floor. At the foot the upper rim is beyond the normal camera; climbing brings the rounded peach rim into view.
- Floor material coordinates bend along the inward normal. Motifs become progressively denser toward the slope and fade into the clean upper wall. Tangential scale remains unchanged; derivatives fade undersampled motifs on the TAB overview.
- The full TAB map captures the same terrain material, so its wall art updates automatically.
- This is a 2D terrain projection with illustrated depth, preserving the existing movement model.

## Movement

- Wall band: 8 world units. Traversable depth: 6 units (three quarters), with a collider-radius inset.
- Uphill resistance still reaches 50% at the middle of the traversable section and 90% near its end. Player and monsters retain downhill sliding.
- Dashes now activate on slopes and consume/recharge normally. Each movement step and final destination are swept against the same invisible limit. Downhill and sideways dashes remain possible.
- Portal travel and remote mini-stage exceptions are preserved.

## Art provenance

Built-in imagegen edit; no CLI or external API fallback. Reference: `exec-2afa7708-8fea-438b-9a85-5dab686ae804.png` (user-selected fine-dot concept). Generated source: `exec-6e4bd370-9441-4730-a457-794f4900028c.png`; copied unchanged into the project as `FoldedWall.png`.

Generation prompt:

> Derive a production 2D game WALL SURFACE TEXTURE from this approved reference. Output ONE rectangular full-bleed wall texture, landscape 3:2. NOT comparison sheet, NO text, NO character, NO HUD, NO rim, NO outside background, NO floor patterns. Only the pink smoothly curved folded wall seen in the reference, covering every pixel. Preserve the exact approved fine clean pixel style: smooth solid stepped coral/peach highlight bands and medium rose shadows, no granular noise, no wetness. Across width show THREE large rounded raised folds with shallow deep-rose valleys between, like loading corridor's broad soft ridges. The folds run from top to bottom, fairly straight upper half, their lower halves curl outward in a gentle S-curve and spread flattening into a smooth rose-pink basal surface at bottom 12% of image. Camera/lighting same as reference, broad pale peach highlight on left shoulder of each ridge, middle rose shadow on right. This is a rectangular unwrapped wall texture to wrap around a stomach arena boundary: no scene perspective, no horizon, no depth vanishing point. Edge-to-edge seamless horizontal repeat: left/right edge both same middle of a wide valley with same color/value along every corresponding y. Top edge shows continuation of wall folds cut off, not rim, sky or flat border. Bottom edge smooth consistent salmon pink (#ef7e8c), no floor, floor will be separately rendered. The three fold silhouettes should be varied slightly yet similarly wide. Fine pixel-art edges, flat color clusters, no airbrush, no 3D-render glossy skin. Keep geometry-readable shading like reference. Actual game texture asset.

## Validation

Evidence is stored under `Proof/FoldedWall/`: actual Level 1 screenshots, full-map capture, TAB capture, and movement/spawn/mini-stage integration results. The preview temporarily pauses combat and restores tutorial/currency preferences; it does not publish a shared build.
