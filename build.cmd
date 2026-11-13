@echo off
setlocal
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. Please install .NET Framework 4.8 first.
  pause
  exit /b 1
)
if not exist "%ROOT%dist" mkdir "%ROOT%dist"
echo Building with "%CSC%" ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"%ROOT%dist\PathTaken.exe" /win32icon:"%ROOT%app.ico" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "%ROOT%src\*.cs"
if errorlevel 1 (
  echo.
  echo [ERROR] build failed.
  pause
  exit /b 1
)
echo.
echo [OK] dist\PathTaken.exe
pause