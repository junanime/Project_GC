# Steam 설치·저장·랭킹 연결

2026-10-26 기준. **Steamworks 심사 중이며 실제 App ID가 아직 없다.** 게임 코드와 배포 도구를 준비한 상태이고, Steam 업로드·Steam Cloud 활성화·Steam 실계정 로그인 검증은 완료되지 않았다. 기존 웹 QA의 링크나 서버 설정은 이 작업에서 변경하지 않는다.

## 심사 완료 후 재개 메모

사용자는 2026-10-26에 설정값과 업로드 절차를 보관하고, 나중에 "심사 끝났어", "이제 Steam에 등록하자"라고 말하면 채팅에서 다시 안내해 달라고 요청했다. 재개할 때 이 문서의 Steamworks·Unity Cloud·Steam Cloud 설정 표와 업로드 순서를 채팅에 다시 제공한다. 실제 적용 전에는 공식 안내의 변경 여부와 발급된 App ID·Windows Depot ID를 확인한다.

준비 코드 기준 커밋은 `b38f23a`이다. 실제 Steam 인증·업로드·Cloud 동기화는 아직 미완료이며, 웹판은 계속 Steam 기능 없이 기존 브라우저 저장과 Unity 랭킹을 사용한다. 심사 대기 중에도 게임 콘텐츠 개발과 로컬 테스트는 진행할 수 있다. Publisher Web API 키 등 비밀값은 이 문서나 채팅에 저장하지 않는다.

## 역할

| 기능 | 담당 | 현재 상태 |
| --- | --- | --- |
| 게임 설치·업데이트 | SteamPipe / Steam 클라이언트 | Windows Steam 전용 빌드와 depot 준비 도구 구현. 실제 App ID·Windows depot ID 발급 대기 |
| 영구 성장 동기화 | Steam Auto-Cloud | 계정별 파일 저장 구현. Steamworks의 저장 경로·용량 설정과 실제 PC 간 동기화 검증 대기 |
| 별명·한 판 최고점·전체 순위 | 기존 Unity Authentication + Cloud Code + Leaderboards | 기존 서버 유지. Steam 인증 연결 코드 구현. Unity Steam 인증 공급자 설정 대기 |
| 전투 | 설치된 게임에서 실행 | 전투를 처리하는 별도 상시 서버는 추가하지 않음 |

Steam Cloud는 별도 사이트에 로그인하는 서비스가 아니라 Steamworks에서 게임별로 설정한다. Unity Cloud 로그인은 별도로 필요하다. App ID 없이 임시 ID나 Spacewar(480)를 정식 게임 설정에 넣지 않는다.

## 게임 동작

- 일반 Windows 테스트판·Unity Editor·웹판: 기존 PlayerPrefs 저장과 Unity 익명 계정을 그대로 사용한다.
- Steam Windows 전용판: Steam 라이브러리 실행·소유권·App ID·Steam 계정을 확인한다. 확인 실패 시 기존 테마의 안내 화면을 표시하며 다른 계정으로 임의 진행하지 않는다.
- Steam 계정은 전용 Unity 인증 프로필로 로그인한다. Steam Web API ticket을 콜백으로 받은 뒤 `SignInWithSteamAsync`에 전달하고 사용 후 해제한다. 로그인 실패를 익명 계정 생성으로 우회하지 않는다.
- 랭킹 연결 실패/오프라인 상태에서도 로컬 플레이는 가능하다. 기존 규칙대로 서버에서 시작을 확인하지 못한 판은 랭킹에 등록하지 않는다. 시작을 확인한 판의 전송 실패는 기기별 보류 기록으로 재시도한다.
- QA 익명 계정과 Steam 계정은 자동 병합하지 않는다. Steam 별명·순위는 인증된 동일 계정으로 다시 조회한다. 기존 QA 보드와 점수는 삭제하지 않는다.
- 실행 중 Steam 계정이 바뀌면 진행을 멈추고 재실행을 안내한다.

### 저장 범위

영구 재화, 캐릭터·침·유물 해금, 유물 장착, 캐릭터별 시작 침과 침 활성화 선택, 튜토리얼 확인 기록을 저장한다. **진행 중인 판 저장/재개는 제공하지 않는다.**

```text
%USERPROFILE%/AppData/LocalLow/DefaultCompany/24시간의사투/
  SteamProfiles/<실제 Steam64ID>/
    progress.json
    progress.previous.json
```

파일은 계정 소유자·스키마 버전·저장 시각·리비전과 허용된 성장 값만 포함한다. 임시 파일을 쓴 뒤 교체하고, 이전 정상 파일을 백업한다. 손상된 본 파일은 정상 백업으로 복구하되 손상 원본도 별도 보관한다. 정상 파일이 없거나 더 최신 버전의 저장 형식이면 조용히 초기화하지 않고 실행 안내를 표시한다. 자동 복구는 직전 저장까지 되돌아갈 수 있다.

