@echo off
rem Starts CSVM in VR (experimental) through the OpenXR runtime that is active on this PC.
rem --xr-mode on is an engine argument and stands ahead of the game's own arguments, which go
rem after a "--", for example: CSVM-VR.cmd -- --xr-quality=flat
start "" /d "%~dp0." "%~dp0CSVM.exe" --xr-mode on %*
