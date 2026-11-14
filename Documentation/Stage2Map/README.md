# Stage 2 colon circulation map — 2026-11-09

## Scope

- Approved circulation plan: four smooth inner ridges; detached upper/lower ridges and two side-wall branches. The lower-left outlet-right passage remains open.
- Stage 2 width 162.59048, height 108.39365 units: 80% of Stage 1 in both dimensions (64% of rectangular area).
- Stage 1 geometry/material are unchanged. Stage 2 is not a scaled screenshot: mesh chunks share world-space pixel coordinates at 0.05084381 units/pixel.
- Peach/apricot floor uses broad low-contrast wave bands, sparse small crosses and tissue cells. Coral wall tissue is sampled along the actual curved wall length; the outer rim is visibly taller/broader than inner ridges.
- Inner wall slope width 4.5; climbable portion 3.5; hard crest 1.0. Existing uphill slowdown, dash sweep stop, monster steering and wall collision use the new geometry. Visible outer wall width is 7.425. Corridor widths vary with the approved curved plan; 25 units is an approximate broad-corridor target, not a guaranteed minimum.

## Placement and loading

86 field objects remain: 24 chests, 12 merchants, 14 blood clots, 8 elite altars, 20 gold jars, 8 silver jars. Six optional boss homes remain.

The entire field/home plan is checked before objects are instantiated. An exhausted proposal is discarded and rerolled deterministically, up to 32 proposals; counts and wall clearances are never silently reduced. The director uses the exact habitat seed validated with the accepted field plan. Loading ends only after the existing readiness checks finish.

- Field objects require at least 2 units of flat-floor clearance beyond the full visible wall footprint (outer wall 7.425, inner wall 4.5). This is separate from player uphill/sliding collision.
- Reserve all six large homes before the 86 props. Place widely spaced blood clots first, then merchants/altars, then small props. Stage 2 regular minimum gaps: 11.2 clot-to-clot, 9.1 merchant-to-merchant / altar-to-altar, 5.2 otherwise. Rare close pairs retain 4 units, or 6 when a clot/altar is involved.
- General monster spawns, elite spawns, drops and dynamic NPC spawns use the same safe-floor projection; monster footprints are checked again after sprite/size setup.
- Normal boss-home separation: 20.8 units; intended rare pair minimum 10; pair proposal probability 2.5%, subject to geometry.
- Entry clearance: 14 units. Habitat-to-prop clearance: 5.6, manatee 7.6.
- Manatee attaches only to the two detached inner walls. Entire house footprint and all three exit approaches are checked.

## Manatee art

HouseBase, HouseLip, HouseMask and HouseDoor were updated together. No boss body/pose/walk art was changed. The three door centers were measured from the new source pixels; the same habitat and masks are used for appearance, pipe travel, door game and defeated state. The old wood door is replaced by a coral tissue hatch.

House and wall art were produced with built-in image generation using the approved map palette; the house sprite was then packed into the existing 720x240 atlas layout. Masks/lips derive from its real opening pixels. Door atlas remains 112x88. All production art lives under Assets/Resources; no runtime dependency on generated_images folders.

## Validation

See the accompanying validation report and in-game screenshots. Placement validation checks 256 deterministic seeds, all 86 object positions, six safe habitats, spacing, whole-floor connectivity with boss clearance, and the lower-left passage. Full game tests cover Stage 1 carry-over, loading readiness, all six arrivals/patterns, house masking and capsule travel.

Earlier map-revision Unity 2022.3.62f3 results, 2026-11-09 (before the wall/score changes below):

- Map validation: PASS, 256 layouts / 1,536 homes; zero disconnected floor cells, no placement failures.
- CapsuleStageSmoke: PASS, 77 checks. Includes real capsule travel, paused entry, original pixel density, uphill slowdown, downhill slide, internal-wall dash stop and optional-home interaction.
- ColonBossSmoke: PASS, 229 checks. Includes another 256 live-map layouts / 1,536 homes; all six arrivals and 25 patterns; stable body sizes, no inherited barrage, status effects, cleanup and real carried-over needle damage.
- Live layout sample: 256 distinct layouts, 23 with a close pair; minimum home gap 11.148, prop gap 5.600, entry gap 14.004 units. A close-pair proposal is probabilistic, not a guaranteed per-run quota.
- Both play-mode test error logs were empty. Existing compiler obsolete-API warnings remain.
- Visual review: world-scale floor and wall detail, full circulation layout, wall junctions, manatee aperture masks and coral door. Visual wall normals blend at junctions while physical nearest-surface normals remain exact.

The tests now mark their result RUNNING before execution. Capsule slope sampling is relative to wall width, and the recorded carry-over fixture temporarily unlocks its owned needles with preference backup/restore; the tests no longer depend on the developer's lobby unlock choices. No production ability balance or unlock rules changed.

Screenshots are Unity material renders (`overview`, `floor-detail`, `inner-wall-detail`) and live game captures (`manatee-home`, `manatee-entry`), not concept mockups. These are automated regression checks, not a human clear-rate or long-session performance study.

## Wall combat, mini-boss scoring and guidance — 2026-11-09

Ground attacks and player needles sweep their path against the visible wall footprint. Warning fills and continuous ground VFX clip to reachable floor on the attack side of a wall. Airborne lob/landing attacks may cross a wall in flight, but landing damage stays local and respects occlusion from the landing point. Player wall climbing/sliding remains unchanged.

All six optional encounters use the existing miniBoss ranking category (500 points each), as do the four Stage 1 chameleons. They do not increment final-boss kills or clear Stage 2. Discovered homes remain on the map with active/completed indicators; the Tab map shows optional progress out of six. Final boss 변비왕 remains reserved.

See [wall/score verification](../QA/Stage2WallScore20261109/README.md) for the checks run on this change.