Steam으로 처음 실행했을 때 이 PC의 기존 테스트판 기록이 있으면 가져오기/새로 시작을 선택한다. 가져오기는 성공적으로 파일을 저장한 뒤에만 이 PC의 이전 기록을 가져온 계정을 기록한다. 원래 PlayerPrefs는 지우지 않으며, 다른 Steam 계정으로 자동 복제하지 않는다. 이미 Steam 저장이 있는 계정에는 이 가져오기 창이 나오지 않는다.

음량·해상도 등 기기 설정, Unity 로그인 토큰, 랭킹 캐시·전송 보류 기록은 클라우드 저장에서 제외한다. Steam Cloud 충돌 선택은 Steam 클라이언트가 담당하며 게임 안에서 서로 다른 두 PC의 기록을 자동 병합하지 않는다.

## App ID 발급 후 설정

### 1. Steamworks

게임 App ID와 Windows depot ID를 확인한다. 실행 파일은 `24tu.exe`, 플랫폼은 Windows 64-bit다. 출시 검토 전에는 비공개 테스트 브랜치와 테스트 권한으로 확인한다.

Auto-Cloud 설정:

| 항목 | 값 |
| --- | --- |
| Root | `WinAppDataLocalLow` |
| Subdirectory | `DefaultCompany/24시간의사투/SteamProfiles/{64BitSteamID}` |
| Pattern | `progress*.json` |
| Recursive | 끔 |
| OS restriction | Cross-Platform |
| Byte quota per user | 1,048,576 (1 MiB) |
| Files per user | 10 |
| Dynamic Cloud Sync | 끔 |

실제 동기화 대상은 본 파일과 이전 정상 파일 2개이며 각 최대 256 KiB다. 일시 파일·손상 원본·로그는 패턴에 포함하지 않는다. 현재 지원하는 저장 루트는 Windows뿐이다. Linux/macOS 플레이어를 추가한다면 해당 OS의 루트 재지정을 추가 검증해야 한다. Steamworks 변경 사항은 저장/게시 후 실제 Steam 클라이언트로 확인한다.

### 2. Unity Cloud의 Steam 인증 공급자

- 기존 프로젝트: `7a0e70db-8cdd-4ee3-883b-3dda34673dcc`, 환경: `production`.
- Authentication의 Steam 공급자에 실제 App ID와 해당 게임용 Steam Publisher Web API key를 설정한다.
- Publisher key는 관리 페이지에 직접 입력한다. 채팅·게임 클라이언트·저장소·빌드 환경 파일에 넣지 않는다.
- 클라이언트의 ticket identity는 `unityauthenticationservice`이다. ticket 발급과 Unity 로그인 요청에서 동일하게 사용한다.
- 기존 Cloud Code `QaRun`, 최고점 보드 `qa_s1_v1`, 점수 계산·직접 쓰기 차단 정책은 유지한다. Steam 계정도 같은 서버 검증 경로로 제출한다.
- 현재 서버는 제출된 카운터와 제한·시작 티켓을 검증한다. 서버에서 전투 전체를 재현하는 완전한 부정행위 방지 시스템은 아니다.

### 3. Windows Steam 빌드

Steamworks.NET UPM 의존성은 `2025.164.1`로 고정했다. 패키지가 지연 실행으로 전역 컴파일 설정을 바꾸지 않도록 `SteamworksNETSettings.json`에서 자동 기호 관리를 껐다. 프로젝트 전체에 `GC_STEAM`을 추가하지 않는다. 전용 빌드만 이 기호와 실제 App ID 리소스를 넣고, 빌드가 끝나면 임시 리소스를 제거한다. **WebGL은 Steam 인증·Cloud 저장·네이티브 라이브러리를 사용하지 않는다.**

```powershell
./Tools/Distribution/Build-SteamWindows.ps1 `
  -AppId <실제_App_ID> `
  -UnityPath 'D:/GameDev/Unity/Editors/2022.3.62f3/Editor/Unity.exe' `
  -OutputPath '<새_Windows_빌드_폴더>'
```

또는 `PROJECT_GC_STEAM_APP_ID`, `PROJECT_GC_WINDOWS_OUTPUT`, `PROJECT_GC_SOURCE_COMMIT` 환경값을 지정하고 Unity 메뉴 `24투/Steam/Build Windows Steam release`를 실행한다. 일반 테스트 빌드 메뉴는 Steam 로그인 없이 실행하는 기존 버전을 만든다.

### 4. SteamPipe 콘텐츠 준비·검사·업로드

```powershell
./Tools/Distribution/Prepare-SteamDepot.ps1 `
  -BuildPath '<검증한_Steam_Windows_빌드_폴더>' `
  -OutputPath '<새_SteamPipe_준비_폴더>' `
  -AppId <실제_App_ID> -DepotId <실제_Windows_Depot_ID>
```

