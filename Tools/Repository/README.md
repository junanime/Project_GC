# 노트북 / PC 저장소 맞추기

2026-10-25 노트북 최신 Junhan2를 기준으로 `Junhan2`, `main`, `small_test`를 같은 커밋으로 통일합니다. GitHub 브랜치가 같아져도 다른 PC의 로컬 파일은 자동으로 바뀌지 않습니다.

## PC에서 한 번 실행

1. 해당 프로젝트의 Unity를 저장하고 닫습니다. GitHub Desktop의 Pull / Commit 등 진행 중인 작업도 끝냅니다.
2. `Sync-Junhan2.cmd`와 `Sync-Junhan2.ps1`을 같은 폴더에 둡니다.
3. `Sync-Junhan2.cmd`를 실행합니다. 경로를 물으면 GitHub Desktop → Repository → Show in Explorer로 연 게임 저장소 경로를 붙여 넣습니다.
4. `DONE`과 `Changes is empty`가 표시되면 GitHub Desktop을 확인합니다. Unity는 **2022.3.62f3**을 사용합니다.

PowerShell에서 먼저 결과를 미리 확인할 수도 있습니다.

```powershell
./Sync-Junhan2.ps1 -Repository 'D:/Project_GC/Project_GC'
./Sync-Junhan2.ps1 -Repository 'D:/Project_GC/Project_GC' -Apply
```

## 무엇을 보존하고 변경하나

- 원격 주소가 `junanime/Project_GC`인지, GitHub의 세 브랜치가 같은 커밋인지 먼저 확인합니다. 서로 달라졌으면 중단합니다.
- 기존 로컬 브랜치는 `backup/device-sync-날짜/브랜치`에 보관합니다.
- 미커밋 수정과 추적 전 파일은 이름 붙은 stash에 보관합니다. 실제 수정도 버리지 않으며 최신 게임에 자동으로 섞지 않습니다.
- 복구 기록은 Git 공통 폴더 아래 `device-sync-backups/날짜/recovery.json`에 기록합니다. stash 목록을 지워도 복구할 수 있도록 별도 참조도 남깁니다.
- 세 로컬 브랜치만 원격 기준으로 맞춥니다. 다른 브랜치, 다른 worktree, `Library`, 빌드 결과, 게임 세이브, 서버는 수정하지 않습니다. 원격에 push하지 않습니다.
- 다른 worktree에서 대상 브랜치를 사용하거나 Git 병합이 진행 중이면 중단합니다.
- 완료 후에도 이전 작업은 백업에 남습니다. 필요하면 별도 복구 브랜치에서 검토하세요. 오래된 stash를 그대로 적용하면 예전 PNG 설정 변경까지 다시 생길 수 있습니다.

## PNG가 계속 변경으로 나타났던 이유

이번 노트북의 Changes는 PNG 원본이 아니라 `.png.meta` 112개였습니다. Unity가 비어 있는 YAML 값 뒤 공백과 비활성 WebGL 기본 설정(`overridden: 0`)을 다시 기록한 것입니다. 이미지 픽셀, GUID, 적용되는 압축·필터·크기 설정은 달라지지 않았습니다.

이제 Unity가 기록한 형식을 저장하고 `.meta` 줄바꿈을 LF로 고정합니다. `.meta`를 삭제하거나 무시하면 이미지 연결이 깨질 수 있으므로 PNG와 항상 함께 관리합니다. 조직 식별자는 Unity Hub가 같은 `24tu-QA` 프로젝트에 기록한 값으로 맞췄으며 Cloud Project ID는 유지합니다.

외부 도구로 새 이미지와 `.meta`를 만들 때도 해당 Unity 버전에서 가져오기를 마친 뒤 함께 커밋합니다. 비활성 플랫폼 기본값이나 YAML 공백을 매번 수동 삭제하면 다음 가져오기 때 다시 생성됩니다.

이 도구는 이번 통일용입니다. 이후 다른 기기로 이동할 때는 한 기기에서 실제 수정을 커밋·푸시한 다음 다른 기기에서 Pull합니다. 양쪽에서 서로 다른 작업이 생기면 내용을 확인한 뒤 합쳐야 하며, 이 도구로 덮어 정리하지 않습니다.
