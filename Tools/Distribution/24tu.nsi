Unicode True
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

; Resolve Windows packaged-host filesystem redirection before writing shell paths.
; Input/output: $0. The directory must already exist.
!macro ResolvePhysicalDirectory
  System::Call 'kernel32::CreateFileW(w r0, i 0, i 7, p 0, i 3, i 0x02000000, p 0) p .r1'
  ${If} $1 = -1
    MessageBox MB_ICONSTOP "설치 폴더의 실제 경로를 확인하지 못했습니다."
    Abort
  ${EndIf}
  System::Call 'kernel32::GetFinalPathNameByHandleW(p r1, w .r2, i ${NSIS_MAX_STRLEN}, i 0) i .r3'
  System::Call 'kernel32::CloseHandle(p r1)'
  ${If} $3 = 0
  ${OrIf} $3 >= ${NSIS_MAX_STRLEN}
    MessageBox MB_ICONSTOP "설치 경로를 확인하지 못했거나 경로가 너무 깁니다."
    Abort
  ${EndIf}
  ; Local per-user installation: remove the Win32 extended-path prefix.
  StrCpy $0 $2 "" 4
!macroend

!ifndef PAYLOAD
  !error "Pass /DPAYLOAD=<Windows build directory>"
!endif
!ifndef OUTPUT
  !error "Pass /DOUTPUT=<installer exe>"
!endif
!ifndef UNINSTALL_LIST
  !error "Generate the exact uninstall manifest using Package-Windows.ps1"
!endif
!ifndef BUILD_VERSION
  !define BUILD_VERSION "0.1.20260929"
!endif

!ifdef FESTIVAL_PACKAGE
  !define APP_NAME "24시간의사투 페스티벌 데모"
  !define APP_FOLDER "24tuFestival"
  !define APP_SHORTCUT "페스티벌데모버전"
  !define APP_REGISTRY "24tuFestivalDemo"
  !define APP_MUTEX "24tuFestivalSetup"
  !define APP_BRANDING "24시간의사투 · 45초 페스티벌 데모"
  !define APP_WELCOME "Windows PC용 45초 페스티벌 데모를 설치합니다.$\r$\n$\r$\n바탕화면에 페스티벌데모버전 실행 아이콘이 생깁니다.$\r$\n인터넷 연결 없이 게임과 행사 순위를 이용할 수 있습니다.$\r$\n기존 행사 기록과 QA 데이터는 유지합니다.$\r$\n$\r$\n함께 받은 MP4는 행사 대기용 별도 영상입니다.$\r$\n업데이트 전 실행 중인 게임을 종료해 주세요."
!else
  !define APP_NAME "24시간의사투 (테스트)"
  !define APP_FOLDER "24tu"
  !define APP_SHORTCUT "24시간의사투 (테스트)"
  !define APP_REGISTRY "24tuDesktopTest"
  !define APP_MUTEX "24tuDesktopSetup"
  !define APP_BRANDING "24시간의사투 · Windows 테스트 버전"
  !define APP_WELCOME "Windows PC용 테스트 버전을 설치합니다.$\r$\n$\r$\n바탕화면과 시작 메뉴에 실행 아이콘이 생깁니다.$\r$\n기존 재화·해금·유물·설정 데이터는 삭제하지 않습니다.$\r$\n$\r$\n현재는 로컬 저장만 지원하며 Steam/Google 로그인 및 계정 동기화는 아직 제공되지 않습니다.$\r$\n업데이트 전 실행 중인 게임을 종료해 주세요."
!endif

Name "${APP_NAME}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\${APP_FOLDER}"
RequestExecutionLevel user
ManifestDPIAware True
Icon "${__FILEDIR__}\HyukiActive.ico"
UninstallIcon "${__FILEDIR__}\HyukiActive.ico"
!ifdef FAST_PACKAGE
  SetCompressor zlib
!else
  SetCompressor /SOLID lzma
  SetCompressorDictSize 32
!endif
ShowInstDetails show
ShowUninstDetails show
BrandingText "${APP_BRANDING}"
VIProductVersion "0.1.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} 설치 프로그램"
VIAddVersionKey "FileVersion" "${BUILD_VERSION}"
VIAddVersionKey "LegalCopyright" ""

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "${APP_NAME} 설치"
!define MUI_WELCOMEPAGE_TEXT "${APP_WELCOME}"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\24tu.exe"
!define MUI_FINISHPAGE_RUN_TEXT "${APP_NAME} 실행"
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
  ; A fixed per-user location keeps development checkouts and save folders out of scope.
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\${APP_FOLDER}"
  ; Keep an existing physical installation path, including packaged-host redirection.
  ; Do not create a second installation when LOCALAPPDATA resolves differently on update.
  ReadRegStr $4 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "InstallLocation"
  ${If} $4 != ""
    ${GetFileName} "$4" $5
    ${If} $5 == "${APP_FOLDER}"
      IfFileExists "$4\24tu.exe" 0 +2
        StrCpy $INSTDIR $4
    ${EndIf}
  ${EndIf}
  System::Call 'kernel32::CreateMutexW(p 0, i 0, w "${APP_MUTEX}") p .r0 ?e'
  Pop $1
  ${If} $1 = 183
    MessageBox MB_ICONSTOP "설치 프로그램이 이미 실행 중입니다."
    Abort
  ${EndIf}
FunctionEnd

Section "게임 설치"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  ; Updates replace the existing payload in the same folder without a duplicate installation.
  SetOverwrite on
  ClearErrors
  File /r /x "*_DoNotShip" /x "*.pdb" /x "*.mdb" "${PAYLOAD}\*"
  IfErrors 0 +3
    MessageBox MB_ICONSTOP "파일 복사에 실패했습니다. 실행 중인 게임을 종료하고 저장 공간을 확인한 뒤 다시 설치해 주세요."
    Abort
  StrCpy $0 $INSTDIR
  !insertmacro ResolvePhysicalDirectory
  StrCpy $INSTDIR $0
  SetOutPath "$INSTDIR"
  File "${__FILEDIR__}\HyukiActive.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$DESKTOP\${APP_SHORTCUT}.lnk" "$INSTDIR\24tu.exe" "" "$INSTDIR\HyukiActive.ico" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\24시간의사투.lnk" "$INSTDIR\24tu.exe" "" "$INSTDIR\HyukiActive.ico" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\제거.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "DisplayVersion" "${BUILD_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "DisplayIcon" "$INSTDIR\HyukiActive.ico"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "NoRepair" 1
SectionEnd

Function un.onInit
  SetShellVarContext current
  ; Never recursively delete an installation directory, even if it was moved.
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}" "InstallLocation"
  ${If} $0 = ""
    MessageBox MB_ICONSTOP "설치 경로 등록 정보를 찾지 못해 제거를 중단합니다."
    Abort
  ${EndIf}
  !insertmacro ResolvePhysicalDirectory
  StrCmp $INSTDIR $0 +3
    MessageBox MB_ICONSTOP "설치 경로가 변경되어 자동 제거를 중단합니다."
    Abort
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  SetOutPath "$TEMP"
  ; Exact packaged files only; unknown/user files and all save data remain untouched.
  !include "${UNINSTALL_LIST}"
  Delete "$INSTDIR\HyukiActive.ico"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$DESKTOP\${APP_SHORTCUT}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\24시간의사투.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\제거.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_REGISTRY}"
SectionEnd
