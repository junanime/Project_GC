# Stage 2 — whipped cream field

Stage 1's roll-cake snail now melts into a 4.8-unit crater and drops three regular ability-selection chests. The run continues until the player chooses to interact with the crater. Both E and the existing mobile interaction control use the shared interaction focus system. The same BloodClotTravel rig selects Ashi, Ari, Hyuki or Shini from the current character blueprint and performs both entry and landing.

The transition remains in Level 1's scene. It does **not** call Character.Init, Inventory.Init, AbilityManager.Init or restore a lossy scene-transfer snapshot. The same character, skills, augments, relic runtime (including consumed revival), consumables, merchant item runtime, experience, silver and statistics remain alive. Existing ground loot and unopened chests remain available. Old field monsters despawn without kills/rewards; hostile projectiles are cleared. Special-event schedules, field minis, the toad encounter and final-boss altar restart. Stage 2 gets a fresh 900-second clock and the existing roll-cake boss. Its final defeat ends the run; there is no Stage 3 yet.

## Nine ordinary snails

Original cinnamon roll, strawberry donut, pistachio macaron, honey waffle, kimbap, steamed dumpling, cheeseburger, watermelon and caramel pudding. All share a 1.2-unit visible body and 0.84-unit shell width; padding is excluded. Their food shells remain rigid while actual movement drives a small body crawl and shell sway. The ground shadow stays fixed to the foot. Hitboxes include body and shell. Sprites import at 512 px maximum; full generated source PNGs are preserved. StageTwoInstaller calibrates source-pixel PPU and bakes visible bounds without enabling runtime texture readback.

Cream uses the existing infinite-background shader and mirrored edge sampling. No new scene, second camera or separate mobile gameplay implementation is introduced.

## Entry HP calibration

The baseline is the probability-weighted mean HP of Stage 1 normals at 95% elapsed time, after the existing spawn balance applier has run. Boss damage measures actual HP removed by attacks (not overkill), divided by active vulnerable combat seconds with a 20-second minimum; traps do not inflate that damage measure.

`entry = max(lateMean × 1.15, clamp(bossDps × 0.85, lateMean, lateMean × 2.5)) × clamp(entryPower / defeatPower, 0.8, 1.5)`

Power is damage multiplier × attack-speed multiplier, capturing reward choices made after the boss. The nine HP values are fixed once on entry: `ceil(entry × 0.8 × spread^(tier/8))`, where `spread = clamp(lateMax / lateMin, 2.4, 3.8)`. Population weights shift progressively from low to high tiers across five keyframes. Further player upgrades do not rescale these values. Existing enzyme difficulty multipliers continue to apply through LevelManager's normal spawn path.

## Verification

`StageTwoSmoke.Run` exercises the live Level 1 scene: first boss death and duplicate protection; three queued rewards; all four travel animations and physics restoration; actual field interaction; same-object inventory/skill preservation; real augment, merchant item, relic and spent revival retention; level/XP/damage retention; unopened chest and consumable retention; all nine sprites and HP tiers; movement, shadow and mini-stage suspension; hit damage; shifted spawn probabilities; the automatic 15-minute boss and final clear. Test-only PlayerPrefs are backed up and restored. Screenshots are saved under Library/StageTwoSmoke.

Art provenance and generation prompts are in StageTwoArt.json. Reimport with `Tools/Junhan2/Install stage 2`; general discovery-art rebaking also includes Stage2Snails.

## Development delivery (2026-10-07)

This stage is delivered for PC development first. Do not deploy Stage 2 to the existing public share link. The only authorized public update is SnailBossSettings.maxHealth from 6000 to 24000, retaining the previous compiled game and textures on both PC and mobile. Mobile compression, mobile builds and further QA are deferred at the user's request. A new Stage 2 sharing link will be prepared later, after more development, when requested.
