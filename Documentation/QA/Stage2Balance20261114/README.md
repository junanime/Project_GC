# Stage 2 balance verification — 2026-11-14

The production change and final decision are described in [the balance note](../../Balance/Stage2-20261114.md).

## Scope and reproduction

Unity 2022.3.62f3 Editor, real play mode, normal time scale. These checks do not create a Windows executable or deploy the web game. The starting revision was `6112b7655ebacc2fe89173135e0f5cff51b4a654` on Junhan2.

Run each entry point separately with Unity `-batchmode -job-worker-count 2 -projectPath <project> -executeMethod <method> -logFile <log>`. The test exits the Editor itself; do not add `-quit`. Only one Editor may use the project at a time.

| Check | Entry point |
| --- | --- |
| Shini active movement | `Vampire.Editor.ShiniEmberSmoke.Run` |
| Both Stage 2 entry routes, actual HP/spawns/rewards | `Vampire.Tests.Editor.StageDirectEntrySmoke.Run` |
| Ordinary/elite roster and field content | `Vampire.Tests.Editor.ColonContentSmoke.Run` |
| Ordinary HP 75% versus 80% | `Vampire.Tests.Editor.Stage2CombatBalanceProbe.RunOrdinaryAB` |
| Six stationary optional boss bodies | `Vampire.Tests.Editor.Stage2CombatBalanceProbe.RunOptionalCalibration` |
| Actual shark/manatee habitats and AI | `Vampire.Tests.Editor.Stage2CombatBalanceProbe.RunOptionalMobile` |

For the three comparative probes, first copy the 12 JSON files in `fixtures/` into `Library/Stage2FreshBalanceProof/variants/seed-261113-x1/`. Set `GC_COMBAT_FIXTURES` to that directory. OrdinaryAB explicitly requires this cohort. Check for an intentional `Library/Stage2BalanceStop.request` before starting: an old stop marker aborts the probe while restoring the saved profile.

The fixtures are previously observed, invulnerable Stage 2 growth opportunities at 450 seconds and 1× speed. They are not Stage 1 carryover builds, new growth runs after this HP change, or human playthroughs. Manifests record their acquisition policy and equipment. The fixture serializer did not retain live item proc timers; the probe normalizes those consistently before each window.

## Interpretation

- Ordinary A/B begins with no accumulated field enemies. Each pair uses the same complete field layout, ordinary spawn type/time schedule, starting build and time-authored route. Real collisions can make player positions diverge. Skills, dashes and new upgrades are excluded; owned automatic weapons and passive/item effects remain active.
- Spawn time runs for 3,000 fixed ticks (60 seconds). Final damage is sampled on the following render-frame continuation, so combat totals have frame resolution. The population mean is weighted by fixed ticks. Spawn-to-death duration includes enemy travel and is not hit-to-kill time.
- Boss calibration isolates damage against real stationary bodies for up to 25 seconds. Mobile probes retain real habitat geometry, AI, skills and exposure windows for up to 45 seconds. They use an automatic movement policy rather than a human player.
- The Shini fixture has only 3 embers, below the 6 needed to start the active skill. Isolated boss windows grant no artificial fuel and have no field pickups. Its active movement bonus is verified by the separate Shini test, not inferred from these fuel-limited combat rows.
- A missing measured TTK means the target was not killed in that window. `naive_hp_over_dps_seconds` is an extrapolation, never a measured clear time. `balance_valid=false` flags movement/range/damage limits in the automated policy; such rows cannot establish human difficulty.
- No result is a human win rate or proof that the full game balance is final. The production spawn curve, timing, type mix, cap, special monster HP, boss attacks/rewards and final king HP remain unchanged.

## Test interruptions

An early DirectEntry run was intentionally interrupted when the user clarified the baseline to Stage 1 ×0.75–0.80. The complete replacement run passed. The first OrdinaryAB start then encountered a stop marker from 2026-11-13 and exited with no measurement rows; the marker was archived outside the repository before restarting. Neither partial run contributes to the result tables.

## Completed results

| Suite | Result |
| --- | --- |
| Shini | PASS, 168 checks |
| DirectEntry | PASS, 1,292 checks |
| Content | PASS, 9,658 checks |
| OrdinaryAB | PASS, 8 windows / 4 matched pairs; all paired layout and spawn hashes equal |
| Calibration | PASS, 24 valid 25-second windows; no actual target defeat |
| Mobile | PASS, 8 valid 45-second windows; no target or player defeat; zero blocked decisions and capped frames |

Results and diagnostics are in the correspondingly named subdirectories. OrdinaryAB's `paired-summary.csv` summarizes its raw results. All game preference values were compared with the pre-test snapshot and restored; only Unity's own session ID/count changed. Preference backup values remain outside the repository. Generated font/time-setting changes were restored. The two unrelated pre-existing asset metadata edits were preserved outside this commit.
