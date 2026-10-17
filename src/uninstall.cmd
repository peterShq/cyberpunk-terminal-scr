@echo off
rem ---------------------------------------------------------------------
rem  Removes NexusCore.scr and clears the selection if it is active.
rem  Requires administrator rights; asks for them automatically.
rem ---------------------------------------------------------------------
setlocal

net session >nul 2>&1
if errorlevel 1 (
    echo Requesting administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "DST=%WINDIR%\System32\NexusCore.scr"
set "KEY=HKCU\Control Panel\Desktop"

echo.
echo Uninstalling NexusCore screen saver
echo.

rem Only touch SCRNSAVE.EXE if it actually points at us.
for /f "tokens=2,*" %%A in ('reg query "%KEY%" /v SCRNSAVE.EXE 2^>nul ^| find /i "SCRNSAVE.EXE"') do set "CUR=%%B"

if defined CUR (
    echo Current selection: %CUR%
    echo %%CUR%%|find /i "NexusCore" >nul
    if not errorlevel 1 (
        reg add "%KEY%" /v SCRNSAVE.EXE /t REG_SZ /d "" /f >nul
        reg add "%KEY%" /v ScreenSaveActive /t REG_SZ /d "0" /f >nul
        echo [OK] Cleared the active-screen-saver selection.
        set CUR=
    ) else (
        echo [SKIP] A different screen saver is selected; leaving it alone.
    )
) else (
    echo [INFO] SCRNSAVE.EXE is not set.
)

if exist "%DST%" (
    del /f /q "%DST%" >nul 2>&1
    if exist "%DST%" (
        echo [ERROR] Could not delete %DST%
        echo         Close anything using it, or check your antivirus.
    ) else (
        echo [OK] Removed %DST%
    )
) else (
    echo [INFO] File not present - nothing to delete.
)

rem Housekeeping: our own log/selftest files.
del /q "%TEMP%\NexusCoreSaver.log" >nul 2>&1
del /q "%TEMP%\NexusCoreSaver-selftest.txt" >nul 2>&1

echo.
echo Done.
echo.
pause