# Junhan2 UFO cores and return portal

The gameplay UFO is `Assets/Junhan/Prefabs/Boss/Test/BossPartDamageTestRoot.prefab`.
Existing class, interface, enum and serialized reference field names are retained.
Red=1, Yellow=2 and Blue=4 retain their numeric values. Orange=8 and Green=16 are independent flags; All=31. A union means multiple active cores, never a synthesized color.

## Pattern inventory and registration

| Core | Single-core scripts |
| --- | --- |
| Red | BossFanShotPattern, BossChargePattern |
| Orange | BossRadialBurstPattern |
| Yellow | BossBombLobPattern, BossCaffeineInjectorPattern |
| Green | BossRadialBurstPattern, BossAbsorbMinionHealPattern, BossColaBottleHealPattern |
| Blue | BossHomingMissilePattern |

All existing configured BossPatternBase instances, including older variants, are registered. Projectile, warning and summon prefab settings are preserved. Each instance keeps its own cooldown.

Implemented combinations:
- Red + Orange: BossChargePattern, available from phase 2.
- Yellow + Green: BossColaBottleHealPattern, available from phase 2.

`BossFiveCoreSkillController.combinationSlots` contains all ten unordered pairs. Eight slots have no Pattern yet and cannot be selected. To extend a slot, add a BossPatternBase-derived component under the active UFO hierarchy, assign its Owner Part to the slot Primary, and put the component in the slot Pattern. Set Combination Core to the slot Secondary (or leave it empty to use the slot's authoritative secondary requirement). The controller initializes and selects slot patterns even when they are not separately in Skill Pool. Conflicting or incomplete registrations are rejected. Both required cores must be alive; both become active before execution, and losing either interrupts execution.

The legacy core swap timer is disabled for this UFO. The legacy visual component now displays individual active colors side by side rather than replacing two colors with orange/purple/green. Its old serialized prefab fields remain for compatibility.

Caffeine and cola challenges on the UFO retain their active cores for the full challenge. Absorb healing, caffeine and charge cancellation explicitly clean up summons, warnings, collision exclusions and movement/contact locks as appropriate.

Other existing combat mechanisms are not BossPatternBase skills: BossPressureOverloadController is driven by PressureFilled; BossRainbowAnnihilationController is driven by rainbow highlight events. Their existing disabled UFO setup is retained. The original Monster BossAbility family (Walk, Shotgun, Grenade, Charge, BulletHell) belongs to the separate legacy BossMonster architecture and is not interchangeable with BossController's Pattern components.

## Return portal

MiniStageReturnInteractable.SetUnlocked updates the serialized SpriteRenderer with `mini_not_open` or `mini_open`. Existing MiniStageRoomBase completion/reward/optional-return rules remain the authority. Entry, reuse, and relocking use the same setter. The setup migration wires all directly serialized return portals in prefabs and scenes; prefab instances inherit these references.

## Ground shadow

UFOGroundShadow is a child of the stationary boss root, separate from UFOBody's float motion. Default sorting layer, order -20 places it below the body (10), cores (20) and actors. BossVisualOrderKeeper excludes this renderer.

BossGroundShadow queries player/monster colliders and checks the actor origin against an ellipse. All of an actor's child SpriteRenderers receive a multiplicative RGB tint based on shadow color/alpha; original alpha is preserved. Shared per-renderer contributions make overlapping shadows independent of exit order. Teleports, pooled/disabled actors and disabled/destroyed shadows release their contribution. External color writes such as hit flashes are retained as the new base color.

## Verification

`Tools > Junhan2 > Configure and validate boss and portals` runs the explicit asset migration and checks. The migration reassigns the known existing patterns and should not be run after manually customizing new slot registrations without reviewing its mapping.

Batch migration and regression checks:

    Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod Junhan2BossSetup.Run -quit -logFile <log>

Read-only asset checks, physics area/tint regressions and a rendered UFO preview:

    Unity.exe -batchmode -projectPath <repo> -executeMethod Junhan2BossSetup.ValidateAndPreview -quit -logFile <log>

Results are written to ignored `Logs/Junhan2-validation.txt`; the preview is `Logs/UFO-shadow-preview.png`. Tests cover all eight Pattern types, every serialized instance in a pool, phase gating, simultaneous required-core activation, secondary loss, missing scripts, portal relocking, overlap exit order, external color changes, multiple actor colliders, teleport, pooling and player/monster detection. These are automated editor/physics checks, not a full manual boss fight playthrough.

Verified on Unity 2022.3.62f3: 102 checks passed, batch exit code 0, no C# compile or serialized-reference errors. The rendered shadow preview was inspected after sizing adjustment. Full manual combat playthrough was not performed.
