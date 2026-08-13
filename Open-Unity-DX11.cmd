@echo off
setlocal
set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe"
set "PROJECT_DIR=%~dp0"

if exist "%PROJECT_DIR%Temp\UnityLockfile" (
  echo El-Emental is already open in Unity.
  echo Save anything important, close the flickering Editor, then run this file again.
  pause
  exit /b 2
)

if not exist "%UNITY_EXE%" (
  echo Unity 6000.5.7f1 was not found at:
  echo %UNITY_EXE%
  pause
  exit /b 3
)

start "El-Emental Unity DX11" "%UNITY_EXE%" -projectPath "%PROJECT_DIR%" -force-d3d11
exit /b 0
