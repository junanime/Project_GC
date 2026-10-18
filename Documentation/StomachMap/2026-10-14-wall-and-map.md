# Stomach wall, field bounds and TAB overview

This update supersedes the preview-only scope of `2026-10-14.md`. It includes the earlier finite-map/floor work and the approved tall curved wall on Junhan2. No shared-link deployment, mobile build or image compression is performed.

## Terrain and movement

- Keep the 203.2381 × 135.4921 world extent, approved floor detail scale, bean-shaped organ, upper-right inlet and lower-left outlet.
- A shared signed-distance field drives the clean pink wall shader, movement boundary and spawn projection. The rounded lip uses soft peach highlights; the wall has no floor motifs or fleshy mottling.
- Wall band: 8 world units, with 5.2 units of approach/climb before the upper limit. The upper section remains unreachable; actor collider radius is included in that limit.
- Uphill resistance is continuous: 100% flat speed at the foot, 50% halfway through the climbable band, 10% at its end. Tangential movement and walking down remain responsive. Resistance compensates existing Rigidbody drag rather than permanently modifying character/monster stats.
- Release movement to accelerate gently down toward the floor. The slide target rises from 0.12 to 1.2 units/s with slope progress, using 2.4 units/s² smoothing.
- A dash cannot start on the slope or cross the flat-ground edge from outside it; rejected starts do not consume charges.
- Player and pooled monsters share `StomachSlopeBody`. Portal travel, mini-stage ownership, suspended field monsters, paused run flow and Stage 2 bypass these rules. Mini-stage arena movement remains unchanged.
- Direct-velocity controllers (chameleons, treasure runners, nutrition thieves, locked rush attacks and digestive enzymes) also receive the same uphill multiplier. The independent roll-cake boss applies it at its movement command, avoiding repeated multiplication across physics ticks.

## Spawns

- Chests, NPC shops, normal/special monsters (including sniper Setup adjustments), digestive enzymes, elite summon objects and supply capsules project to the flat floor with a footprint margin.
- The finite terrain installs before initial field loot spawns.
- Mini-stage owned monster spawns and room rewards retain their original room coordinates. Room initialization during a portal transition is also exempt when outside the field.

## TAB map

- Render the same complete terrain into a cached 768 × 512 overview texture; fit its aspect ratio inside the existing exploration panel.
- The entire organ is visible. Existing discovery rules continue to govern object/monster markers; marker icons, legend, player indicator and relative world positions are retained.
- Stage 2 and mini-stage views use the existing map path.

## Validation

Unity 2022.3.62f3 PC Play Mode, real Level 1 with Ashi. `StomachMapPreview.InstallAndRun` runs `StomachMapChecks` before saving visual captures. Checks cover spawn projection across 500 deterministic candidates, actual chest/NPC/sniper setup, uphill speed, player and monster sliding, dash charges, pause behavior, actual mini-stage travel/room reward/return, and the TAB texture/icons.

See `Proof/Wall/` for the final captures and check log. The harness temporarily suppresses combat to inspect the terrain, restores backed-up preferences, and does not save temporary room/character changes to the game scene.

The terrain remains a 2D gameplay surface with simulated slope resistance and downhill motion. Random hill placement and anatomical-hole travel are not part of this update.
