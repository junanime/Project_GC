# Stitch patch UI — 2026-10-17

Approved reference: `04-preparation.png`, with matching augment and exploration-record previews.

- Preparation, title-menu buttons, exploration record and settings use shared stitched applique edging. Existing text, hit targets, loadout logic and map contents remain live UI.
- Preparation's eight designated ornaments use leaves (Ashi), ice (Hyuki), fire (Shini), or rounded stones (Ari). Shared frames do not change with character selection.
- Back/deploy and skip/refresh buttons have no leaves. Ordinary augment-card corner leaves are removed.
- Ordinary augment cards use the same grade accent for their frame and decorative stars. Subtle unscaled glints respect reduced motion. Approved character-specific Noble illustrations are preserved.
- Silver currency is a separate `Silver currency dock` sibling, with the existing silver foil coin sprite and live wallet label.
- Buttons retain their existing pointer/touch behavior, fixed hit targets, press feedback and gold orbit. Skip/refresh retain fixed coral/mint colors.

## Artwork

`Assets/Resources/StitchPatch/CharacterMotifs.png` is an original transparent imagegen atlas. Native Sprite rectangles crop transparent margins without altering the PNG. No compression is enabled. Frame/seam/patch geometry is native Unity UI so corners and stitches retain consistent thickness at different dimensions.

Image mode: built-in image_gen. Prompt:

```
Create production transparent UI ornament atlas based on the attached approved stitched-patch game UI. Use ONLY its small soft green leaves around the stats box and beside character/item tabs as material/style reference. Do NOT reproduce screenshot. Output one square 2x2 sprite atlas, transparent background throughout, FOUR isolated equal-scale compact icons centered in each quadrant with huge transparent gutters; each icon fits within central 65% of its quadrant, no crossing cell boundaries. Top left: a cluster of three sage/mint leaves identical in style to screenshot. Top right: cluster of three small ice crystals, pale icy blue and white. Bottom left: a small friendly coral-orange flame with golden center, no face. Bottom right: cluster of three smooth rounded light beige/taupe pebble stones. All four are sewn collectible UI appliques with subtly embossed soft bevel, clean simplified highlights, warm outline, crisp tidy pixel-aware edges, matching visual weight and silhouette size. Cozy game art, NOT photoreal, not thick black outlines, no text, no buttons, no panels, no shadows cast beyond icon silhouette, no stars, no additional objects. Actual transparent PNG. Keep individual icons simple and readable when displayed at 25-30 game UI pixels.
```

## Verification

`Vampire.Tests.Editor.StitchPatchPlaySmoke.Run` exercises title/preparation navigation, four character motifs, live silver dock, settings categories/bookmarks, reward reroll/skip, grade accents, decorative raycast exclusion, gold orbit preservation, and mobile book navigation. It captures the actual Unity screens under `../work/stitch-*.png` and restores temporary character unlocks after the run.

Final verification (2026-10-17): Unity 2022.3.62f3 Play Mode runner passed 198 assertions with no errors. Actual 1920x1080 captures cover all four preparation characters, title buttons, reward selection, six grade accents, exploration record and settings, including mobile UI mode. Inspected text/seam overlap, close-button draw order, motif transparency and ribbon stitching. Mobile mode was exercised in the editor; this was not a physical-device QA run. Existing unrelated texture import changes were excluded from the commit.

## Reference alignment revision — 2026-10-17

The three approved references (`04-preparation.png`, `04-augment.png`, `04-tab.png`) exposed stacked legacy frames and stitched overlays. The revised renderer gives each surface one owner:

- Preparation panels and selection/loadout cards replace their original frame graphics. The stitched skin sits behind icons and captions; it is never appended over the content. Skill slots and record inventory cells retain their plain original frames without a stitched overlay.
- Where the exploration record already has a printed border, the overlay draws only a single seam. Each lower inventory column gets one perimeter seam, not separate overlapping inventory/description outlines.
- Corner patches follow rounded quarter-circle silhouettes. Button seams follow their body; bookmark seams follow the curved lower edge. Label padding and record skill/item cell bounds keep text and slots clear of compartment dividers.
- Common augment cards use the reference's salmon frame, retaining the neutral grade label. Other card frames retain grade accents. Decorative diamonds use the card-frame color and the existing subtle twinkle. No corner or reward-button leaves are added.
- Character motifs, the independent live silver dock, gold selection orbit and fixed reward-button colors remain intact.

The play-mode regression now checks replacement-frame uniqueness, original-frame retirement, draw order behind content, plain inventory cell borders, column seam count and inventory/description bounds in addition to existing navigation and grade checks. Reference comparison uses real Unity captures, including all four characters and both book pages. No game balance or live shared-link build is changed.

Revision verification: Unity 2022.3.62f3 final run passed 409 assertions without errors. Reviewed actual preparation, reward, record and settings screenshots after the final seam and divider adjustments. Physical mobile-device QA was not part of this revision.
