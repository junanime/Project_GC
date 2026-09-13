# UFO 4단계: 코어별 스킬 풀 / Yellow+Green 조합

대상: junanime/Project_GC, Junhan2. 기준 커밋 ace0841a3f28ff2d272a7b4f0de42a876dfd9524.

## 적용

GitHub에서 Junhan2를 최신으로 받아 Unity 2022.3.62f3으로 열면 됩니다. 별도 수동 컴포넌트 연결은 필요 없습니다. ZIP을 사용하는 경우 Unity를 종료하고 ZIP의 Assets 폴더를 프로젝트 Assets에 덮어씁니다. 기존 파일은 먼저 백업하세요. 새 스크립트의 .meta도 함께 복사해야 프리팹 연결이 유지됩니다.

수정 프리팹: Assets/Junhan/Prefabs/Boss/Test/BossPartDamageTestRoot.prefab.
Boss_Test.prefab은 다른 구형 샘플입니다.

## 배정과 계층

PartsRoot 아래 기존 RedCore, OrangeCore, YellowCore, GreenCore, BlueCore의 위치·회전·크기는 유지했습니다. 각 코어 아래 Skills가 추가되고 YellowCore 아래 ColaCombination이 추가됩니다. UFOBody/AttackMuzzle은 기존 하단 동력부 위치입니다.

| 코어 | Skill Pool | 비고 |
|---|---|---|
| Red | RedFan | 기존 부채꼴 탄막. 기본 공격도 Red가 담당 |
| Orange | OrangeRadial | 기존 원형 탄막 |
| Yellow | YellowBomb, YellowGreenColaHeal | 콜라는 Green과 함께 활성화될 때만 사용 |
| Green | GreenRadial | 기존 원형 탄막의 탄 수·회전 간격을 바꾼 임시 변형 |
| Blue | BlueHoming | 기존 유도탄 |

기존 비활성 패턴은 비교용으로 그대로 남겼습니다. UFO는 각 코어의 Skill Pool에 등록된 활성 패턴만 선택합니다. 구형 전역 코어 색상 교체가 UFO 스킬 후보를 결정하지 않습니다.

## Inspector에서 스킬 바꾸기

1. 프리팹을 열고 원하는 색상 Core를 선택합니다.
2. BossPartDamageTestPart의 Skill Pool 목록에 원하는 패턴 컴포넌트를 넣습니다.
3. 해당 패턴의 Owner Part에 같은 코어를 넣습니다. 두 연결이 일치해야 선택됩니다.
4. 단독 스킬은 Combination Core를 비워둡니다. 조합 스킬은 두 번째 코어를 넣습니다. YellowGreenColaHeal은 Owner Part=YellowCore, Combination Core=GreenCore입니다.
5. 패턴 GameObject와 컴포넌트가 활성화되어 있어야 합니다. 패턴별 Cooldown 등 기존 설정을 그대로 사용합니다.

BossController와 같은 오브젝트에 추가된 BossFiveCoreSkillController는 Health Root와 Basic Attack Owner가 연결되어 있습니다. Active Primary/Active Secondary는 Play 중 현재 공격에 참여하는 코어를 확인하는 값입니다. 직접 설정하지 마세요.

## 페이즈와 피격

1페이즈는 한 코어를 활성화합니다. 2페이즈부터는 살아 있는 코어 두 개를 함께 활성화합니다. 조합 스킬은 지정한 두 코어를 사용합니다. 단독 스킬일 때는 다른 생존 코어 하나를 함께 활성화하되 스킬은 선택한 한 개를 실행합니다. 코어가 하나만 남으면 단독 스킬로 계속합니다. 3페이즈도 현재는 같은 두 코어 규칙을 유지하며 기존 HP 기반 페이즈 배율을 사용합니다.

