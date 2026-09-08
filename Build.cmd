@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build-Velum.ps1" %*
exit /b %ERRORLEVEL%
