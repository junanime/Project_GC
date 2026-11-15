# 스테이지 2 장벽·수동 소환·열쇠 연출 후속 검증

2026-11-14, 원본 PC의 Junhan2 작업본, Unity 2022.3.62f3. 최신 벽과 두 출입구, 빈 변기 수동 소환, 열쇠 드랍·줍기·공중 운반, 봉인 해제와 데모 복귀를 검증한다. 이전 `Validation.md` 및 `Proof/`는 최초 구현 당시 자료다.

## 보존 및 변경 범위

- 바닥 조직색·물결·입자 계산 1,100자 구간은 작업 시작 HEAD `b655cdea25cbb83189218b8f19acf24ed2cac9e2`와 정확히 같다. 모든 렌더 픽셀이 같다는 뜻은 아니다. 벽 접지 그림자와 출입구의 높이·음영은 수정했다.
- 변비왕·선택 보스의 기존 PNG 73개는 원본 바이트를 보존했다. 빈 변기와 벽 표면 PNG 두 개를 추가했고 원래 변비왕 이미지의 지팡이 분리 및 몸체 등장/소멸은 셰이더와 실제 게임 오브젝트로 처리한다.
- 기존 두 구멍의 위치·크기·상호작용 트리거는 보존했다. 대장은 보이는 벽 발치에서 이동이 막히며, 통로 일부를 넓히고 열린 출구 앞 접근로를 확보했다. 구강·위장의 경사면 규칙은 유지한다.
- 대장의 자동 15분 소환을 제거했다. 좌측하단 출구 위 빈 변기에서 언제든 상호작용한다. 처치만으로 완료하지 않고 열쇠 줍기 → 봉인 해제 → 열린 출구 상호작용 → 데모 안내 확인 순서로 한 번만 저장·복귀한다.

## 검사 결과

최종 결과는 아래 증빙과 함께 기록한다. 각 검사 횟수는 독립 assertion 수이며, 서로 다른 플레이 판 수나 사람의 플레이 횟수를 뜻하지 않는다.

| 검사 | 결과 | 확인 범위 |
| --- | --- | --- |
| ColonMapRevisionValidation.Run | PASS | 256시드, 필드 배치 22,016개, 보스 거처 1,536개, 보스 크기 바닥 단절 0, 오류 0, 최소 거처 간격 11.020 |
| ConstipationKingSmoke.RunEnding | PASS 217 | 시간 제한 없는 실제 상호작용 소환, 등장 중 보호·일시정지·안내 표시, 드랍/줍기/네 캐릭터 운반/봉인 해제/데모/저장·복귀, 재출전 후 반복, 실제 카메라 프레이밍 |
| ConstipationKingSmoke.Run | PASS 282 | 변비왕 전투·페이즈·18개 적법 패턴 조합과 출구 흐름 |
| ConstipationKingSmoke.RunDeath | PASS 35 | 사망 시 전투·소환·연출 정리, 승리 오인 차단 |
| ColonContentSmoke.Run | PASS 9,658 | 일반·엘리트·특수 몬스터, 배치·충돌·재사용 및 미니방 왕복 |
| ColonSpecialSmoke.Run | PASS 64 | 대장 신규 특수 몬스터 동작 및 정리 |
| CapsuleStageSmoke.RunChecksOnly | PASS 78 | 실제 위장 보스 처치·캡슐 탑승·대장 진입, 성장 초기화, 벽 근처 걷기·대시, 선택 보스 상호작용 |
| ColonEventProof.Run (smoke only) | PASS 22 | 이벤트 4종, 바람 벽 통과 방지, 감속 해제, 찌꺼기 1회 파괴, 실제 터치 상호작용과 경험치 획득, 임시 물체 정리 |
| ColonBossSmoke.Run | PASS 252 | 선택 보스 6종·25개 기술, 등장/처치·중복 방지·동시 체력바·상태이상·벽 안전·기존 장비 전투 |
| ColonReleasePresentation.Run | PASS 82 | 실제 이동·소환·치명타·키 상호작용·출구 해제 촬영, 도달 가능한 착지, 운반 연결부 없음, 저장/종료 없는 촬영 |

