# Roll-cake snail boss — Junhan2

## Approved design and runtime asset contract
- Single slice, no feet or lower-face eyes. Eyestalk tips are the only eyes.
- Vanilla: strawberry, mango, blueberry, melon embedded in the shell and four toppings above.
- Chocolate: cut bar, chocolate balls, Kisses, tablet inside. Top: cut bar, one ball, one Kisses, diagonal tablet.
- Thin-outline pixel-painted art. Sprite PNGs imported uncompressed with alpha, no mipmaps.
- Shell is an immutable sprite at a fixed width per phase. No frame-by-frame cake regeneration or squash.
- Body, shell, toppings are separate renderers; idle breath and walking affect flesh only.
- Dash: both cheeks swell, stalk eyes > <, spit wet lane, hide body, small hop, fast roll with ghost trails/splashes, emerge and land toppings.
- Basic spit keeps round eyes. Groggy uses dizzy eyes; chocolate trap also tumbles the rig.
- Old UFO assets are retained for rollback, but LevelBlueprint final-boss references use RollCakeSnail.prefab.

## Provisional tuning (SnailBossSettings.asset)
| Item | Phase 1 | Phase 2 |
| --- | --- | --- |
| Phase | Vanilla until 30% total HP remains | Chocolate; healing cannot revert |
| Basic | 18 radial blueberries, 2 bursts / 0.8 s, angle offset | 3 bursts / 0.6 s, distinct offsets |
| Bomb | 2 strawberries / 2 s; pulp impact damage, seeds visual only | 5 smaller Kisses / 0.2 s, smaller radius, 3 s chocolate terrain |
| Fan | 6 distinct melon pieces spreading, one volley | Two 6-deep x 3-lane tablets / 0.8 s, directions 15° apart |
| Homing | Mango follows 3 s then plants; dash triggers damage + groggy | Wrapped bar follows then melts to wrapper; dash triggers tumble + groggy |
| Summon | 6 random four-topping minis | 6 minis, 1.3x HP |
| Absorb | 6 minis, each 3% total max HP | 12 faster minis, each 2% total max HP |
| Dash | One roll, 10 units/s, 9 units | Two rolls, 1 s between rolls including second windup |
| Wet dash slow | 25% reduction, immediate on spit | 30% reduction = 1.2x slow amount |
| Slow exit | Dash: immediate restore | Kisses: 2 s after leaving; timer never expires while inside |
| Groggy | 3 s, 1.5x incoming damage, no exposed core | Same |

Tablet internal lane angle starts at ±12°, middle lane 0°; this is explicitly a play-test value, separate from the 15° between the two boards.
HP starts at 6000, projectile speed 3.2, contact damage 12, projectile damage 8, bomb damage 14. Balance is provisional, not a claim of final difficulty tuning.

## Field mini roll cakes
The mini visuals now use eight single-ingredient cake cutfaces with matching toppings and attached snail bodies, including movement-driven crawling. Field spawns use the four vanilla kinds; summon/absorb actors use their phase's four kinds. See [MiniRollCakeSnails.md](MiniRollCakeSnails.md).
Legacy pooled AcidLeechMonster remains as the component/file identity to preserve serialized scene and pool links. In-game identity and art are field mini roll cakes.
- Preserve curved random path motion, remove feeding anchor and difficulty increase.
- Three kills of the same topping remove that phase1 ingredient/pattern before boss summon.
- Each removed type subtracts 25% of the **phase1 70% health allocation**, i.e. 17.5% total max health.
- All four removed: show empty vanilla cavities, then transition to chocolate; phase2 health remains.
- Field wave: three every 35 seconds, first at 15 seconds, cap 12 living, stops when boss exists.
- Summoned and absorbing minis do not modify field progress.
- New run resets counts; scene transfer keeps counts.

## Entry points and QA
- Existing final boss terminal/automatic schedule -> EntityManager.SpawnFinalBoss -> RollCakeSnail.
- Insert uses the same terminal flow, with direct configured spawn fallback only if terminal absent.
- Duplicate spawns rejected.
- Tools/Junhan2/Install roll cake snail: explicit asset installer.
- Tools/Junhan2/Validate roll cake snail: rule, reference, import and shell-scale checks.
- SnailBossPlaySmoke.Run: real Level 1 play-mode test, all seven patterns in both phases, status overlap/exit, trap/groggy, phase transition/heal, field mini wave, all-four-removed transition, mini-stage suspension/cleanup, death/clear; captures under Logs/SnailQA.
- New code and assets are isolated under Assets/Junhan/Script/Boss/Snail and Assets/Resources/SnailBoss.

## Art provenance
Generated with the built-in image generation tool using the approved thin-outline reference. Atlas slicing only extracts generated pixels. Final prompts are stored alongside this document.
