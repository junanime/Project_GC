Unicode True
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

!ifndef OUTPUT
  !error "Use Package-GStarWindows.ps1 to compile this installer."
!endif
!ifndef INSTALL_LIST
  !error "Missing exact installation manifest."
!endif
!ifndef UNINSTALL_LIST
  !error "Missing exact uninstall manifest."
!endif

!define APP_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\24tuGStar2026"
!define APP_NAME "24시간의 사투 지스타2026"
Name "${APP_NAME}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\24tu-GStar2026"
InstallDirRegKey HKCU "${APP_KEY}" "InstallLocation"
RequestExecutionLevel user
ManifestDPIAware True
Icon "${__FILEDIR__}\HyukiActive.ico"
UninstallIcon "${__FILEDIR__}\HyukiActive.ico"
; Per-file LZMA avoids a full solid archive expansion in the user's temporary drive.
!ifdef FAST_PACKAGE
  SetCompressor zlib
!else
  SetCompressor lzma
  SetCompressorDictSize 16
!endif
ShowInstDetails show
ShowUninstDetails show
BrandingText "24시간의 사투 · 지스타 2026 체험판"
VIProductVersion "${BUILD_VERSION}"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "24시간의 사투 지스타 2026 설치 프로그램"
VIAddVersionKey "FileVersion" "${BUILD_VERSION}"
VIAddVersionKey "LegalCopyright" "24시간의 사투"

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "지스타 체험판 설치"
!define MUI_WELCOMEPAGE_TEXT "24시간의 사투 지스타 2026 체험판을 설치합니다.$\r$\n$\r$\n설치 후 바탕화면 아이콘으로 바로 플레이할 수 있습니다.$\r$\nUnity·Steam을 별도로 설치할 필요가 없습니다.$\r$\n$\r$\n설문과 홈페이지 QR이 포함된 버전입니다.$\r$\n기존 일반 게임·페스티벌 버전과 별도로 설치됩니다.$\r$\n랭킹과 플레이 기록은 업데이트·제거 시에도 유지됩니다.$\r$\n$\r$\n실행 중인 지스타 체험판을 종료한 뒤 진행해 주세요."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\24tu-GStar2026.exe"
!define MUI_FINISHPAGE_RUN_TEXT "지스타 체험판 바로 실행"
!define MUI_FINISHPAGE_TEXT "설치가 완료되었습니다.$\r$\n바탕화면의 「24시간의 사투 지스타2026」으로 실행할 수 있습니다.$\r$\n$\r$\n운영자: 종료 화면에서 F9로 다음 참가자를 준비하고, F8로 QA 자료를 저장합니다."
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "64비트 Windows가 필요합니다."
    Abort
  ${EndIf}
  System::Call 'kernel32::CreateMutexW(p 0, i 0, w "24tuGStar2026Setup") p .r0 ?e'
  Pop $1
  ${If} $1 = 183
    MessageBox MB_ICONSTOP "지스타 설치 프로그램이 이미 실행 중입니다."
    Abort
  ${EndIf}
  ; Respect an existing installation and NSIS /D= overrides. Prefer a fixed D drive
  ; for a new default installation; the directory page remains freely editable.
  ReadRegStr $0 HKCU "${APP_KEY}" "InstallLocation"
  ${If} $0 == ""
  ${AndIf} $INSTDIR == "$LOCALAPPDATA\Programs\24tu-GStar2026"
    System::Call 'kernel32::GetDriveTypeW(w "D:\") i .r0'
    ${If} $0 = 3
      StrCpy $INSTDIR "D:\Games\24tu-GStar2026"
    ${EndIf}
  ${EndIf}
FunctionEnd

Section "게임 설치"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  ClearErrors
  !include "${INSTALL_LIST}"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "파일 복사에 실패했습니다. 실행 중인 지스타 체험판을 종료하고 저장 공간을 확인한 뒤 다시 설치해 주세요."
    Abort
  ${EndIf}
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\24tu-GStar2026.exe" "" "$INSTDIR\GStar2026.ico" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\24tu-GStar2026.exe" "" "$INSTDIR\GStar2026.ico" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\제거.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${APP_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${APP_KEY}" "DisplayVersion" "${BUILD_VERSION}"
  WriteRegStr HKCU "${APP_KEY}" "DisplayIcon" "$INSTDIR\GStar2026.ico"
  WriteRegStr HKCU "${APP_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${APP_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${APP_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegDWORD HKCU "${APP_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${APP_KEY}" "EstimatedSize" ${ESTIMATED_SIZE}
SectionEnd

Function un.onInit
  SetShellVarContext current
  ReadRegStr $0 HKCU "${APP_KEY}" "InstallLocation"
  ${If} $0 == ""
  ${OrIf} $0 != $INSTDIR
    MessageBox MB_ICONSTOP "등록된 설치 폴더와 달라 자동 제거를 중단합니다."
    Abort
  ${EndIf}
  IfFileExists "$INSTDIR\gstar-release.json" +3 0
    MessageBox MB_ICONSTOP "지스타 설치 정보를 찾지 못해 제거를 중단합니다."
    Abort
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  SetOutPath "$TEMP"
  ; Never recurse: unknown/user files and all LocalLow records remain untouched.
  !include "${UNINSTALL_LIST}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\제거.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "${APP_KEY}"
SectionEnd
