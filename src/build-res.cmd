@echo off
rem ---------------------------------------------------------------------
rem  Compiles app.rc -> app.res. Kept separate from build.cmd so the
rem  paths are handled by cmd itself (calling rc.exe straight from Git
rem  Bash mangles the /fo argument into RC1107 "invalid usage").
rem
rem  Returns 0 when app.res exists, 1 otherwise. Failure is NOT fatal:
rem  without it the screensaver still builds, it just shows up in the
rem  Windows list under the raw file name.
rem ---------------------------------------------------------------------
setlocal
cd /d %~dp0

set "RC="
set "SDKVER="
for /f "delims=" %%D in ('dir /b /ad /o-n "%ProgramFiles(x86)%\Windows Kits\10\bin" 2^>nul') do (
    if not defined RC if exist "%ProgramFiles(x86)%\Windows Kits\10\bin\%%D\x64\rc.exe" (
        set "RC=%ProgramFiles(x86)%\Windows Kits\10\bin\%%D\x64\rc.exe"
        set "SDKVER=%%D"
    )
)

if not defined RC (
    echo [SKIP] Windows SDK resource compiler not found.
    exit /b 1
)

set "SDKI=%ProgramFiles(x86)%\Windows Kits\10\Include\%SDKVER%"
if not exist "%SDKI%\um\windows.h" (
    echo [SKIP] Windows SDK headers not found under "%SDKI%".
    exit /b 1
)

rem  NOTE: no /fm here. "/fm" means "resource-only DLL file name" and rc
rem  rejects it on an EXE/SCR target with "RC1107: invalid usage".
rem  That flag is only valid when building satellite resource DLLs.
rem  /i points at the SDK headers so "#include <windows.h>" resolves.
"%RC%" /nologo /fo "app.res" /c 65001 /i "%SDKI%\um" /i "%SDKI%\shared" /i "%SDKI%\ucrt" "app.rc"

if not exist "app.res" (
    echo [SKIP] rc.exe failed; see the message above.
    exit /b 1
)

echo [OK]   app.res
exit /b 0
endlocal