이 도구는 App ID 일치·실행 파일·네이티브 라이브러리·라이선스를 확인하고, 개발용 `steam_appid.txt`, 디버그 심볼·로그·`*_DoNotShip`을 제외한다. 콘텐츠별 SHA-256, 미리보기/업로드 VDF, Cloud 설정 기록을 생성한다. 기존 폴더를 덮어쓰지 않는다.

Steamworks SDK의 SteamCMD에서 업로드 권한이 있는 계정으로 로그인하고 암호/Steam Guard는 직접 입력한다. 암호를 명령 인자나 파일로 저장하지 않는다. 먼저 다음 콘솔 명령으로 검사한다.

```text
login <업로드_계정>
run_app_build <준비_폴더>/scripts/app_build_preview.vdf
```

검사 결과를 확인한 뒤 `app_build_upload.vdf`를 사용하면 빌드를 업로드한다. 두 VDF 모두 `SetLive`를 포함하지 않는다. 업로드만으로 공개 버전이나 기본 브랜치가 바뀌지 않으며, Steamworks에서 테스트 브랜치에 지정하고 실행·업데이트를 검증한 뒤 공개 여부를 결정한다. 설치 프로그램이 아닌 실행 파일과 전체 데이터 폴더를 depot으로 보낸다.

## 검증과 남은 확인

로컬 검증은 `Vampire.Tests.Editor.SteamPreparationChecks.Run`, `Vampire.Tests.Editor.SteamCompilationChecks.Run`, `Tools/Distribution/Test-SteamDepot.ps1`, `CloudCode/QaRun.test.cjs`, 기존 `LeaderboardSmoke`를 사용한다. 테스트용 패키징 ID는 실행 가능한 게임에 넣거나 업로드하지 않는 격리 텍스트 파일에서만 사용한다.

2026-10-26 확인 결과:

- 저장·복구 검사 **34개 통과**: UTF-8 데이터 재시작, 계정 분리, 손상 복구, 미래 스키마 보호, 저장 크기/항목 제한, 기존 PlayerPrefs 호환, 비성장 정보 제외.
- SteamPipe 준비 검사 **20개 통과**: 미리보기/업로드 분리, 자동 공개 없음, 파일 해시, 개발 파일 제외, App ID 불일치·잘못된 경로·누락 라이브러리 차단.
- 기존 서버·게임 플레이 검사 **27개 통과**: 별도 `Validation_Only` 보드에서 최고점 유지·계정별 조회·직접 점수 쓰기 차단·별명 저장과 실제 전투/결과 화면 확인.
- `QaRun.test.cjs` 통과: 점수 가중치, 잘못된 입력, 시작 티켓, 재시도·복구, 최고점과 검증 전용 보드 분리.
- 플레이어 코드 컴파일 통과: Steam Windows **49개 어셈블리 / Steam 포함**, WebGL **47개 / Steam 제외**. Steam 네이티브 플러그인도 WebGL 제외 설정 확인.
- 일반 Windows 테스트판 전체 빌드 성공: **908,289,907 bytes**, Unity 2022.3.62f3, LZ4HC. 네이티브 라이브러리와 MIT 라이선스 동봉 확인. 이 호환성 확인용 빌드는 `steamEnabled=false`이며 Steam 업로드용 빌드나 배포본이 아니다.
- 처음 플레이 검사 도중 SDK의 지연 컴파일 설정 변경이 실행을 방해했다. 패키지의 전역 기호 자동 관리를 끈 뒤 새 프로세스에서 전체 27개를 다시 통과했다.

검사 로그는 개발 PC의 `D:/Project_GC/work/SteamPreparation-20261026`에 보관한다. 이 결과가 실제 Steam 계정 로그인·Steam Cloud 동기화까지 검증했다는 뜻은 아니다.

App ID와 공급자 설정 이후 반드시 확인할 항목:

1. Steam 라이브러리 설치·실행·비공개 브랜치 업데이트와 저장 유지.
2. 같은 Steam 계정으로 노트북 → PC에서 성장 기록·별명·순위 복원.
3. 다른 Steam 계정으로 전환했을 때 성장·인증·보류 점수가 섞이지 않음.
4. Steam 오프라인 플레이 후 온라인 복귀, Cloud 충돌 안내, 손상 파일 복구.
5. 기존 QA 기록 가져오기와 새로 시작을 각각 검증하고 QA 원본이 남는지 확인.
6. Steam ticket 실제 로그인·닉네임 저장·한 판 기록 제출·최고점 유지.

공식 문서: [SteamPipe](https://partner.steamgames.com/doc/sdk/uploading), [Steam Cloud](https://partner.steamgames.com/doc/features/cloud), [Unity Steam 인증](https://docs.unity.com/en-us/authentication/platform-signin/steam), [Steamworks.NET](https://steamworks.github.io/installation/).
