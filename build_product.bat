@echo off
title ApexPOS Product Builder
echo Building ApexPOS standalone product package...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_product.ps1"
pause
