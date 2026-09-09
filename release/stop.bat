@echo off
setlocal
cd /d "%~dp0"
echo Stopping Blue Archive Timer Overlay...
taskkill /F /IM ba_timer_overlay.exe /T 2>nul
echo Done.
endlocal
