# 웹 데모 · 2026-10-24

## 최신 배포 · 모바일 입력 / 웹 오디오 복구

- 주소 유지: https://breath-michigan-employers-arena.trycloudflare.com. 기존 서버와 터널을 재시작하지 않고 활성 빌드 경로만 교체했다. PC·모바일 URL 경로도 유지하므로 해당 주소의 브라우저 저장 데이터를 그대로 사용한다.
- 게임 소스: `10ec95e059f190f43138ea058214f7b00132c6a5` (`Junhan2`). 이후 검사 도구·문서 정리 커밋은 게임 실행 코드를 바꾸지 않는다.
- PC: `Builds/WebDemo-PC-20261024-input-audio`, DXT Crunch 264개, 563,977,921 bytes (약 564 MB).
- 모바일: `Builds/WebDemo-Mobile-20261024-input-audio`, ASTC 4×4 753개, 402,822,969 bytes (약 403 MB).
- 양쪽 모두 Stage 1 전용. Stage 2 맵·몬스터 제외를 BuildReport로 검증했다. 캡슐 보트 이동/좌측 하단 출구/데모 안내/메인 복귀는 유지한다.
- 조이스틱의 실제 터치 입력 보완, 브라우저 좌표 보정, 손가락 소유권, 키보드 입력과의 충돌, 화면 회전·백그라운드 복귀를 보강했다. 대쉬·액티브 동시 조작을 검사했다.
- 원본 음원 128개를 PC·모바일 빌드의 SHA-256 목록과 대조했다. 소스 파일·샘플레이트·채널을 유지하며, 웹에서는 최고 품질 AAC/사전 디코딩을 사용한다. 무손실 PCM과 동일하다는 의미는 아니다. 제작용 오디오·이미지 임포트 설정은 빌드 후 복원한다.
- 메인/출전준비 무음 복구, 오디오 허용·디코딩 이후 재생, 증강 아래 필드 음악 중복 재개 방지. 증강 선택 연출을 준비 완료 후 1.5초로 고정하고 튜토리얼 제목 영역과 구형 지도 누락 스크립트를 정리했다. 밸런스 수치는 변경하지 않았다.
- 실제 Unity 씬 검사 23개 진입점 통과. 입력·오디오 결합 34항목, 5종 보스/위산/상태이상/아이템/필드 배치/진행/Validation_Only 랭킹을 포함한다. 상세 범위: `WebInputAudio-20261024.md`.
- 새 PC 웹에서 인트로 스킵, 메인, 출전준비, 전투 이동·대쉬·액티브, Tab 지도 확대, 설정 음소거/복구, 증강 선택을 확인했다. 재생 로그에서 로비·필드·증강 음악은 Loaded/Pitch=1. 초기 음원 길이 조회 메시지 36건과 Unity 브라우저 경고 2종(3회)은 남았으나, 해당 검사 로그에 로딩 전 재생/디코딩 실패/브라우저 오류는 없었다.
- 실제 휴대폰 및 Safari의 조작·청음은 수행하지 않았다. 모바일은 Unity Touchscreen 입력 검사, 브라우저 연결 코드 검사, 빌드 파일/서버 응답 검사로 검증했다. 이를 실기기 전수 검증으로 표현하지 않는다.
- 외부 HTTPS의 선택 화면, PC/모바일 빌드 정보, 8개 로더·데이터·실행 파일 응답 200과 새 소스 버전을 확인했다. 원본 음원 목록/프로젝트 파일 비공개 응답도 확인했다. 기존 빌드는 진행 중인 클라이언트용으로 보존했다.
- 증거: 작업 폴더 `work/WebInputAudio-20261024`의 `results.json`, `staging-validation.txt`, `release-verification.json`, `pc-browser-log.json`, `public-preparation.png`. 검증 시각: 2026-10-24T10:45:02.070Z.

## 이전 배포 · 혁이 모션 통일 / 신이 상하 불 뿜기

