@echo off
rem ---------------------------------------------------------------------
rem  Builds NexusCore.scr with the C# compiler that ships with Windows.
rem  No Visual Studio, no .NET SDK, no NuGet required.
rem  Sources are UTF-8; /codepage:65001 stops the compiler from
rem  decoding them with the system ANSI codepage (GBK on zh-CN).

rem
rem  Note: that compiler (v4.0.30319) only understands C# 5.0, so the
rem  sources deliberately avoid newer syntax ($"", ?., nameof, ...).
rem ---------------------------------------------------------------------
setlocal

rem Run from wherever this file lives; source paths are then predictable.
cd /d %~dp0

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework32\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] Compiler not found.
    echo         Looked under WINDIR\Microsoft.NET\Framework64\v4.0.30319
    echo         and Framework32\v4.0.30319. That compiler ships with
    echo         Windows 10 / 11. If it is missing, try running
    echo         DISM /Online /Cleanup-Image /RestoreHealth
    exit /b 1
)

if not exist "..\build" mkdir "..\build"
del /q "..\build\NexusCore.scr" 2>nul
del /q "app.res" 2>nul

rem ---------------------------------------------------------------------
rem  Step 1 - resources. The friendly name in the screensaver list comes
rem  from string resource ID 1 (see app.rc / KB 182383). rc.exe lives in
rem  the Windows SDK, which is NOT part of a clean Windows install, so it
rem  is optional: without it we still build, we just lose the pretty
rem  list name and fall back to the file name "NexusCore".
rem ---------------------------------------------------------------------
call "%~dp0build-res.cmd"
set "HAS_RES=0"
if exist "app.res" set "HAS_RES=1"

echo.
echo Compiling with "%CSC%"
echo.

if "%HAS_RES%"=="1" (
  "%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:"..\build\NexusCore.scr" ^
    /win32res:"app.res" ^
  /codepage:65001 ^
  /r:System.dll ^
  /r:System.Core.dll ^
  /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll ^
  Native.cs ^
  Config.cs ^
  Content.cs ^
  Panels.cs ^
  PreviewPanel.cs ^
  Renderer.cs ^
  SurfaceForm.cs ^
  SaverHost.cs ^
  PreviewHost.cs ^
  SettingsForm.cs ^
  Program.cs
) else (
  "%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:"..\build\NexusCore.scr" ^
    /codepage:65001 ^
  /r:System.dll ^
  /r:System.Core.dll ^
  /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll ^
  Native.cs ^
  Config.cs ^
  Content.cs ^
  Panels.cs ^
  PreviewPanel.cs ^
  Renderer.cs ^
  SurfaceForm.cs ^
  SaverHost.cs ^
  PreviewHost.cs ^
  SettingsForm.cs ^
  Program.cs
)

if errorlevel 1 (
    echo.
    echo [FAILED] See errors above.
    exit /b 1
)

echo.
echo [OK] ..\build\NexusCore.scr
for %%F in ("..\build\NexusCore.scr") do echo      size: %%~zF bytes
endlocal