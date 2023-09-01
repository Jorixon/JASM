@echo off
REM %1 elevator output exe
REM %2 JASM output folder
REM %3 Publish folder


set "elevatorOutputFolderPath=%~1"
if "%elevatorOutputFolderPath%"=="" (
    echo Elevator.exe not provided.
    exit /b 1
)


echo Copying Elevator.exe to JASM output folder
copy  %1 %2

REM Clear the contents of the Publish folder

echo Clearing the contents of the Publish folder
set "publishFolderPath=%~3"
if "%publishFolderPath%"=="" (
    echo Publish folder path not provided.
    exit /b 1
)

REM Create the folder if it doesn't exist
if not exist "%publishFolderPath%" (
    mkdir "%publishFolderPath%"
)

REM Echo files that would be deleted from the Publish folder
REM echo Files that would be deleted from the Publish folder:
REM for %%F in ("%publishFolderPath%\*") do (
REM     echo %%~nxF
REM )

REM Delete all files in the folder
REM del /Q "%folderPath%\*" >nul 2>&1

REM Remove all subdirectories in the folder
REM for /d %%d in ("%folderPath%\*") do (
REM     rmdir /S /Q "%%d"
REM )



echo Copying JASM output folder contents to Publish folder
xcopy %2\* %3\ /E /Y

echo Completed.