원문은 `ProofV2/*-checks.txt`, `*-result.txt`, `*-errors.txt`, 지형 결과는 `ProofV2/report.txt`에 보존한다. 구형 검사에서 바닥 여유와 회전한 발사체 크기를 이전 방식으로 측정하던 부분은 현재 의미에 맞게 보정했다. 영양분 검사는 스테이지 이동 후 경험치 기준을 다시 설정해 정상 레벨업 창이 자동 검사를 정지시키지 않도록 했다. 실제 이동·피해·상호작용·경험치 지급 검사는 유지했다.

소환 안내가 이미 표시 중인 필드 이벤트 문구 뒤에서 대기하는 문제를 실제 HUD에서 재현했다. 수동 소환 시 기존 즉시 안내 경로를 사용하고, 소환이 소유한 필드 정지 중에는 안내의 표시 시간이 진행되도록 수정했다. 최종 엔딩 검사는 19:34:49 KST에 PASS 217로 완료했으며, 안내 제목·투명도와 두 번의 출전/복귀를 확인했다.

촬영은 19:36 KST에 완료했다. GIF 8개는 총 441개 원본 프레임을 누락 없이 보존하며 960×540, 12fps, 반복 재생이다. 각 GIF의 프레임 수·시간·용량은 `ProofV2/GIF-manifest.json`, 게임 시간과 실제 Unity 프레임 번호는 `ProofV2/Presentation-frame-times.csv`에 기록했다. 촬영 전후 실제 상호작용 트리거 준비 시간은 클립 사이에 포함하지 않는다.

검수 후 재화·QA 기록 등 게임 설정을 작업 전 백업과 대조해 복구했다. Unity 자체 세션 ID/횟수만 변경되어 있으며 독립 실행본 설정은 변경되지 않았다. 작업 전부터 수정되어 있던 `OralMap/Monsters.png.meta`와 `GalleryCommonCapture.cs.meta`는 보존하고 커밋에서 제외한다.

## 실제 적용 화면

- [전체 맵](ProofV2/full-map.png)
- [빈 변기](ProofV2/empty-throne.png), [실제 소환 HUD](ProofV2/summon-actual-camera-hud.png)
- [손에서 분리되는 열쇠](ProofV2/boss-key-drop.png), [바닥에 꽂힌 열쇠](ProofV2/planted-key.png), [공중 운반](ProofV2/floating-key.png)
- [닫힌 봉인](ProofV2/sealed-exit.png), [자물쇠 파괴](ProofV2/lock-fracture.png), [열린 출구](ProofV2/open-exit.png)
- [닫힌 입구](ProofV2/inlet-closed.png), [열린 입구](ProofV2/inlet-open.png)
- [실제 플레이 카메라의 열쇠 비행](ProofV2/first-clear-key-drop-actual-camera-hud.png), [봉인 해제](ProofV2/first-clear-unlock-actual-camera-hud.png), [열린 출구](ProofV2/first-clear-open-actual-camera-hud.png)
- [Notion 게임설명·GIF 갤러리](https://app.notion.com/p/416636e6d18f8101a7ebd87d61f4a0ce)

## 촬영 및 검증 한계

GIF는 Unity 플레이모드의 실제 오브젝트와 애니메이션을 12fps로 촬영한다. 생성 이미지로 만든 가짜 애니메이션이 아니다. 전체 맵은 배치물을 포함하며, 상세 연출은 잘 보이도록 화면 HUD와 주변 배치물을 숨기고 일반 스폰·자동 공격·필드 이벤트를 정지한 촬영 구도다. 별도 실제 플레이 카메라/HUD 검사는 열쇠 비행·봉인 해제·열린 출구가 화면 안에 들어오는지 확인한다.

자동 검수에는 무적, 시간·위치 제어 및 강제 피해를 사용했다. 사람의 체감 난도·완주율·장시간 실기 플레이 또는 웹/모바일 실행 성능 검증으로 해석하지 않는다. 순위 제출은 제외했다. 이번 후속 작업에서 Windows 실행본을 새로 만들거나 기존 웹 링크를 배포하지 않는다. Junhan2 소스 푸시는 웹 배포와 별개다.
