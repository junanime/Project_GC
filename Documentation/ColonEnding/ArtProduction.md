# 스테이지 2 출구 아트 제작 기록

제작: 내장 image_gen. 최종 아트는 Assets/Resources/ColonEnding에 저장.

확정 기준: 부드러운 D 장벽/일체형 출입구, 기존 바닥 유지. C 굳은 봉인석 자물쇠와 금 체인. 변비왕의 휴지 장식 금 열쇠 지팡이를 처치 시 드랍.

## KeyScepter.png 최초 프롬프트
Use case: stylized-concept. Production Unity 2D sprite asset. Reference image: the approved C concept, use ONLY the middle golden key-scepter, precisely matching ivory toilet paper roll with tiny gold crown emblem, red/gold little crown atop roll, short curling paper edge, polished gold shaft, two blocky gold key teeth at bottom pointing right. Single upright complete key scepter centered, silhouette clean readable at game size, gentle cartoon shading dark brown outline matching reference. No floor, no shadow cast on background, no sparkles detached from object, no labels, no border, no other assets, no character, no lock. Object fills 85% of canvas height with transparent padding. True transparent alpha background. Keep proportions roll head about 38% width, shaft long. Game ready cutout.

## 열쇠 정리 프롬프트
Edit this game sprite: preserve the exact key-scepter object, its shapes, ivory toilet paper and gold/red crown and key teeth. REMOVE ALL atmospheric brown glow, aura, haze or shadow around the object. Outside the dark brown solid outline must be perfectly transparent (alpha 0), no semi-transparent halo. Tight portrait framing around entire object with 5% empty transparent margin on each side. No background, no text, no additions. True alpha transparent game sprite.

## SealLock.png 프롬프트
Use case: stylized-concept. Production Unity 2D sprite asset. Reference image is approved C sealing stone padlock concept. Recreate ONLY the single complete padlock on left, centered, frontal game view, genuine transparent background. Warm orange-brown rounded hardened sealing stone body with shallow cartoon cracks, four broad polished gold corner braces each a rivet. Gold thick U shackle on top closed. Red scalloped wax seal medallion centered with black keyhole, small gold crown above and gold laurel flanking hole exactly reference. Gentle cartoon shaded rendering and dark brown clean outline. Object fills 87% canvas, keep padding. No text, no concept board, no floor, no detached sparkles, no background/shadow, no chains, no key, no character. Single game-ready isolated cutout.


## 2026-11-14 연출 개선용 추가 아트

두 파일 모두 내장 image_gen으로 기존 아트/확정 시안을 참고해 생성한 투명 PNG다. 원본 보스 Body 및 포즈 이미지와 바닥 패턴은 교체하지 않았다. 변기는 소환 전 빈 상태와 넘치는 연출의 기반이며, 벽 이미지는 높이와 방향에 맞춰 표면에 적용한다.

### EmptyThrone.png

- 제작 방식: 참조 이미지 기반 생성, transparent_background=true.
- 저장: `Assets/Resources/ColonEnding/EmptyThrone.png`

```text
Use case: precise-object-edit. Production Unity 2D game sprite. Reference is the existing Constipation King body artwork. Extract/reconstruct ONLY his EMPTY royal toilet throne without the poo king, no body, face, arms, feet, cape, crown, scepter, paper, waterjets. Preserve exact toilet identity: pale ivory-pink porcelain oval bowl seat, flush tank behind on upper left with gold ball flush handle, squat rounded pedestal with red shield panel and gold small toilet emblem at front, gold base trim and ONE tiny central underside outlet. Front three-quarter slightly topdown game view matching reference. Show a real empty oval bowl cavity inside ivory seat, dark warm brown bowl interior with a shallow calm brown liquid surface inside. Lid absent/open out of view. Clean rounded dark brown cartoon outline, painted crisp warm highlights exactly source style, royal golden fittings. Entire isolated toilet fills 85% canvas with 7% transparent margins, no floor, shadow, aura, glow, label, panel, background. TRUE transparent alpha sprite. This empty throne will have the existing king animated rising from the bowl later, so keep the broad front seat rim and bowl visible.
```

### WallSoft.png

- 제작 방식: 참조 이미지 기반 생성, transparent_background=true.
- 저장: `Assets/Resources/ColonMap/WallSoft.png`

```text
Use case: stylized-concept. Production game surface asset, reference image wall style only. Create one isolated straight horizontal segment of the softly rounded PINK INTESTINAL WALL matching exactly reference smooth pastel pink inflated wall, not realistic flesh. 4 broad connected pillowy rounded lobes, smooth joined surface, upper crest pale warm peach highlight, front vertical face dusty rose, soft deeper mauve at foot. Broad restrained gradients with very subtle soft painted mottling. High clear height and rounded volume, crisp silhouette but no black outline. Front threequarter topdown view used by reference. Shape a horizontal long wall with rounded endcaps; width about 3 times height. Only wall, entire object and allendcaps visible. No flooring, no floor shadow, no blood, veins, wrinkles, teeth, doors, holes, props, labels, borders or diagrams. True transparent alpha background. Center with slightpadding. This will provide painted surface detail to game shader; game uses own geometry. Do NOT depict any map or floor.
```
