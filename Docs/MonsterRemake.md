# 미역 함정·타코야키 저격 몬스터 적용

## 게임 동작

- 미역: 휴면 → 8프레임 감싸기 → 마지막 포획 자세 유지. 뒷 미역 / 실제 아시 / 앞 미역을 별도 렌더러로 정렬합니다. 앞 미역은 얼굴을 가리지 않도록 몸통 아래에 맞췄습니다.
- 포획 표정: 3프레임으로 부리를 열고 마지막 프레임을 유지합니다. X X 눈, 혀 없음, 벌어진 부리 사이는 얼굴의 노란색입니다. 기존 아시와 동일한 443px 캔버스 및 675 PPU를 사용합니다.
- 방향·위치: 포획 순간의 방향과 위치를 유지하며, 대쉬 중 포획돼도 이동을 끝까지 진행하지 않습니다.
- 공격: 새 침/샷건/대물침, 연사, 분신·침진 및 자동 공격 증강, 투척·근접 공격을 포획 상태에서 차단합니다. 기존에 발사된 투사체의 이동과 충돌 처리는 변경하지 않았습니다. 체력 회복·방어 같은 비공격 효과는 일괄 비활성화하지 않습니다.
- 탈출: 화살표 퍼즐 성공으로 해제. 시간 경과 자동 해제는 끄고, 씬 전환·비활성화·사망 정리는 별도로 유지합니다.
- 저격수: 기존 에임 추적 → 목표 고정 → 발사 순서를 유지합니다. 총구와 조준선 시작점을 좌우 주둥이 끝에 맞췄습니다. 몬스터 X/Y 및 충돌체를 움직이지 않고 그림만 반동을 표현하며, 가쓰오부시 덩어리가 들렸다가 안착합니다.
- 탄환: 기존 저격탄 프리팹의 그림을 타코야키로 변경했습니다. 속도·피해·타깃 레이어는 유지합니다.

## 구현 위치

- `Assets/Art/MonsterRemake/`: 게임용 투명 PNG 28장 및 Unity 메타데이터
- `SeaweedTrapVisual`, `SniperRecoilVisual`: 앞뒤 레이어/발사 반동 전담
- `TrapMonster`, `SniperMonster`, `PlayerTrapBindRuntime`, `Character`: 실제 상태 연결과 정리
- `Ability.SpawnPlayerProjectile`: 신규 플레이어 투사체 발사 차단 공통 경로
- 각 공격 Ability 및 전설 컨트롤러: 별도 발사·접촉 피해 경로 차단
- 캐릭터/함정/저격수 Blueprint 및 몬스터·저격탄 Prefab: 이미지 참조 저장
- `Assets/Tests/Editor/MonsterRemakeTests.cs`, `MonsterRemakePlaySmoke.cs`: 반복 실행 가능한 검증

## 검증 방식

Unity 2022.3.62f3에서 `Vampire.Tests.Editor.MonsterRemakePlaySmoke.Run`을 실행하면 코드/에셋 검증 10개와 실제 Level 1 플레이 검증을 연속 실행합니다. 플레이 검증은 기존 투사체 이동, 신규/지연 발사 차단, 좌우 포획, 마지막 표정·감싸기 프레임 유지, 화살표 탈출, 공격 복귀, 풀 재사용, 필드 중단/복귀, 실제 저격 발사 및 반동 완료를 확인합니다. 테스트용 풀과 Blueprint 복제본은 Play Mode에만 존재하며 씬/에셋으로 저장되지 않습니다.

## 이미지 제작 기록

내장 이미지 생성 도구로 승인 시안에서 게임용 파츠를 만들고, PNG 슬라이싱·투명 배경 정리·기준점 정렬을 거쳐 저장했습니다. 인게임 이미지의 흰 소스 하이라이트를 지우지 않도록 중립색 배경 영역과 고립된 작은 잡픽셀만 정리했습니다. 원본 승인 시안은 별도로 보존했습니다.

최종 제작 프롬프트:

1. **미역 파츠**: “Production game asset precise edit. Remove ALL yellow chick/Ashi character pixels, headband, feet from all eight cells, reconstruct ONLY seaweed where obscured. Preserve the exact green seaweed monster eight-stage activation progression from flat dormant pool to curled upright strips to front crisscross restraint. No chick, no yellow, no red. EXACT 4x2 equal cell grid of 8 frames. Same baseline, center, scale in every cell; generous clear padding no spill. Each seaweed ring has open transparent interior for an independently rendered player. Dark thick pixel outlines, same green palette. Genuinely transparent background and holes, no checkerboard drawing, no labels.”
2. **포획 아시**: “Precise edit Image1 is production sprite sheet, image2 reference for FACE ONLY. Preserve exactly Image1 4x2 eight frames character size silhouette outline feet headband, frame layout and transparent background. Change ONLY each face: both eyes black pixel X X like image2. Orange beak splits into upper and lower small clam halves opening from fixed side corners. Gap inside beak is yellow face color, NO dark mouth cavity NO tongue NO pink NO teeth. Frame1 minimally open, frame2 half open, frames3-8 fully open. Keep head facing same right direction throughout. NO seaweed in output. Actual transparent background. Do not enlarge or move character relative to each cell. This is a captured facial animation sprite sheet.”
3. **저격 반동**: “Production precise edit of this approved 4x2 8-frame game animation sprite sheet. Remove ALL projectiles/takoyaki balls and muzzle sparks at right, keeping cream funnel muzzle intact. Keep exact rest/shoot/recoil/airborne bonito wig/land sequence and same red octopus design, batter sauces head, face and body. Keep all pink feet identically placed in all 8 cells: fixed stationary turret no root movement. Uniform 4x2 equal cells, sprites centered same position and baseline, EXACT same body scale across all cells. Entire bonito tuft fits inside each cell, generous top margin. True transparent background, no checkerboard drawing, no text, no ground shadow. Do not change approved style or outline thickness. Only dry flakes rise as one clump; batter dome and sauces remain attached.”

