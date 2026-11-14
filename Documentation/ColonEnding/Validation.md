# 검증 기록 — 스테이지 2 봉인 출구

Unity 2022.3.62f3, 2026-11-14, 이 PC의 실제 프로젝트와 플레이모드에서 수행했다. 자동 검사는 무적·타이머 조정·통제된 위치 이동을 사용하며 사람의 체감 난도 또는 성공률을 측정한 것이 아니다. 순위 제출은 제외했고 테스트 재화/선택 설정은 복구했다.

| 검사 | 결과 | 범위 |
| --- | --- | --- |
| ColonMapRevisionValidation.Run | PASS | 256시드, 1,536거점, 단절 지형0, 배치 오류0 |
| ColonMapRevisionValidation.RenderOnly | PASS | 최종 벽/출입구, 바깥 후광 제거 후 실제 렌더 |
| ConstipationKingVisualSmoke.RunKeyScepter | PASS | 기본·상승·내려찍기·레버·좌우반전·드랍 후 6포즈, 드랍좌표/해제 유지 |
| ConstipationKingSmoke.RunEnding | PASS 110, 오류0 | 실제 트리거 줍기/봉인해제/출구/안내/저장·복귀 두 번, 네 캐릭터 운반, 실제 Ari 재출전, 중복 완료 차단, 일시정지, 미니방 숨김, 연출 중 피해·밀림·이동·대시·스킬·포탈 차단 및 종료 뒤 피해 복구 |
| ConstipationKingSmoke.RunDeath | PASS 25, 오류0 | 전투 중 플레이어 사망, 잔여 패턴/소환 정리, 승리 오인 차단, 실패 화면 보존 |

RunEnding 최종 결과는 17:30:58, RunDeath는 17:32:09 KST에 기록됐다. 자세 캡처 이후 작은 종이 윤곽선 제거 영역과 보스 안내 문구를 마지막 보정했고 최종 플레이모드 검사에서 함께 컴파일했다.

바닥 계산 소스 1,100자 구간과 맵 지형/충돌 정의는 원본과 동일하다. 과거 캡처와 내부 바닥2,400픽셀 비교에서는2,376개가 동일하며24개에 입자/채널 차이가 있어 픽셀 전체 동일로 주장하지 않는다. 상세 비교는 `../Stage2WallDesign-20261114.md`와 관련 증빙에 기록했다.

실제 적용 화면 및 검사 결과: `Proof/`. `ApprovedWallConcept.png`와 `ApprovedLockConcept.png`는 확정 콘셉트이며 실제 화면과 구분한다. 게임용 PNG는 `Assets/Resources/ColonEnding/`에 있고 제작 모드/프롬프트는 `ArtProduction.md`에 기록했다.

## Windows 실행본

2026-11-14 17:39 KST에 Windows 64비트 빌드 PASS, 1,001,644,179바이트. 출력은 `C:/Users/zxc64/Documents/Codex/2026-11-13/pc-pc-junhan2-2-2/outputs/24tu-stage2-seal-demo/24tu.exe`다. 새 실행본을 별도 프로세스로 실행해 창 응답과 로비 오디오 시작, 시작 로그의 오류·예외 0건을 확인한 뒤 테스트 프로세스를 종료했다. 독립 실행본에서 전체 엔딩을 다시 플레이한 검사는 아니며, 엔딩 전 과정은 위 실제 에디터 플레이모드 검증을 따른다.

빌드 기록의 sourceCommit은 작업 시작 기준 `0da03ae7`이고 `localChangesIncluded=true`로 이 변경을 포함한다. 빌드 후 확정한 커밋은 실행 폴더의 `implementation-commit.txt`에 기록한다. 게임 실행에는 exe와 같은 폴더의 데이터/DLL이 모두 필요하다.

작업 전 존재하던 `OralMap/Monsters.png.meta`, `GalleryCommonCapture.cs.meta` 변경은 보존하고 이번 커밋에서 제외했다.