- 현재 주소: https://breath-michigan-employers-arena.trycloudflare.com
- 게임 소스: `1f5e27ff6676228a40445a7b6c1ebde888238109` (`Junhan2`). 혁이의 승인 외형·대기·이동·대쉬·이동 연출을 연결하고 신이 상하 전용 불 뿜기 자세를 추가했다. 신이 좌우 자세와 불 이미지는 유지한다.
- PC 빌드: `Builds/WebDemo-PC-20261024-hyuki-shini`, DXT Crunch 264개, 563,963,156 bytes (약 564 MB).
- 모바일 빌드: `Builds/WebDemo-Mobile-20261024-hyuki-shini`, ASTC 4×4 753개, 402,558,181 bytes (약 403 MB).
- 두 빌드 모두 Stage 1 전용이며 BuildReport에서 Stage 2 맵·몬스터 제외를 확인했다. 제작용 원본 텍스처 설정과 대장 Resources는 복원했다.
- 에디터 실제 플레이 검증: 혁이 57항목 / 신이 105항목 통과. 프레임 가장자리 잘림, 공통 몸 배율, 방향별 부리 발사점, 피해 판정과 모션 복귀를 확인했다. 상세 내용은 `HyukiMotion-20261024.md`, `ShiniVerticalBreath-20261024.md`에 기록한다.
- 배포 서버 16항목, 기기 선택 페이지 48항목, PC 빌드 정적 검사 통과. 실제 PC 웹에서 인트로·메인·혁이 출전준비·전투·종료, 모바일 웹에서 인트로·신이 출전준비·전투 진입·액티브/대쉬 표시·증강 선택을 확인했다. 모바일은 Chromium 터치/기기 에뮬레이션이며 실기기 Safari 성능 검증은 아니다.
- 외부 HTTPS 루트, PC/모바일 빌드 정보, 모바일 페이지 및 새 데이터/실행 파일 HEAD 응답 200. 두 공개 빌드의 소스 커밋과 Stage 1 제한이 위 내용과 일치한다.
- 이전 `touch-knives-won-dispatched` 주소는 만료되어 교체했다. 호스트 PC와 터널이 실행 중인 동안 접속 가능하다. 주소가 달라 이전 주소의 브라우저 저장 데이터는 자동 이전되지 않는다.
- QA 증거: 작업 폴더 `work/web-motion-qa/pc-final`, `mobile-final` 및 `output/shini-vertical-breath-20261024`, `output/hyuki-walk-review-20261024/applied-review`. 신규 계정 개인 랭킹 조회의 기존 404 응답 외에 이번 실제 브라우저 검사에서 pageerror나 충돌은 발견되지 않았다.

## 공개 범위와 종료

PC·모바일은 스테이지 1까지 공개한다. 기존 보스 도주/보상, 캡슐 낙하와 최초 한의사 튜토리얼, 해치 탑승, 육지·위산 수면 이동, 좌측 하단 구멍 열림을 유지한다. 목적지 도착 후 아래 문구를 하단 스티치 대화창에 표시한다.

> 데모버전은 스테이지 1까지만 공개됩니다!

`메인화면으로` 확인 → 2초 나선 닫힘 → 메인화면. 대화창 중에는 전투·게임 시각을 정지하고 버튼 중복 입력을 무시한다. 성공 기록·획득 재화 저장은 한 번만 처리한다. 개발판의 Stage 2 진행 경로는 유지한다.

`WebDemoBuild.Run`은 항상 `PROJECT_GC_STAGE_ONE_ONLY`로 빌드한다. `WebDemoStageScope`가 ColonMap/ColonMonsters Resources를 일시적으로 Editor 폴더로 옮기고 빌드가 끝나면 경로/GUID를 복원한다. BuildReport에 두 폴더가 포함되면 빌드를 실패 처리한다. 중단된 작업은 `24투/Restore browser build stage resources`로 복원 가능하다.

## PC / 모바일 안내

| 동작 | PC | 모바일 |
|---|---|---|
| 이동 | WASD / 방향키 | 왼쪽 조이스틱 |
| 대쉬 | Shift | 우측 하단 대쉬 |
| 액티브 | R (신이 길게 누르기) | 대쉬 왼쪽 액티브 (신이 길게 누르기) |
| 상호작용 | 근처에서 E | 근처 NPC·오브젝트 직접 터치 |
| 아이템 | 1–4 / ZXCV | 중앙 하단 보관함 |
| 정보 / 설정 | Tab / Esc | 우측 상단 톱니바퀴 |
| 지도 | 휠 확대·축소, 드래그 이동 | 두 손가락 확대·축소, 드래그 이동 |

기기 선택 웹 페이지·PC/모바일 로딩 화면·동봉 사용 안내를 함께 갱신한다. 모바일에 별도 상호작용 버튼이 있다는 안내는 사용하지 않는다.

