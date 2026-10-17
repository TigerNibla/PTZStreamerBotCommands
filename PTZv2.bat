
@echo off
setlocal

:: Reolink RLC-823A 16X
set "CAMERA_IP=IP OF CAMERA GOES HERE"
set "USERNAME=XXXXXX"
set "PASSWORD=XXXXXXX"

:: Arguments
set "OP=%~1"
set "VALUE=%~2"

:: Default movement speed
if not defined VALUE set "VALUE=1"

:: Check command
if not defined OP (
    echo Usage: PTZ.bat COMMAND [SPEED or PRESET]
    exit /b 1
)

:: Preset movement
if /I "%OP%"=="ToPos" (
    set "JSON=[{\"cmd\":\"PtzCtrl\",\"action\":0,\"param\":{\"channel\":0,\"id\":%VALUE%,\"op\":\"ToPos\",\"speed\":1}}]"
    goto SEND
)

:: Stop movement
if /I "%OP%"=="Stop" (
    set "JSON=[{\"cmd\":\"PtzCtrl\",\"action\":0,\"param\":{\"channel\":0,\"op\":\"Stop\"}}]"
    goto SEND
)

:: Directional movement
set "JSON=[{\"cmd\":\"PtzCtrl\",\"action\":0,\"param\":{\"channel\":0,\"op\":\"%OP%\",\"speed\":%VALUE%}}]"


:SEND
curl -s -k --connect-timeout 5 -X POST -H "Content-Type: application/json" -d "%JSON%" "https://%CAMERA_IP%/cgi-bin/api.cgi?user=%USERNAME%&password=%PASSWORD%"

echo.
endlocal
