# One-time, backup-first alignment of this game's three working branches.
# Without -Apply this script only fetches and reports. It never pushes.
[CmdletBinding()]
param([string]$Repository, [switch]$Apply)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$targets = @('Junhan2', 'main', 'small_test')
$expectedOrigin = '^(https://github\.com/|git@github\.com:)junanime/Project_GC(\.git)?/?$'

function Invoke-RepoGit {
    param([string[]]$ArgumentList)
    $result = @(& $script:GitExe -C $script:Repo @ArgumentList)
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($ArgumentList -join ' ')" }
    return ($result -join "`n").TrimEnd()
}

try {
    $command = Get-Command git -ErrorAction SilentlyContinue
    if ($command) { $script:GitExe = $command.Source }
    else {
        $gitCandidates = @(
            "$env:ProgramFiles/Git/cmd/git.exe",
            "$env:LOCALAPPDATA/Programs/Git/cmd/git.exe"
        )
        $gitCandidates += @(Get-ChildItem "$env:LOCALAPPDATA/GitHubDesktop/app-*/resources/app/git/cmd/git.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | ForEach-Object FullName)
        $script:GitExe = $gitCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (!$script:GitExe) { throw 'Git was not found. Install Git for Windows or add Git to PATH, then run again.' }
    }
    if (!$Repository) {
        # The installed copy in Tools/Repository knows its repository. A downloaded
        # copy asks for the path instead of guessing among old project copies.
        $nearby = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
        if (Test-Path -LiteralPath (Join-Path $nearby '.git')) { $Repository = $nearby }
        else { $Repository = (Read-Host 'Paste the Project_GC repository path shown in GitHub Desktop').Trim('"') }
    }
    $script:Repo = (Resolve-Path -LiteralPath $Repository).Path
    $script:Repo = Invoke-RepoGit @('rev-parse', '--show-toplevel')
    $origin = Invoke-RepoGit @('config', '--get', 'remote.origin.url')
    if ($origin -notmatch $expectedOrigin) { throw 'This is not junanime/Project_GC. No working files were changed.' }
    $current = Invoke-RepoGit @('branch', '--show-current')
    if ($current -cnotin $targets) { throw "Open Junhan2, main, or small_test first. Current branch: $current" }
    $commonDir = Invoke-RepoGit @('rev-parse', '--path-format=absolute', '--git-common-dir')
    foreach ($marker in @('MERGE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-merge','rebase-apply','index.lock')) {
        $markerPath = Invoke-RepoGit @('rev-parse', '--git-path', $marker)
        if (![IO.Path]::IsPathRooted($markerPath)) { $markerPath = Join-Path $script:Repo $markerPath }
        if (Test-Path -LiteralPath $markerPath) { throw "Finish the existing Git operation first: $marker" }
    }
    # Unity's lock belongs to this worktree, so an unrelated editor is unaffected.
    $lockPath = Join-Path $script:Repo 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        try {
            $lock = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $lock.Dispose()
        } catch { throw 'Save and close Unity for this repository before synchronizing.' }
    }
    $worktrees = Invoke-RepoGit @('worktree','list','--porcelain')
    foreach ($branch in $targets) {
        if ($branch -cne $current -and $worktrees -cmatch "(?m)^branch refs/heads/$branch`r?$") {
            throw "$branch is open in another worktree. Close or detach that worktree first."
        }
    }
    Invoke-RepoGit @('fetch','origin','--prune') | Out-Null
    $target = Invoke-RepoGit @('rev-parse','refs/remotes/origin/Junhan2')
    foreach ($branch in $targets) {
        $remoteHead = Invoke-RepoGit @('rev-parse',"refs/remotes/origin/$branch")
        if ($remoteHead -cne $target) { throw 'The three remote branches have diverged. Stop and review before using this one-time synchronization tool.' }
    }
    $oldRefs = [ordered]@{}
    $localNames = (Invoke-RepoGit @('for-each-ref','--format=%(refname:short)','refs/heads/')) -split "`n"
    foreach ($branch in $targets) {
        $oldRefs[$branch] = if ($localNames -ccontains $branch) { Invoke-RepoGit @('rev-parse',"refs/heads/$branch") } else { $null }
    }
    $status = Invoke-RepoGit @('status','--porcelain=v1','--untracked-files=all')
    Write-Host "Repository: $script:Repo"
    Write-Host "Unified remote commit: $target"
    Write-Host "Unity version: $((Get-Content -LiteralPath (Join-Path $script:Repo 'ProjectSettings/ProjectVersion.txt') -TotalCount 1))"
    if ($status) { Write-Host 'Local edits will be saved in a named Git stash, including untracked files.' }
    if (!$Apply) {
        Write-Host 'PREVIEW ONLY. Run with -Apply to back up and align the three local branches.'
        exit 0
    }
    $stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6)
    $backupDir = Join-Path $commonDir "device-sync-backups/$stamp"
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    $record = [ordered]@{ repository=$script:Repo; date=(Get-Date -Format o); target=$target; previousBranch=$current; oldRefs=$oldRefs; originalStatus=$status; stash=$null; completed=$false }
    $recordPath = Join-Path $backupDir 'recovery.json'
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    # Back up all existing branch tips before touching the worktree or its refs.
    foreach ($branch in $targets) {
        if ($oldRefs[$branch]) {
            Invoke-RepoGit @('update-ref',"refs/heads/backup/device-sync-$stamp/$branch",$oldRefs[$branch],('0'*40)) | Out-Null
        }
    }
    if ($status) {
        Invoke-RepoGit @('stash','push','--include-untracked','-m',"device-sync-$stamp before laptop Junhan2 alignment") | Out-Null
        $record.stash = Invoke-RepoGit @('rev-parse','refs/stash')
        # Keep a durable reference even if somebody later clears the stash list.
        Invoke-RepoGit @('update-ref',"refs/device-sync-backups/$stamp/stash",$record.stash,('0'*40)) | Out-Null
        $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    }
    if (Invoke-RepoGit @('status','--porcelain=v1','--untracked-files=all')) { throw 'Some edits could not be stashed. Branches were not aligned; inspect the recovery record.' }
    Invoke-RepoGit @('switch','--detach',$target) | Out-Null
    # Compare-and-swap all three refs atomically, including missing local branches.
    $commands = @('start')
    foreach ($branch in $targets) {
        if ($oldRefs[$branch]) { $commands += "update refs/heads/$branch $target $($oldRefs[$branch])" }
        else { $commands += "create refs/heads/$branch $target" }
    }
    $commands += @('prepare','commit')
    # Windows PowerShell adds CRLF to native pipelines, but update-ref's protocol
    # requires LF. Write its input directly, without a shell or command interpolation.
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $script:GitExe
    $info.Arguments = 'update-ref --stdin'
    $info.WorkingDirectory = $script:Repo
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($info)
    $process.StandardInput.Write(($commands -join "`n") + "`n")
    $process.StandardInput.Close()
    $transactionOutput = $process.StandardOutput.ReadToEnd()
    $transactionError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $transactionExit = $process.ExitCode
    $process.Dispose()
    if ($transactionExit -ne 0) { throw "Branch transaction failed: $transactionError. Recover using $recordPath" }
    foreach ($branch in $targets) { Invoke-RepoGit @('branch',"--set-upstream-to=origin/$branch",$branch) | Out-Null }
    Invoke-RepoGit @('switch',$current) | Out-Null
    foreach ($branch in $targets) {
        if ((Invoke-RepoGit @('rev-parse',"refs/heads/$branch")) -cne $target) { throw "Verification failed: $branch" }
    }
    if (Invoke-RepoGit @('status','--porcelain=v1','--untracked-files=all')) { throw 'Unexpected changes remain. See the recovery record before discarding anything.' }
    $record.completed = $true
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    Write-Host 'DONE: Junhan2, main, small_test match GitHub; Changes is empty.' -ForegroundColor Green
    Write-Host "Backups and recovery details: $recordPath"
    Write-Host 'Previous local edits are preserved, not reapplied. This tool did not push or change any other branch.'
    exit 0
} catch {
    Write-Host "STOPPED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
