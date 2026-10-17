@echo off
rem ---------------------------------------------------------------------
rem  Installs NexusCore.scr into the Windows system directory so it shows
rem  up in Settings - Personalization - Lock screen - Screen saver.
rem  Requires administrator rights; asks for them automatically.
rem
rem  After copying it VERIFIES the installed copy byte-for-byte. A stale
rem  System32 copy silently runs old code, which has already cost us one
rem  wasted test round - so never trust the copy without checking it.
rem ---------------------------------------------------------------------
setlocal

net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

for %%I in ("%~dp0..") do set "ROOT=%%~fI"
set "SRC=%ROOT%\build\NexusCore.scr"
set "DST=%WINDIR%\System32\NexusCore.scr"

if not exist "%SRC%" goto nosrc

echo.
echo === Installing NexusCore screen saver ===
echo   from : %SRC%
echo   to   : %DST%
echo.

if exist "%DST%" fc /b "%SRC%" "%DST%" >nul 2>&1
if exist "%DST%" if not errorlevel 1 echo Existing copy is already up to date.

copy /y "%SRC%" "%DST%" >nul
if errorlevel 1 goto copyfail

fc /b "%SRC%" "%DST%" >nul 2>&1
if errorlevel 1 (
    echo [WARN] Installed file does NOT match the build byte-for-byte.
    echo        Windows may still be running an older version.
) else (
    echo [OK] Installed and verified byte-for-byte.
)

echo.
echo Make it the ACTIVE screen saver now? [Y/n]
set "ANS="
set /p ANS=
if /i not "%ANS%"=="n" (
    reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%DST%" /f >nul
    reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d "1" /f >nul
)

echo.
echo --- verifying the active-screen-saver setting ---
set "CUR="
for /f "tokens=2,*" %%A in ('reg query "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE 2^>nul ^| find /i "SCRNSAVE"') do set "CUR=%%B"
echo   SCRNSAVE.EXE = %CUR%
echo %CUR%| find /i "NEXUSC" >nul
if errorlevel 1 (
    echo   [WARN] That does NOT point at NexusCore, so the system will
    echo          never launch it. Select it from the screen-saver list.

) else (
    echo   [OK] This machine will now launch NexusCore when idle.
)

echo.
echo Reminder: ticking "On resume, display logon screen" is safe with this
echo          saver - measured, it does NOT stop the idle trigger. This
echo          saver exits on ANY key or mouse input, so if you need
echo          unattended locking, use that Windows option (or a password).
echo.
pause
exit /b 0

:nosrc
echo [ERROR] Build output not found: %SRC%
echo         Run build.cmd inside the src folder first.
pause
exit /b 1

:copyfail
echo.
echo [ERROR] Copy failed. Usual causes:
echo         - antivirus blocked the write; allow it and retry
echo         - System32 is protected by security software
pause
exit /b 1