## 패키징

PC DXT Crunch / 모바일 ASTC 4×4 / gzip. 이미지 해상도·스프라이트 분할·필터를 유지하며 작은 도트 이미지는 무손실로 둔다. 원본 import 설정은 빌드 후 복구한다. 모바일 전용 스트리밍 압축 해제 및 Unity Services callback 호환 보정을 유지한다. 생성 빌드는 Git에 포함하지 않는다.

## 검증

- `Vampire.Tests.Editor.WebDemoEndingSmoke.Run`: 실제 Level 1 PC/터치 설정 두 경로. 캡슐 낙하/탑승/이동/출구 열림, 정확한 문구, Stage 2 미진입, 시간 정지, 성공 기록, 확인 중복, 로비 복귀와 런타임 제거 확인. Stage 2 리소스 임시 제외/원래 GUID 복원 확인.
- 모바일 HTML 17항목, 압축 다운로드 메모리 10항목, Unity callback 호환 검사 통과.
- 기기 선택 HTML 48항목 통과. 이 검사는 게임 로더를 대체한 웹 페이지 검사다.
- 실제 컴파일된 PC 웹에서 인트로·출전 준비·전투·증강 선택을 확인했다. 안드로이드/아이폰 User-Agent·터치 에뮬레이션의 실제 모바일 빌드 출전/전투 진입도 통과했다. 휴대폰 실기기 및 실제 Safari의 성능 테스트는 하지 않았다.


## 이전 배포 결과 · 697f74d

- 이전 주소(만료): https://touch-knives-won-dispatched.trycloudflare.com
- 기존 `https://gps-moms-studying-mai.trycloudflare.com` 주소는 DNS 만료로 사용할 수 없어 새 임시 주소를 발급했다. 호스트 PC와 터널이 실행 중인 동안 접속 가능하다. 주소 변경으로 이전 사이트의 브라우저 저장 데이터가 자동 이전되지는 않는다.
- 게임 코드: `697f74dfb5e96cb3d9c6d45a5305bceddf567deb`. 빌드 당시 작업 트리를 해당 커밋과 확인한 뒤 배포 메타데이터에 기록했다. 빌드 시작 기준 커밋은 별도 `buildBaseCommit`으로 보존했다.
- PC: DXT Crunch 대상 258개, 실제 참조 파일+StreamingAssets 다운로드 564,520,883 bytes (약 564.5 MB).
- 모바일: ASTC 4×4 대상 749개 (비정형 크기 491개 포함), 실제 다운로드 400,731,746 bytes (약 400.7 MB). 모바일 안내는 올림값 401 MB로 표시한다.
- 두 빌드 모두 `stageCount=1`, `stageTwoAssetsExcluded=true`. Unity BuildReport에서 대장 맵/몬스터 없음 확인. 원본 대장 Resources와 texture import 설정은 복원했다.
- 외부 HTTPS 루트·PC 빌드 정보·모바일 빌드 정보·모바일 페이지 모두 HTTP 200 및 정확한 소스/스테이지 범위 확인.
- 종료 회귀 최종 37항목 통과, 오류 0. PC/터치 각각 획득 재화 저장과 중복 완료 방지까지 검증했다.
- 배포 서버 16항목, 최신 안내 페이지 48항목, 모바일 로딩 페이지 17항목, 메모리 스트리밍 10항목, Unity Services callback ABI 검사 통과.
- 실제 웹 QA에서 신규 계정의 개인 랭킹 조회 404는 아직 점수가 없는 플레이어 응답이다. 게임 로딩 실패가 아니다. 기존 MapPanelRoot/MapViewport의 누락 스크립트 경고와 Unity Web API 경고는 남아 있으며, 이번 검사에서 Unity 실행 예외나 브라우저 pageerror는 없었다.
- 노션: [통합 문서](https://app.notion.com/p/341636e6d18f807594a2ec7c596e2c7f), [위장](https://app.notion.com/p/402636e6d18f8154a92aff6dd54283f7), [캡슐 이동](https://app.notion.com/p/402636e6d18f8147a10ced9d0e28e46f). 후반 몬스터 수치·스폰표·걷기 GIF, 웹 데모 종료 화면, PC/모바일 조작과 개발판/공개판 차이를 기록한다.
