# Windows 설치형 테스트 배포

## 범위

- 현재 Junhan2 게임을 Windows x64 독립 실행 파일로 빌드한다. Unity 설치 없이 실행할 수 있다.
- 설치 파일은 사용자 계정의 `%LOCALAPPDATA%\Programs\24tu`에 설치하고 바탕화면/시작 메뉴 바로가기와 제거 기능을 등록한다. 관리자 권한을 요구하지 않는다.
- 로비에서 새 게임을 시작하며 기존 Insert 보스 테스트 기능도 유지한다. Steam 정식 출시 빌드는 테스트 단축키를 별도 검토한다.
- 이번 작업은 Steam 업로드, Steam/Google 로그인, 클라우드 동기화를 포함하지 않는다.
- 대표 아이콘은 사용자가 지정한 혁이 액티브 스킬 `HyukiActive.png` 원본을 그대로 사용한다. Windows 실행 파일/바탕화면 바로가기/설치 및 제거 프로그램에 동일하게 적용한다. 새 그림을 생성하거나 원본 외형을 편집하지 않는다. `encode_windows_icon.py`는 설치 프로그램용 ICO 포맷만 변환한다.

## 저장 범위 — 2026-09-29 사용자 확정

**영구 성장만 유지한다. 진행 중인 판 저장/재개는 구현하지 않는다.**

현재 영구 재화(`LobbySilverCoins`, 기존 `Coins`), 해금(`LobbyUnlock.*`), 유물 해금/장착(`RelicUnlocked_*`, `EquippedRelicId`)은 Unity PlayerPrefs에 저장된다. 그래픽/소리/접근성 설정은 `Apothecary.Settings.v2`에 저장된다.

Windows 플레이어의 저장 공간은 `HKCU\Software\DefaultCompany\24시간의사투`이며 Unity Editor 데이터와는 별도다. 회사명/제품명을 바꾸면 저장 공간이 달라지므로 이번 빌드는 기존 이름을 유지한다. 설치/업데이트/제거는 이 레지스트리나 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\24시간의사투`를 삭제하지 않는다. 저장 데이터는 설치 폴더에 넣지 않는다.

### 계정 연동 후속 설계 (미구현)

1. 영구 성장 저장을 버전이 있는 파일로 이관하고, 기존 PlayerPrefs는 1회 가져온 뒤 보존한다. 계정 전환 시 다른 계정의 데이터를 자동 이관하지 않는다.
2. Steam ID별 로컬 프로필을 분리하고, Steam Cloud를 설정한다. 인증된 Steam ID를 사용하고 가짜 ID나 테스트용 App ID를 정식 설정으로 넣지 않는다.
3. 저장 실패/손상 복구용 백업, 임시 파일 후 교체, 버전 마이그레이션, 오프라인 플레이, 동기화 충돌 및 이전 저장 복원을 테스트한다. 클라우드 접근 실패를 신규 계정으로 해석해서 기존 데이터를 덮어쓰지 않는다.
4. 그래픽/해상도 등 기기별 설정은 영구 성장 파일과 분리하고 클라우드 동기화 대상에서 제외한다.
5. Google 로그인 및 Steam↔Google 공통 진행은 별도 계정 서비스, 인증 검증, 명시적인 계정 연결/해제 및 충돌 처리 정책이 필요하다. 로그인 버튼만으로 다른 기기에서 저장이 복원되는 것은 아니다.

Steam Cloud 공식 문서: https://partner.steamgames.com/doc/features/cloud

## 재현

1. Unity 2022.3.62f3 + Windows Build Support에서 `Vampire.Editor.WindowsDesktopBuild.Run` 실행. 메뉴: `24투/Build Windows desktop test release`.
2. 기본 산출물: `Builds/WindowsDesktop/24tu.exe`와 동반 데이터 폴더. `PROJECT_GC_WINDOWS_OUTPUT`으로 다른 새 폴더 지정 가능. 전체 폴더가 게임이며 exe만 복사하면 안 된다.
3. NSIS 3.13의 `makensis.exe`를 `Tools/Distribution/Package-Windows.ps1 -Compiler <compiler path>`에 전달한다. 기존 동일 이름 설치 파일은 덮어쓰지 않는다.
4. 설치 패키지는 `Builds/Installers/24tu-Setup-0.1.20260929.exe`. SHA-256 파일도 생성된다. 새 배포마다 버전을 올린다.
5. Steam에는 설치 프로그램이 아니라 검증한 `WindowsDesktop` 실행 파일/데이터 폴더를 depot 콘텐츠로 준비하되, `*_DoNotShip`/`*.pdb`/`*.mdb`는 제외한다(로컬 설치 패키지는 자동 제외). 실제 App ID, depot ID, Steamworks 설정과 출시 검토는 별도다.

설치 패키지 제작 도구: NSIS 3.13. 이 문서에는 외부 설치 파일 다운로드 링크를 게시하지 않는다.

## 주의

- 로컬 테스트 배포본은 코드 서명되지 않았다. 외부 PC 배포 시 Windows 경고가 발생할 수 있으며 보안 설정을 끄도록 안내하지 않는다. 공인 서명/배포 검토는 출시 준비 단계다.
- 설치 파일에는 게임만 포함하며 사용자 저장, 프로젝트 원본, 개발용 비밀값은 포함하지 않는다.
- 제거는 패키지에 포함된 정확한 파일 목록만 삭제하며 설치 폴더 전체를 재귀 삭제하지 않는다. 사용자가 추가한 파일과 저장은 보존한다.
- 종료/재실행, 재설치 후 영구 데이터 유지, 다른 PC의 Unity 없는 실행은 각각 검증해야 한다. 현재 PC 실행 성공이 모든 PC 호환성 보증은 아니다.
