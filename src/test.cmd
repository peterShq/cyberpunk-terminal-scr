@echo off
rem ---------------------------------------------------------------------
rem  P0 smoke test - run BEFORE installing anything into System32.
rem  1) saver mode on every monitor (q / Esc to exit)
rem  2) settings dialog + the built-in preview button
rem  3) command-line contract self test
rem ---------------------------------------------------------------------
setlocal

cd /d %~dp0

rem Normalise to "...\cyberpunk-terminal" so error messages show a clean path
rem instead of "src\..\build\...".
for %%I in ("%~dp0..") do set "ROOT=%%~fI"
set "SCR=%ROOT%\build\NexusCore.scr"

if not exist "%SCR%" (
    echo [ERROR] build\NexusCore.scr not found - run src\build.cmd first.
    pause
    exit /b 1
)

:menu
echo.
echo ==================== NexusCore P0 test ====================
echo   1. Run as screen saver  (q / Esc / Ctrl+S to exit)
echo   2. Settings dialog      (then click the preview button)
echo   3. Command-line self test (writes to %%TEMP%%)
echo   4. Quit
echo ===========================================================
echo.
set "CHOICE="
set /p CHOICE=Choose [1-4]:

if "%CHOICE%"=="1" (
    echo.
    echo Starting saver mode on all monitors...
    "%SCR%" /s
    goto :menu
)

if "%CHOICE%"=="2" (
    echo.
    echo Opening settings dialog...
    "%SCR%"
    goto :menu
)

if "%CHOICE%"=="3" (
    "%SCR%" --selftest
    echo.
    echo Result written to "%TEMP%\NexusCoreSaver-selftest.txt"
    start notepad "%TEMP%\NexusCoreSaver-selftest.txt"
    goto :menu
)

if "%CHOICE%"=="4" goto :done

rem Empty input means stdin hit EOF (e.g. run non-interactively).
rem Without this the menu would spin forever.
if "%CHOICE%"=="" goto :done

goto :menu

:done
endlocal