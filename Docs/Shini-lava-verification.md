# Shini lava pools and tornadoes — 2026-09-22

## 2026-09-23 skill revision (supersedes activation rules below)

- Keep FireNeedle as Shini's starting augment, using the same ownership/progression path as Hyuki's IceNeedle.
- A successful dash also erupts all existing, unexpired field pools. Dash does not add a bonus pool.
- Active retains its existing eruption on activation and automatic eruption of newly spawned pools.
- Active additionally grants 30% movement speed and reduces accumulated movement time per pool from 2 seconds to 1 second for its existing 8-second duration (35-second cooldown).
- `ShiniSkills` exposes `shiniActiveMoveMultiplier` and `shiniActivePoolInterval`; the installer reproduces the defaults. Existing phoenix/burning visuals remain unchanged.
- An eruption already rising cannot be reset by another trigger. After its 1.6-second animation ends, another valid dash can erupt the same living pool again.
- Burn rules are unchanged: no direct pool contact damage, guaranteed shared burn stacks once per target per second, 0.5% max-health ticks, existing caps/upgrades and tornado cash-out formula.
- `Monster.TakeDamage` subtracts fractional damage; `DamageText` formats with `N0`. A 25-health target takes 0.125 per base burn tick even though the popup can show 0. No damage/rounding change was made.
- Expanded native Shini suite covers starting fire ownership, real burn tick/low-health fractional damage, active speed/interval, retained active tornadoes, dash-only activation, rejected dash, repeat eruption and expiry.
- Burn visuals retain body flames and add a pale orange (RGB 1 / 0.62 / 0.28, alpha 0.25) silhouette between the monster and flames. Reuses the existing sprite-mask tint shader without changing ice visuals; tracks the current sprite/flip/transform and hides on consumption, death or pool disable.
- Hyuki motion and unrelated skills remain unchanged. Per the follow-up request, cap sleep at 60 stacks (= 12 crystals, 5 stacks each), including restored/consumed stacks. The existing +8% per-stack movement benefit therefore caps at +480%, with its 4-second duration unchanged. Tooltip and installer reflect this limit.
- Earlier optional broad Hyuki checks failed dash-wake timing and freeze-cleanup assertions; that run was stopped and the temporary test edit reverted. Only the newly requested stack cap is tested here; no full Hyuki pass is claimed.

### Final verification for the 2026-09-23 revision

- Unity 2022.3.62f3 Windows development build: `Succeeded errors=0`.
- Native `-shiniSkillsSmoke`: 58 assertions passed, including orange overlay layering/cleanup, fractional damage and the six targeted Hyuki cap checks. No runtime errors in this suite.
- Native `-ashiSkillsSmoke`: 12 assertions passed. No runtime errors in this suite.
- Viewed native `Shini-orange-burn.png` capture. No new raster asset is needed: the orange layer uses each monster's own current sprite mask.
- Android/iOS hardware testing and the deferred Hyuki motion remake are outside this verification.

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
