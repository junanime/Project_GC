# Stage-one exploration layout and chocolate currency jars

The approved fixed-count, randomized layout is now the default in stage one on Junhan2. Shared-link builds are not published by this change.

## Initial objects

| Object | Count |
| --- | ---: |
| Item chest | 24 |
| Merchant | 12 |
| Blood clot / mini-stage entrance | 14 |
| Elite altar | 8 |
| Gold chocolate jar | 20 |
| Silver chocolate jar | 8 |
| Total | 86 |

The ordinary nearby timed spawners for these four interaction objects are suppressed in stage one. Monster waves, field events, supply capsules and mini-stage reward objects keep their existing schedules. A mini-stage roundtrip retains the same field objects, including consumed/broken state. An actual stage-two transition clears stage-one props and permits the legacy stage-two schedules.

## Distribution

- All positions stay at least 10 world units inside the map boundary and 7 from the starting position. Solid scene obstacles are excluded.
- Eight broad angular sectors balance coverage with random coordinates; these are not grid-aligned rows.
- Usual spacing is at least 8 world units. Same-type blood clots use 16, merchants/altars 13.
- A 20% pair-placement attempt creates occasional pairs at 6.05–7.5 units, subject to safety checks. A partner can only form one pair. Blocked pair attempts revert to normal spacing.
- An initial chest, merchant and clot are attempted at rotated distances of 9, 15 and 21 units respectively.
- 250 deterministic layout seeds passed counts, map-inside and overlap checks; 2,219 pairs in total, averaging 8.88 pairs per run. This is not a guarantee of a fixed pair count.

## Jar art and interaction

Approved PNGs are preserved without recompression. Unity imports alpha-trimmed Gold/Silver sprites from the two original sheets. Gold uses the round jar and round sealed gold-foil coins; silver uses the hexagonal jar and sealed silver-foil coins.

Jar visible height is 55% of Ashi's measured in-game body height: 0.285185 world units. They have attack triggers, with no solid movement obstruction. One damaging player hit breaks a jar. The glass/cork fragments scatter, fade and are disposed; three visible coins pop out as one reward transaction. After a 0.6-second landing arc, coins wait until the player's actual pickup radius reaches them, then move into the character. Pause and mini-stage transitions suspend this animation/collection.

| Currency | Amount and probability | Expected amount per jar | All jars, expected total |
| --- | --- | ---: | ---: |
| Gold | 10: 50%, 30: 35%, 50: 15% | 23 | 460 |
| Silver | 5: 50%, 10: 38%, 20: 12% | 8.7 | 69.6 |

Gold uses the existing field-event and player gold-gain modifiers. Silver is added to the persistent wallet immediately on collection and recorded for the results screen. Creating that run record does not enable per-monster silver rewards in a scene that did not already use them. Simultaneous hits cannot duplicate drops. Jars do not grant XP, monster kills, life-steal or combat damage contribution.

## Validation

`Vampire.Tests.Editor.FieldLayoutSmoke.Run` passed in the actual Level 1 scene with Ashi:

- Actual initial counts for all six object types; no extra startup chest or timed respawns.
- 250 seeded layouts and exact 10,000-quantile payout probability checks.
- Real needle collider hit, shared area attack and thrown bomb explosion.
- Exact visible jar size, pause/mini-stage guards and repeated-hit protection.
- One-time gold credit with modifiers, persistent silver and run-record credit.
- Actual mini-stage entry/return preserving the field and merchant counts.
- No jar contribution to monster kills or combat damage score.

`Vampire.Tests.Editor.SupplyResultsSmoke.Run` also passed after the layout change, covering the existing capsule interaction/relic selection and success/failure result panels, inventory icon bounds and all four nonlethal character portraits.

Play-mode captures and a lossless-frame-derived GIF are stored with the local task artifacts under `artifacts/field-layout-2026-10-15`. The harness regenerates captures in `Library/FieldLayoutProof`.

This commit also includes the preceding October QA corrections (capsule landing/shadows, pooled monster visual reset, multi-elite altar rewards, skill/dash reward limits and UI fixes). The rollcake snail boss is unchanged.
