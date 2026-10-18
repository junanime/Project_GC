# Level1 unified journal HUD — 2026-10-15

- Desktop and mobile now share a top-right journal gear, shortened XP bar (right end only), currency counters, prescription scroll and bottom-center consumables.
- Passive/active skill icons and dash/interaction buttons are removed from the field HUD. Skill descriptions remain readable in the character page. Desktop skill/dash keyboard bindings and player combat gauges are unchanged.
- Mobile retains its left movement joystick. Nearby eligible field objects accept direct taps through the existing interaction registry; UI taps and bomb targeting take precedence. This update does not invent a replacement mobile dash/active gesture.
- The gear opens character information. TAB opens character information, switches from settings to character information, or closes the character page. ESC opens settings directly and closes settings on the next press. Confirmation dialogs still consume ESC first.
- Character information and settings use the exploration-record frame. Both pages share curved bookmark tabs. Selection enlarges the bookmark, changes cream to coral, and retains gold edge feedback.
- Settings retains graphics, audio, convenience, session navigation, display confirmation/rollback, and augment-VFX opacity. Leaving settings without Apply cancels previews. Switching pages preserves pause ownership, including an underlying level-up dialog.
- Existing consumable help, guide, live map, inventory details, skill explanations and golden selection effects are retained.
- Verification entry point: `Vampire.Editor.UnifiedBookSmoke.Run`. Screenshots and logs are written to ignored `Library/UnifiedBookProof` and `Logs/unified-book-smoke.log`.
- Scope: Junhan2 source changes only; no shared-link deployment or compressed distribution build.
