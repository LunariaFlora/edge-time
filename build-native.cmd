@echo off
setlocal
cd /d "%~dp0"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (echo Visual Studio installer was not found. & exit /b 1)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSROOT=%%i"
if not defined VSROOT (echo C++ desktop build tools were not found. & exit /b 1)
call "%VSROOT%\Common7\Tools\VsDevCmd.bat" -arch=x64 -no_logo
if errorlevel 1 exit /b 1
cl /nologo /std:c++20 /EHsc /MT /LD native\GlassEffect.cpp /link /out:GlassEffect.dll windowsapp.lib d2d1.lib dxguid.lib
if errorlevel 1 exit /b 1
dotnet build EdgeTime.csproj -c Release
exit /b %errorlevel%
