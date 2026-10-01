# Roll-cake snail validation — 2026-09-28

Unity 2022.3.62f3, branch Junhan2.

- EditMode regression suite: 54 / 54 passed (snail-regression2.xml).
- Smoke run: 96 asset/rule checks plus 38 real Level 1 Play Mode checks passed (134 total); no captured runtime errors.
- The two retired UFO-default assertions were updated to assert the new snail default and 6000 HP / 30% transition configuration.
- Art review: fixed eye flattening by using equal-size body canvases and uniform base scale; retained rigid shell width across all poses.
- Balance remains provisional; no claim of performance certification on LG Gram or gameplay balance approval.

## Play smoke record

```text
SNAIL PASS: snail prefab installed
SNAIL PASS: settings available
SNAIL PASS: chocolate slow is 1.2x slow amount
SNAIL PASS: phase 2 at 30% remaining
SNAIL PASS: phase1 reduction 0
SNAIL PASS: phase1 reduction 1
SNAIL PASS: phase1 reduction 2
SNAIL PASS: phase1 reduction 3
SNAIL PASS: phase1 reduction 4
SNAIL PASS: two kills keep ingredient
SNAIL PASS: three kills disable ingredient
SNAIL PASS: two kills keep ingredient
SNAIL PASS: three kills disable ingredient
SNAIL PASS: two kills keep ingredient
SNAIL PASS: three kills disable ingredient
SNAIL PASS: two kills keep ingredient
SNAIL PASS: three kills disable ingredient
SNAIL PASS: all four removed
SNAIL PASS: basic 2/3 bursts
SNAIL PASS: bomb 2/5 throws
SNAIL PASS: middle tablet lane stays straight
SNAIL PASS: outer lanes spread symmetrically
SNAIL PASS: absorb minion count
SNAIL PASS: art Shell1
SNAIL PASS: uncompressed alpha Shell1
SNAIL PASS: art Shell2
SNAIL PASS: uncompressed alpha Shell2
SNAIL PASS: art Body1
SNAIL PASS: uncompressed alpha Body1
SNAIL PASS: art Body2
SNAIL PASS: uncompressed alpha Body2
SNAIL PASS: art Groggy1
SNAIL PASS: uncompressed alpha Groggy1
SNAIL PASS: art Groggy2
SNAIL PASS: uncompressed alpha Groggy2
SNAIL PASS: art Puff1
SNAIL PASS: uncompressed alpha Puff1
SNAIL PASS: art Puff2
SNAIL PASS: uncompressed alpha Puff2
SNAIL PASS: art DashPuff1
SNAIL PASS: uncompressed alpha DashPuff1
SNAIL PASS: art DashPuff2
SNAIL PASS: uncompressed alpha DashPuff2
SNAIL PASS: art Spit1
SNAIL PASS: uncompressed alpha Spit1
SNAIL PASS: art Spit2
SNAIL PASS: uncompressed alpha Spit2
SNAIL PASS: art Toppings1
SNAIL PASS: uncompressed alpha Toppings1
SNAIL PASS: art Toppings2
SNAIL PASS: uncompressed alpha Toppings2
SNAIL PASS: art Blueberry
SNAIL PASS: uncompressed alpha Blueberry
SNAIL PASS: art Strawberry
SNAIL PASS: uncompressed alpha Strawberry
SNAIL PASS: art Melon
SNAIL PASS: uncompressed alpha Melon
SNAIL PASS: art Mango
SNAIL PASS: uncompressed alpha Mango
SNAIL PASS: art Ball
SNAIL PASS: uncompressed alpha Ball
SNAIL PASS: art Kisses
SNAIL PASS: uncompressed alpha Kisses
SNAIL PASS: art Tablet
SNAIL PASS: uncompressed alpha Tablet
SNAIL PASS: art Bar
SNAIL PASS: uncompressed alpha Bar
SNAIL PASS: art CreamPool
SNAIL PASS: uncompressed alpha CreamPool
SNAIL PASS: art ChocolatePool
SNAIL PASS: uncompressed alpha ChocolatePool
SNAIL PASS: art Wrapper
SNAIL PASS: uncompressed alpha Wrapper
SNAIL PASS: spawner wired Level 1
SNAIL PASS: spawner wired Test Level
SNAIL PASS: spawner wired Stage1_SniperTest_Level
SNAIL PASS: immutable shell False Idle
SNAIL PASS: immutable shell False Walk
SNAIL PASS: immutable shell False Puff
SNAIL PASS: immutable shell False DashPuff
SNAIL PASS: immutable shell False Spit
SNAIL PASS: immutable shell False Roll
SNAIL PASS: immutable shell False Groggy
SNAIL PASS: immutable shell False Absorb
SNAIL PASS: immutable shell False Transition
SNAIL PASS: immutable shell False Dead
SNAIL PASS: immutable shell True Idle
SNAIL PASS: immutable shell True Walk
SNAIL PASS: immutable shell True Puff
SNAIL PASS: immutable shell True DashPuff
SNAIL PASS: immutable shell True Spit
SNAIL PASS: immutable shell True Roll
SNAIL PASS: immutable shell True Groggy
SNAIL PASS: immutable shell True Absorb
SNAIL PASS: immutable shell True Transition
SNAIL PASS: immutable shell True Dead
SNAIL PASS: field wave creates three random topping minis
SNAIL PASS: all fruit clears remove only phase1 health
SNAIL PASS: all fruit clears enter phase2 automatically
SNAIL PASS: Insert command accepted through existing terminal
SNAIL PASS: terminal arrival yields snail
SNAIL PASS: duplicate Insert rejected
SNAIL PASS: phase1 start Basic
SNAIL PASS: phase1 start Bomb
SNAIL PASS: phase1 start Fan
SNAIL PASS: phase1 start Homing
SNAIL PASS: phase1 start Summon
SNAIL PASS: phase1 start Absorb
SNAIL PASS: phase1 start Dash
SNAIL PASS: cream slow
SNAIL PASS: overlap chooses strongest
SNAIL PASS: leaving chocolate keeps cream
SNAIL PASS: dash exit immediate restore
SNAIL PASS: standing in puddle never expires
SNAIL PASS: Kisses exit tail persists
SNAIL PASS: Kisses exit tail expires
SNAIL PASS: 30% threshold transitions to chocolate
SNAIL PASS: healing never reverts phase
SNAIL PASS: phase2 start Basic
SNAIL PASS: phase2 start Bomb
SNAIL PASS: phase2 start Fan
SNAIL PASS: phase2 start Homing
SNAIL PASS: phase2 start Summon
SNAIL PASS: phase2 start Absorb
SNAIL PASS: phase2 start Dash
SNAIL PASS: trap dash starts
SNAIL PASS: trap interrupts dash and groggies
SNAIL PASS: groggy multiplier 1.5 without core
SNAIL PASS: groggy ends
SNAIL PASS: pattern before mini-stage pause
SNAIL PASS: mini-stage cancels boss attacks and effects
SNAIL PASS: mini-stage cannot drag boss into room
SNAIL PASS: lethal damage enters death
SNAIL PASS: boss death completes level
SNAIL_PLAY_ALL_PASS
SNAIL_SMOKE_DONE failed=False
```
