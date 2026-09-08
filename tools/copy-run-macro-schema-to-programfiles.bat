@echo off
REM Run as Administrator to sync schema into Program Files Velum adapter-package.
copy /Y "C:\ProgramData\ISIDA\Adapters\sldworks_19\schema\handlers-catalog.json" "C:\Program Files\Velum\adapter-package\schema\handlers-catalog.json"
copy /Y "C:\ProgramData\ISIDA\Adapters\sldworks_19\schema\README.txt" "C:\Program Files\Velum\adapter-package\schema\README.txt"
echo Done.
pause
