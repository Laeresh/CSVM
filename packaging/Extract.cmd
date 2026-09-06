@echo off
rem One-click asset extraction for the CSVM release package: double-click this file and it
rem fills extracted\ next to CSVM.exe from your own Crimson Skies install.
rem
rem All it does is run Extract.ps1 beside it with the execution-policy switch Windows needs
rem for an unsigned script, and hold the window open afterwards. Every extraction decision is
rem in Extract.ps1 and the two extractor scripts it dispatches to.
rem
rem Any argument is forwarded, so dragging your Crimson Skies install folder onto this file
rem uses that folder; with no argument, Extract.ps1 looks for the install itself and offers a
rem folder picker.

setlocal
title CSVM - extract Crimson Skies game data

echo.
echo CSVM asset extraction
echo =====================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Extract.ps1" %*
set EXITCODE=%ERRORLEVEL%

echo.
if not "%EXITCODE%"=="0" (
    echo Extraction did NOT finish - exit code %EXITCODE%. The messages above say why.
    echo Nothing on your machine was changed except, possibly, a partly written extracted folder.
) else (
    echo Extraction finished. You can close this window and run CSVM.exe.
)

rem The window is held open on BOTH outcomes: a console that closes the instant it is done
rem cannot be told from one that crashed, and the message above is the whole point of the run.
echo.
echo Press any key to close this window.
pause >nul
exit /b %EXITCODE%
