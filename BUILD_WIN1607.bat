@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
set "LOG=%~dp0BUILD_WIN1607_LAST.log"
set "APP=%~dp0OfflineScreenRecorder1607.exe"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
>"%LOG%" echo OFFLINE SCREEN RECORDER - WIN1607 BUILD - %DATE% %TIME%
echo ==============================================================
echo  Offline Screen Recorder - portable build for Windows 1607+
echo ==============================================================
echo.
echo [STAGE 1/4] Check working directory and source
>>"%LOG%" echo WORKDIR=%CD%
if not exist "%~dp0source\Program.cs" (
  echo FAIL: Missing source\Program.cs
  >>"%LOG%" echo FAIL: Missing source\Program.cs
  goto :failed
)
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo FAIL: .NET Framework C# compiler missing. .NET Framework 4.x is required.
  >>"%LOG%" echo FAIL: Missing csc.exe
  goto :failed
)
echo PASS: Compiler located
>>"%LOG%" echo CSC=%CSC%

echo.
echo [STAGE 2/4] Build portable WinForms GUI
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:"%APP%" "%~dp0source\Program.cs" >>"%LOG%" 2>&1
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo FAIL: C# compile failed, ERRORLEVEL=%RC%
  echo See BUILD_WIN1607_LAST.log for compiler details.
  goto :failed
)
if not exist "%APP%" (
  echo FAIL: Compiler returned success but no EXE was produced.
  goto :failed
)
echo PASS: EXE compiled successfully.
>>"%LOG%" echo PASS: EXE compiled successfully
>"%~dp0BUILD_VERSION.txt" echo 1.0.1
if errorlevel 1 (
  echo FAIL: Could not write BUILD_VERSION.txt marker.
  >>"%LOG%" echo FAIL: Could not write build version marker
  goto :failed
)

echo.
echo [STAGE 3/4] Check portable FFmpeg
if exist "%~dp0ffmpeg.exe" goto :ffmpeg_exists
if exist "%~dp0tools\ffmpeg.exe" (
  copy /Y "%~dp0tools\ffmpeg.exe" "%~dp0ffmpeg.exe" >>"%LOG%" 2>&1
  goto :ffmpeg_exists
)
if exist "C:\Tools\ffmpeg\bin\ffmpeg.exe" (
  copy /Y "C:\Tools\ffmpeg\bin\ffmpeg.exe" "%~dp0ffmpeg.exe" >>"%LOG%" 2>&1
  goto :ffmpeg_exists
)
where ffmpeg.exe >nul 2>&1
if errorlevel 1 (
  echo WARNING: ffmpeg.exe not found. Put the Windows FFmpeg ffmpeg.exe beside this app.
  >>"%LOG%" echo WARNING: ffmpeg.exe not found
  goto :ffmpeg_missing
)
for /f "delims=" %%F in ('where ffmpeg.exe') do (
  if not exist "%~dp0ffmpeg.exe" copy /Y "%%F" "%~dp0ffmpeg.exe" >>"%LOG%" 2>&1
)
:ffmpeg_exists
if exist "%~dp0ffmpeg.exe" (
  "%~dp0ffmpeg.exe" -hide_banner -version >>"%LOG%" 2>&1
  if errorlevel 1 (
    echo WARNING: Local FFmpeg cannot run. Use GUI Check FFmpeg.
    >>"%LOG%" echo WARNING: FFmpeg cannot run
  ) else (
    echo PASS: Local FFmpeg launches.
    >>"%LOG%" echo PASS: local FFmpeg launches
  )
) else (
  echo WARNING: No local FFmpeg executable. Capture requires FFmpeg.
)
:ffmpeg_missing

echo.
echo [STAGE 4/4] Result
echo PASS: GUI EXE exists at:
echo "%APP%"
echo The EXE plus ffmpeg.exe can run entirely offline on Windows 1607+.
echo Run the EXE and use 'Check FFmpeg' then 'Refresh audio'.
>>"%LOG%" echo BUILD SUCCESS %DATE% %TIME%
echo.
echo FINAL ERRORLEVEL = 0
pause
exit /b 0

:failed
>>"%LOG%" echo BUILD FAILED %DATE% %TIME%
echo.
echo FINAL ERRORLEVEL = 1
echo Check "%LOG%"
pause
exit /b 1