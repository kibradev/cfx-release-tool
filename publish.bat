@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem Yeni kurulumdan sonra eski CMD oturumunda PATH guncellenmemis olabilir
set "DOTNET_EXE="
where dotnet >nul 2>&1
if not errorlevel 1 (
  for /f "delims=" %%i in ('where dotnet 2^>nul') do (
    set "DOTNET_EXE=%%i"
    goto :found
  )
)

if exist "%ProgramFiles%\dotnet\dotnet.exe" (
  set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
  goto :found
)

if exist "%ProgramFiles(x86)%\dotnet\dotnet.exe" (
  set "DOTNET_EXE=%ProgramFiles(x86)%\dotnet\dotnet.exe"
  goto :found
)

echo .NET SDK bulunamadi.
echo Kuruluysa CMD/PowerShell penceresini kapatip yeniden acin.
echo Indir: https://dotnet.microsoft.com/download/dotnet/8.0
exit /b 1

:found
echo dotnet: %DOTNET_EXE%
"%DOTNET_EXE%" --version
if errorlevel 1 exit /b 1

"%DOTNET_EXE%" publish ReleaseTool.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish

if errorlevel 1 exit /b 1

echo.
echo Basarili: %~dp0publish\ReleaseTool.exe
endlocal