이번의 '활성화/열림'은 공격 상태입니다. 덮개 이미지나 개폐 애니메이션은 아직 추가하지 않았습니다. 살아 있는 모든 코어는 공격 참여 여부와 관계없이 계속 피격됩니다. 본체는 피격되지 않습니다. 코어 Collider/Layer/Sorting과 하단 발사점은 이전 설정을 유지합니다.

## 콜라 회복

기존 콜라 병 4개를 보스 주변에 소환합니다. 회복 대기 시간과 초당 회복량은 기존 프리팹 값을 사용합니다. 최대 지속시간은 30초로 설정했습니다. 콜라가 유지되는 동안 같은 조합을 유지하고 다른 스킬은 시작하지 않습니다. 플레이어는 계속 코어와 콜라를 공격할 수 있습니다.

회복량은 살아 있는 코어의 부족한 HP 비율대로 분배합니다. 파괴된 코어는 부활하지 않고 각 코어 최대 HP를 넘지 않습니다. Yellow나 Green 파괴, 보스 사망, 패턴 중단, 제한시간 종료 시 콜라를 정리합니다. 회복으로 페이즈가 이전 단계로 내려가지는 않습니다.

## Unity에서 확인

1. Level 1 씬을 열고 Play 합니다. 기존 BossPartDamageTestSpawner의 Spawn Test Boss 메뉴로 보스를 생성합니다.
2. 각 코어의 HP와 Skill Pool을 Inspector에서 확인합니다. 이제 기본 공격과 등록된 스킬이 실행됩니다.
3. 모든 탄 발사지점이 UFO 하단 동력부인지 확인합니다. 위치를 바꾸려면 UFOBody/AttackMuzzle만 옮깁니다. 콜라 병의 주변 소환 위치는 발사점과 별도입니다.
4. HP를 줄여 2페이즈에 도달한 뒤 Yellow+Green 조합이 선택되는지 봅니다. 쿨타임과 무작위 선택 때문에 즉시 나오지는 않을 수 있습니다. 해당 조합 중 Active Primary=YellowCore, Active Secondary=GreenCore여야 합니다.
5. 콜라 회복 대기 후 살아 있는 코어와 보스 HP가 함께 오르는지 봅니다. Yellow 또는 Green을 파괴하면 병이 사라지고 회복이 멈춰야 합니다.
6. Red를 파괴하면 기본 공격과 RedFan이 더 이상 나오지 않아야 합니다. 다른 코어도 파괴한 색상의 스킬은 다시 선택되지 않아야 합니다.
7. 코어가 하나만 남아도 해당 단독 스킬은 사용 가능하고, 다섯 개가 모두 파괴되면 기존 사망·보상 처리가 이어져야 합니다.

공격이 안 나오면 BossController의 External Action Lock이 꺼져 있는지, 플레이어 참조가 있는지, 코어의 Skill Pool과 패턴 Owner Part가 일치하는지 확인합니다. 조합이 안 나오면 2페이즈 이상인지, 두 코어가 살아 있는지, Cooldown이 지났는지 확인합니다.

이번 단계는 스킬 배정과 실행 연결입니다. 전체 페이즈 경계 상황 검증, 나머지 미연결 원본 패턴의 취소 대응, 무기 개폐 연출, Sorting/그림자는 후속 단계입니다. 이미 발사된 독립 탄환은 코어 파괴만으로 지워지지 않으며 기존 수명대로 종료합니다.

## 자동 검증 결과

Unity 2022.3.62f3 Play Mode에서 4단계 검사 434개(후보 무작위 선택 반복 400회 포함)와 기존 HP·발사지점·사망·보상 회귀 검사 75개를 통과했습니다. 최종 실행에서 컴파일 오류와 런타임 예외는 없었습니다. 기존 미사용 필드 경고는 남아 있습니다. 기존 Transform 43개의 위치·회전·크기를 보존했습니다.

격리된 검증 씬에서 실제 컴포넌트와 패턴 코루틴을 실행했습니다. Level 1에서의 직접 조작과 난이도·화면 연출은 위 테스트 순서로 확인해 주세요.
