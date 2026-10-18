# Character consumable cards and magnetic quest note — 2026-10-15

## Applied UI

- The four consumable slots are separate cream cards with coral keycaps. Existing item order, icons, capacity, keyboard shortcuts, bomb targeting, pointer use and tooltips remain connected to the original inventory.
- Empty slots show 0/capacity with subdued icons. Occupied slots show actual count/capacity. PC shows the existing number/letter shortcuts; mobile shows item names. Hover/press retains a gold border highlight.
- Small ornaments appear only at the two ends of the inventory and at the quest magnet: Ashi three-leaf sprigs, Ari ivory phoenix feathers, Hyuki blue ice crystals, Shini orange fire. No additional raster textures, compression or animated scene particles are introduced.
- The former 비전서 title and noble reward source text now read 퀘스트.
- The title band's fill calls `PreparationIdentity.ColorFor(character)`, exactly the same source as the lobby's active skill frame. Ashi RGB (.91,.19,.40), Ari (1,.89,.65), Hyuki (.16,.49,.91), Shini (.90,.20,.24). White lettering except Ari's dark lettering keeps contrast.
- The note starts open. Clicking the header or decorative magnet folds the sheet upward, leaving the title/progress strip visible below the stationary magnet. Clicking again releases and unfolds the sheet downward. The moving shaded paper lip and layered closed edge suggest a folded note without stretching the objective text. Duration 0.36 seconds, unscaled time, reversible while moving; reduced motion changes states immediately.
- Removed the previous 4-second auto-close. TAB/settings roundtrips preserve the chosen open/closed state.
- Objectives, progress, completed strike-through and one-time noble reward claim still use PrescriptionRuntime. Claim controls are masked and non-interactive while folded or transitioning. Reward selection keeps the existing pause/menu behavior.
- The paper uses bounded text, responsive anchoring and the same desktop/mobile layout. Existing unified journal/gear remains unchanged.

## Verification

Play Mode entry: `Vampire.Editor.CharacterHudSmoke.Run`.
Final result: **122 checks passed, 0 failures**, Unity 2022.3.62f3. The initial mobile potion caption overflow was fixed using the short caption `증식 물약`; the full name and description remain in the existing tooltip/help. See [verification log](verification.txt).
Checks cover all four character colors, ornaments and titles; inventory stock; actual pointer raycasts; open/close, fixed magnet, interruption/reversal, persistent state and removal of auto-close; PC and simulated mobile layouts; one-time quest reward opening the existing selector.
Captures originate from the actual Level1 scene. Objective completion is seeded for repeatable UI validation, not a natural progression/balance test. Fold midpoint captures are deterministic geometry samples so slow editor frames cannot skip them. Mobile checks use editor simulation, not physical devices.

The UI theme is switched on the running Ashi test actor to verify all four character blueprints without restarting the scene. The central actor sprite is therefore Ashi in these UI verification captures.

| Theme / state | Actual capture |
| --- | --- |
| Ashi | [Open](Proof/Ashi-open.png) |
| Ari | [Open](Proof/Ari-open.png) |
| Hyuki | [Open](Proof/Hyuki-open.png) |
| Shini | [Open](Proof/Shini-open.png) |
| Folded / midpoint | [Closed](Proof/Ashi-closed.png) · [Midpoint](Proof/Ashi-folding.png) |
| Mobile landscape / tablet | [1560×720 viewport](Proof/mobile-1560.png) · [1024×768 viewport](Proof/mobile-1024.png) |

Capture pixel sizes include the editor's display scale. The mobile viewport tests check actual safe-area bounds and EventSystem hit targets.

## Scope

Junhan2 source only. No shared-link deployment, WebGL/mobile build or asset compression. Previously modified unrelated texture `.meta` files are not included in this commit.
