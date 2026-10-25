@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Sync-Junhan2.ps1" -Apply %*
set "sync_result=%errorlevel%"
echo.
if not "%sync_result%"=="0" echo Synchronization stopped. Read the message above; do not discard your files.
pause
exit /b %sync_result%
