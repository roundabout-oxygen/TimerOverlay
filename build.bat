@echo off
setlocal
cd /d "%~dp0"

echo ========================================================
echo   Building Timer Overlay (v1.1.7)
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist %CSC% (
    echo [ERROR] .NET Framework compiler not found: %CSC%
    pause
    exit /b 1
)

set WPFLIB=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF

%CSC% /target:winexe /platform:x64 /optimize+ /win32icon:src\app.ico /lib:%WPFLIB% /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,WindowsBase.dll,PresentationCore.dll,PresentationFramework.dll,System.Xaml.dll /out:release\timer_overlay.exe src\App.cs

if errorlevel 1 (
    echo [ERROR] Build failed.
    if "%1" neq "nopause" pause
    exit /b 1
)

echo [SUCCESS] Built successfully: release\timer_overlay.exe
endlocal
