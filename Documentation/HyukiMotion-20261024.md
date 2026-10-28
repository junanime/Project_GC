# 혁이 확정 외형과 모션 — 2026-10-24

승인한 A 외형과 작은 보폭을 적용했다. 기준은 기존 인게임 몸 높이 약 0.59이며, 걷기 16프레임/0.96초, 추워하는 대기 16프레임/0.8초다. 출전준비와 타이틀은 같은 대기 프레임을 사용한다.

## 사용처

| 동작 | 실제 사용 자산/연결 |
|---|---|
| 이동 | CharacterDesigns/Hyuki_ApprovedWalk, 캐릭터 Blueprint |
| 대기·스킬 중 몸·기존 결과 대기 | CharacterDesigns/Hyuki_ApprovedIdle, Blueprint |
| 얼음 대쉬 | CharacterDesigns/Hyuki_ApprovedDash 8프레임, 기존 대쉬 시간에 맞춤 |
| 함정에 붙잡힘 | CharacterDesigns/Hyuki_ApprovedCaptured |
| 혈전 진입·퇴장, 스테이지 등장 | Resources/BloodClotTravel/HyukiApproved |
| 캡슐 탑승 | 새 대기 스프라이트의 몸 영역을 사용하는 기존 CapsulePose |
| 얼어붙는 퇴장·실패 결과 그림 | Resources/CharacterExits/hyuki |
| 로딩 뒷모습 | Resources/StageEntryLoading/HyukiApproved, StageEntryLoadingArt |

프로필·스킬 아이콘·고귀 증강 그림은 별도의 삽화다. 전투 스킬의 봉황/눈보라는 캐릭터 몸을 대체하지 않고 별도 렌더러로 표시한다.

## 크기와 마감

- 게임 모션은 512픽셀 셀, 공통 PPU 약 730.9323과 동일한 발 기준 피벗을 사용한다. 매 프레임 그림을 꽉 채우도록 늘리지 않는다.
- 머리 크기를 기준으로 새 전용 자세를 보정했다. 웅크림·발 들기·팔 벌림에 따른 윤곽 변화는 동작의 일부이며, 캐릭터 전체 배율을 바꾸지 않는다.
- 얼음 대쉬 바닥 효과는 몸 이미지에서 분리했다. 얼음/입김/투명 여백이 몸의 크기 계산에 들어가지 않는다.
- 혈전 이동에서 투명 셀의 높이로 재확대하던 처리를 제거했다. 기존 진입/퇴장 경로와 무적 규칙은 유지한다.
- 퇴장 시 몸은 고정하고 얼음만 바닥에서 자란다. 얼음 상단을 수평으로 잘라 노출하던 임시 합성은 제거했다.
- Point 필터, 공통 캔버스, 투명 PNG를 사용한다. 붙잡힘 1장·혈전 5장·대쉬/뒷모습 각 8장으로 중복 프레임 저장을 줄였다.

## 검증

`HyukiApprovedMotionSmoke.Run`은 실제 Main Menu → 출전준비 → Level 1 진입 뒤 이동, 좌우 전환, 대쉬, 액티브, 함정, 혈전 진입/퇴장, 얼어붙는 종료를 실행한다. 프레임 사용, 공통 배율/피벗, 동작 복귀와 출전준비 재생을 확인한다. 화면과 모션 프레임은 `Library/HyukiApprovedMotionProof`에 저장된다.

최종 에디터 실행은 57개 항목 통과, 오류 로그 0건. 70개 실제 프레임의 불투명 영역이 셀 가장자리에서 최소 8픽셀 이상 떨어져 있음을 별도 확인했다. PC 브라우저와 모바일 터치 에뮬레이션 검증은 웹 배포 기록에 분리한다.

`HyukiApprovedMotionInstaller.Install`은 완성된 시트를 재연결한다. 원본에서 재패킹하려면 `Run -hyukiMotionSource <승인된 16프레임 폴더> -hyukiActionSource <dash/captured/curl/idle-pose/rear.png 폴더>`를 사용한다. 보행·대기 원본과 생성한 전용 자세는 작업 폴더의 `output/hyuki-walk-review-20261024`에 보관했다.
