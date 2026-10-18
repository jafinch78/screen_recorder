@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "BUILT="
if exist "%~dp0BUILD_VERSION.txt" set /p "BUILT="<"%~dp0BUILD_VERSION.txt"
if exist "%~dp0OfflineScreenRecorder1607.exe" if /I "%BUILT%"=="1.0.1" goto :launch
echo Recorder EXE is missing or out of date for v1.0.1.
echo Building the new offline GUI locally...
call "%~dp0BUILD_WIN1607.bat"
if errorlevel 1 goto :failed
:launch
if not exist "%~dp0OfflineScreenRecorder1607.exe" goto :failed
start "Offline Screen Recorder" "%~dp0OfflineScreenRecorder1607.exe"
if errorlevel 1 goto :failed
exit /b 0
:failed
echo FAIL: Recorder could not be built or launched. See BUILD_WIN1607_LAST.log.
pause
exit /b 1