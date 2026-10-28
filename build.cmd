@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [x] .NET Framework 4.8 compiler not found:
  echo     %CSC%
  exit /b 1
)

rem csc cannot read sources from a non-ASCII path (Chinese folder names, etc.).
rem Stage everything in an ASCII-only folder and compile there.
set "WORK=%PUBLIC%\sf6-rain-build"
if "%PUBLIC%"=="" set "WORK=%TEMP%\sf6-rain-build"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%WORK%" || goto :fail

copy /y "src\*.cs" "%WORK%\" >nul || goto :fail
copy /y "app.manifest" "%WORK%\" >nul || goto :fail
copy /y "assets\app.ico" "%WORK%\" >nul || goto :fail
copy /y "assets\app.png" "%WORK%\" >nul || goto :fail

pushd "%WORK%"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /unsafe /codepage:65001 ^
  /win32icon:app.ico /win32manifest:app.manifest ^
  /resource:app.png,Sf6Rain.logo.png ^
  /out:sf6-rain.exe ^
  AppPaths.cs AssemblyInfo.cs AudioPlayer.cs Detector.cs Json.cs MainForm.cs ^
  MonitorEngine.cs MusicLibrary.cs OverlayForm.cs PreviewPanel.cs Program.cs ^
  ScreenCapture.cs SelfTest.cs Settings.cs Theme.cs WinApi.cs ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll"
set "ERR=%ERRORLEVEL%"
popd

if not "%ERR%"=="0" goto :fail

copy /y "%WORK%\sf6-rain.exe" "%~dp0sf6-rain.exe" >nul || goto :fail
echo [ok] built %~dp0sf6-rain.exe
exit /b 0

:fail
echo [x] build failed
exit /b 1
