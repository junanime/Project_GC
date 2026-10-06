# Final art prompt set

Tool: built-in image generation, transparent alpha requested. Original source sprites retained. Cropping and sprite import metadata share a fixed pixels-per-unit per sheet; no per-frame auto-resize. Unity renders the animation; Sharp only encodes rendered frames into GIF.

## Shared sheet prompt

Use case: stylized-concept. Asset type: production game animation sprite sheet, real transparent alpha background. EXACTLY 4 columns x 4 rows equal square cells, 16 sprites, no lines no text no shadow no portal. Every character full body within its own cell with 15% margins, same character size across frames (except deliberate crouch), same camera and crisp pixel art texture of reference. Do not draw travel trajectories; runtime handles translation and rotation. Read sequence left-to-right top-to-bottom. First 8 cells are dive poses, last 8 emerge poses. Preserve identity, colors, clothes and anatomy.

## Shini

Reference: `Assets/Junhan/Art/ShiniSkills/Idle.png`. Red phoenix, red rounded tuft, yellow beak, determined brows, black W varsity jacket, white wings, red feet. Omit surrounding aura. Dive: preparation, crouch, powerful takeoff, soar, forward flight, streamlined dive, wings closed, compact dive. Emerge: three compact somersault tucks (runtime rotation), unfold, three-point hero landing, raise head, two final hero-landing poses.

## Hyuki — fixed pose from apex, throughout ejection

Edit target: `codex-clipboard-c3676039-82fc-45e8-b230-7129987deae6.png`. Remove dark background to transparent alpha. Straighten upright so tuft above center and shoes downward. Preserve exact curled posture, sleepy dash eye, yellow beak, blue bird, black jacket/white sleeve, grey tucked knee and white shoes. Compact tucked ball, never standing or extending limbs. Same chunky pixel art with black outline. No new costume details, motion lines, text or shadow. One fixed sprite, no redesign. Runtime bounces it rigidly without rotation or deformation.

2026-10-06 follow-up: `HyukiApproach.png` reuses the previously generated 4×4 Hyuki sheet, importing only its first row (calm step, other step, small crouch, takeoff with knees bending). Reference was `Assets/Junhan/Art/CharacterDesigns/Hyuki_Walk.png`: blue bird, tall tuft, calm eyes, yellow beak, black W varsity jacket with white sleeves, grey pants, black-white shoes. The fixed single sprite replaces these four poses only at the jump apex. No new image generation was needed for this revision; the approved upright curl and other characters' images are unchanged.

## Ari

Reference: `Assets/Junhan/Art/CharacterDesigns/Ari_Idle.png`. Pink egg bird, brown forehead mark, small yellow beak, cream wings/feet, blush. Dive: waddle, tuck, two upright round ball poses, squash for bounce, stretched bounce, two airborne curls. Runtime rotates the ball. Emerge: two curls, unfold, ground impact, rise with @@ spiral eyes, wobble left/right, final dizzy pose. No stars or chicks baked into sheet; runtime adds orbiting chicks.

## Ashi

Reference: `Assets/Junhan/Art/AshiRemake/Ashi_Idle_8_Eyebrows.png`. Yellow chick, red headband trailing left, orange beak and feet, black eyebrows, pink cheeks. No costume. Dive: three running steps, crouch, leap wings open, knees bent, forward fall, compact dive. Emerge: tuck, upward hop, apex wings open, descend, landing crouch, recover, two regular standing poses.

## Overcharged clot

Edit target: `Assets/Junhan/Art/mini_open.png`. Preserve original pink/red fleshy portal silhouette, mouth location and raised rim. Add branching dark-purple/lavender swollen veins along existing lobes, restrained violet danger gleam. No teeth, eyes, rock, horns, wings, skulls or text. Single transparent sprite; deliberately chunky pixel appearance, same camera/orientation. Dark-red oval opening remains usable for diving.
