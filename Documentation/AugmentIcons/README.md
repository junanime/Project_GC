# Augment icon replacement

Special: 16 live icons. Legendary: six user-approved originals including GastricPeristalsisWave. Planned: Wood, Fire, Ice, Wind and Vibration needles are imported sprites ONLY; not added to gameplay pools.

## Locked needle
`ArtSource/AugmentIcons/NeedleMaster.png` is the user's original attachment, copied unchanged. All 21 special/planned icons use the same crop and only one whole-object similarity transform per needle. Ring, grip and shaft are never individually resized or regenerated. A final uniform inset applies to the entire composite. Raster rotation/resampling is allowed; geometric proportions are fixed.

Effect-only layers are separate from the master. Old generated metal was removed before recomposition. `manifest.json` records the source hash, crop, uniform scales, rotation matrices and paths. `python Tools/Art/rebuild_augment_icons.py --check` reconstructs all icons and checks exact final pixels, similarity matrices, alpha and all six approved legendary file hashes.

## Unity
Sprites: 1254 square, Single / FullRect, 100 PPU, Point filtering, no mipmaps, no NPOT rescale, lossless import, max 2048. Common old textures are not overwritten or deleted.

`Vampire.Tests.Editor.AugmentIconTests.InstallAndValidate` configures imports and changes only image references for the existing enum selections. Conditional fallback icons follow parent images. PoisonContagion continues sharing Poison as the intentional parent-dependent exception.

Legendary and special generation prompts are separate files beside this document. Generated future effects must remain separate from the immutable master; do not ask an image generator to redraw the needle.

## Verification — 2026-09-17

- Baseline: Junhan2 78e6b51, including the two upstream commits fetched before this change.
- Python reconstruction check: PASS, 21 composites; six legendary original hashes match.
- Unity 2022.3.62f3 compilation: PASS (existing unused-field warnings remain).
- Both AugmentIconTests checks executed by InstallAndValidate: PASS. Import, image binding, uniqueness of the 22 distinct icons, conditional fallback sharing and planned-image exclusion were checked.
- AugmentIconPlaySmoke.Run: PASS. Entered Play Mode in Level 1, initialized LevelManager and generated UI geometry with matching textures for all 27 icons. Finished failed=False. Temporary gallery objects were destroyed and no scene was saved.
- Contact sheets visually inspected. This was an automated headless UI smoke, not manual gameplay or a full combat regression suite.
- Prefab diff restricted to image/fallbackSprite references. Gameplay scripts, enum values, ability statistics, scenes and existing shared image files were not changed.

Notion history: https://app.notion.com/p/3dd636e6d18f81f4b164d69335686272
