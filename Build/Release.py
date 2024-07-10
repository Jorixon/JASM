
import os
import shutil
import re
import sys


ELEVATOR_CSPROJ = "src\\Elevator\\Elevator.csproj"
ELEVATOR_OUTPUT_FILE = "src\\Elevator\\bin\\Release\\Publish\\Elevator.exe"

JASM_CSPROJ = "src\\GIMI-ModManager.WinUI\\GIMI-ModManager.WinUI.csproj"
JASM_OUTPUT = "src\\GIMI-ModManager.WinUI\\bin\\Release\Publish\\"

JASM_Updater_CSPROJ = "src\\JASM.AutoUpdater\\JASM.AutoUpdater.csproj"
JASM_Updater_OUTPUT = "src\\\\JASM.AutoUpdater\\bin\\Release\Publish\\"
JASM_Updater_FolderName = "JASM - Auto Updater_New"

RELEASE_DIR = "output"
JASM_RELEASE_DIR = "output\\JASM"

SelfContained =  sys.argv[1] == "SelfContained" if len(sys.argv) > 1  else False

def checkSuccessfulExitCode(exitCode: int) -> None:
	if exitCode != 0:
		print("Exit code: " + str(exitCode))
		exit(exitCode)

def extractVersionNumber() -> str:
	with open(JASM_CSPROJ, "r") as jasmCSPROJ:
		for line in jasmCSPROJ:
			line = line.strip()
			if line.startswith("<VersionPrefix>"):
				return re.findall("\d+\.\d+\.\d+", line)

print("PostBuild.py")
print("PWD: " + os.getcwd())
print("SelfContained: " + str(SelfContained))

versionNumber = extractVersionNumber()
if versionNumber is None or len(versionNumber) == 0:
	print("Failed to extract version number from " + JASM_CSPROJ)
	exit(1)
versionNumber = versionNumber[0]

print("Building Elevator...")
elevatorPublishCommand = "dotnet publish " + ELEVATOR_CSPROJ + " /p:PublishProfile=FolderProfile.pubxml -c Release"
print(elevatorPublishCommand)
checkSuccessfulExitCode(os.system(elevatorPublishCommand))
print()
print("Finished building Elevator")


if (SelfContained == False):
	print("Building JASM - Auto Updater...")
	jasmUpdaterPublishCommand = "dotnet publish " + JASM_Updater_CSPROJ + " /p:PublishProfile=FolderProfile.pubxml -c Release"
	print(jasmUpdaterPublishCommand)
	checkSuccessfulExitCode(os.system(jasmUpdaterPublishCommand))
	print()
	print("Finished building JASM - Auto Updater")
else:
	print("Skipping JASM - Auto Updater build because it is not supported for self-contained build")
	print()

print("Building JASM...")
jasmPublishCommand = "dotnet publish " + JASM_CSPROJ + (" /p:PublishProfile=FolderProfileSelfContained.pubxml" if SelfContained else " /p:PublishProfile=FolderProfile.pubxml") + " -c Release" 
print(jasmPublishCommand)
checkSuccessfulExitCode(os.system(jasmPublishCommand))
print()
print("Finished building JASM")

# Create release directory
os.makedirs(RELEASE_DIR, exist_ok=True)
os.makedirs(JASM_RELEASE_DIR, exist_ok=True)


print("Copying Elevator to JASM...")
checkSuccessfulExitCode(os.system("copy " + ELEVATOR_OUTPUT_FILE + " " + JASM_RELEASE_DIR))
print()
print("Finished copying Elevator to release directory")

print("Copying JASM to output...")
shutil.copytree(JASM_OUTPUT, JASM_RELEASE_DIR, ignore=shutil.ignore_patterns("*.pdb"), dirs_exist_ok=True)
print()
print("Finished copying JASM to release directory")

if (SelfContained == False):
	print("Copying JASM - Auto Updater to output...")
	os.mkdir(JASM_RELEASE_DIR + "\\" + JASM_Updater_FolderName)
	shutil.copytree(JASM_Updater_OUTPUT, JASM_RELEASE_DIR + "\\" + JASM_Updater_FolderName, ignore=shutil.ignore_patterns("*.pdb"), dirs_exist_ok=True)
	print()
	print("Finished copying JASM - Auto Updater to release directory")

print("Copying text files to RELEASE_DIR...")
shutil.copy("Build\\README.txt", RELEASE_DIR)
shutil.copy("CHANGELOG.md", RELEASE_DIR + "\\CHANGELOG.txt")

print("Finished copying text files to release directory")

print("Zipping release directory...")
print("7z a -t7z -xm4 JASM.7z " + RELEASE_DIR)
releaseArchiveName = "JASM_v" + versionNumber + ".7z"
if (SelfContained):
	releaseArchiveName = "SelfContained_" + releaseArchiveName

checkSuccessfulExitCode(os.system(f"7z a -mx4 {releaseArchiveName} .\\{RELEASE_DIR}\\*"))
print()
print("Finished zipping release directory")

env_file = os.getenv('GITHUB_ENV')
if env_file is None:
	exit(1)

with open(env_file, "a") as myfile:
    myfile.write(f"zipFile={releaseArchiveName}")

checkSuccessfulExitCode(os.system(f"7z h -scrcsha256 .\\{releaseArchiveName}"))


exit(0)



