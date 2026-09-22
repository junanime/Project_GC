# Shini lava pools and tornadoes — 2026-09-22

## Applied rules

- FireNeedle start retained; continuous dash/movement trail replaced by stationary lava pools.
- Two seconds of actual physics movement creates one pool at the feet. Idle pauses accumulation. Dash has no bonus spawn.
- Pool lifetime: 6 seconds; width about twice the original character. No direct contact damage.
- Contact applies a guaranteed shared FireNeedle burn stack, then at most one per second per target across overlapping pools.
- Existing individual burn lifetimes, tick damage, upgrades and boss tick caps are retained.
- Active: 8 seconds, cooldown 35 seconds, both starting on input. Existing and newly spawned pools each erupt once.
- Phoenix embrace compressed to 0.6 seconds, simultaneous with eruption; original burning character art starts after embrace.
- Eruption lasts 1.6 seconds and hits once at 0.6 seconds. Damage is D*(1+1.5*S)+remaining burn ticks; D is the current noncritical needle hit damage and S is consumed stacks.
- Shared stacks are cleared before damage. Overlapping eruptions processed in the same update hit each target once.
- Pools expire at their own deadline; an already-started eruption can finish after pool expiry.

## Artwork

User-approved source: LavaTornadoSource.png (6 columns, 4 rows).
LavaTornado.png is mechanically extracted, keyed and anchored. Pool lower footprint pixels are identical for all six idle frames; only upper molten bubbles/flames animate. Frame 6's neighbouring-row spill is excluded from anchor detection.
The pivot is fixed at the ground surface, not at the moving flame bounding-box center. Pools and columns use GroundEffects, above Background and below actor layers. Column width about twice character width, height approximately 26.7% of camera height.

## Verification

Unity 2022.3.62f3 Windows development build: succeeded, 0 errors.
Native Windows player suites all completed with passed=True and no runtime errors:

- Shini replacement suite: 33 checks (movement/idle timer, contact burn, overlapping pool interval, shared cap, cash-out formula and atomic consumption, active/cooldown start, rise hit timing, transformed sprites, new-pool eruption, TAB pause, expiry, real movement, old trail removal, death cleanup).
- Hyuki/ice regression: 188 checks (sleep crystals, walking/dash snow, 20/30/40% chill, fourth-hit guaranteed freeze, 5% proc, 5-second freeze, shatter, overlapping slow effects, pooled cleanup, storm/shaders).
- Ashi regression: 12 checks (dash passive, R cut-in, buff modifiers, timer pause/expiry and pointer activation).

The first movement test exposed render/physics timing undercounting. Movement accumulation was moved to FixedUpdate; rebuilt and rerun successfully.
Artwork was visually inspected in the atlas and native game capture.
Actual Android/iOS device performance and long-run balance playtesting were not performed.

## Reproduction

Run Vampire.Editor.ShiniLavaInstaller.Run only when rebuilding the atlas/skill definition.
Build with Vampire.Tests.Editor.ApothecaryBuildCheck.Run, then launch the resulting player separately with -shiniSkillsSmoke, -phoenixSkillsSmoke, and -ashiSkillsSmoke. Use separate -logFile destinations.
ShiniSkillPlayTests.Run is retained as a compatibility build entry; assertions execute in the native -shiniSkillsSmoke suite.
