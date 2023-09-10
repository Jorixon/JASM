# JASM - Just Another Skin Manager

JASM is a skin manager for a certain game. Made using WinUI 3 with WinAppSDK. 
I made this for fun, for myself and to learn WinUI, but it kinda took off over time.

There may be some "GIMI-ModManager" references throughout the app, that is what I originally called this project. I'll change them eventually.


Download link and images are also available over at [GameBanana](https://gamebanana.com/tools/14574)

**This is still in an early stage of development. Make backups and use at your own risk ⚠️** 

Unhandled exceptions are written to the Logs file as well. Debug logging can be enabled in appsettings.json



## Features
- Pretty UI 👀
- Drag And Drop files directly into the app
- Automatically sort unsorted mods into respective character's folders
- Move Mods between characters
- Start a 3Dmigto launcher and/or a certain game straight from the app
- Refresh mods using F10 or the refresh button in app. (Requires an elevated side process, see description bellow)
- The app watches character folders and automaticly updates if skins are added or removed from folders.

## Hotkeys
- "SPACE" - In character view, toggles selected mods on/off
- "F10" - Refresh Mods in the game, if the elevator process and a certain game is running
- "F5" - In character view, refresh the character's mods from disk
- "CTRL + F" - In character overview, focus on the search bar
- "ESCAPE" - In character view, Go back to character overview

## Download
Latest release can be downloaded either from GameBanana or from the [Releases](https://github.com/Jorixon/JASM/releases) page. To start the app run ```JASM - Just Another Skin Manager.exe``` in ```JASM/``` folder, I suggest creating a shortcut to it.

Latest development release can be downloaded from [Actions Tab](https://github.com/Jorixon/JASM/actions/workflows/dotnet-desktop.yml?query=branch%3Amain+is%3Asuccess) these are automatically built from the main branch and are '''usually stable''' but not necessarily ⚠️
1. Link to the latest successful [build](https://github.com/Jorixon/JASM/actions/workflows/dotnet-desktop.yml?query=branch%3Amain+is%3Asuccess)
2. Select the latest build (top of the list)
3. Scroll down to the "Artifacts" section and click the "Upload JASM" link to download.

## Requirements
- Windows 10, version 1809 or higher ([supposedly](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/))
- [NET 7.0 Desktop Runtime (v7.0.10)](https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch=x64&rid=win10-x64&apphost_version=7.0.10&gui=true)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

### Elevator process
The elevator process is a small program that can be started as an elevated process from the app. It is completely optional.
It is used to send the F10 key to the game to refresh the mods. This is done trough named pipes. 
The process does not listen for keybinds it only waits for a simple "1" command from the app. This makes it send the F10 key to the game.

The [H.InputSimulator](https://github.com/HavenDV/H.InputSimulator) library is used to send keyboard input.


### Limitations and Acknowledgements
- The Json file that stores the characters is a stripped down version of this [genshin-impact-tools ](https://github.com/tokafew420/genshin-impact-tools/blob/3d10e411a411b8ed532356ccb45fcd80b6b2383b/data/characters.json) and images are also from the same repository.
- I have tested this app mostly on two machines running Windows 11. I have not tested it on Windows 10.
- There are definitely bugs. I have not tested this app enough and there are not tests written for it.
- Drag and drop is really finicky for some reason. It works but it's not perfect. So the code for it is still quite messy and it involed time consuming tirial and error until it worked.
- Changing the app's theme causes instability and usually it crashes when navigating to another page. This means it needs to restart after changing the theme
- I made everything in dark mode theme, so light mode does not look good.
- Currently the newest characters have not been added. I plan to add them, as well as creating a way to add new custom characters.
- App settings are stored here ```C:\Users\<username>\AppData\Local\JASM\ApplicationData```

### Contributing
If you want to contribute to this project, feel free to do so. I am not a professional developer when it comes to WinUI and I am still actively  learning.

**Just be aware that the code is not super clean...**

### Building from source
- I suggest following the [Install tools for the Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment?tabs=cs-vs-community%2Ccpp-vs-community%2Cvs-2022-17-1-a%2Cvs-2022-17-1-b) tutorial.
- From then on it should be a simple git clone https://github.com/Jorixon/JASM
- Then inside the cloned folder, click on JASM\src\GIMI-ModManager.sln or open it trough Visual Studio.
- Then right click GIMI-ModManager.WinUI project in the sidebar and then click publish and click publish again.
- In the target location the application should have been built.

## FAQ

### I Get the error: An error occurred while adding the storage items. Mod may have been partially copied. Could not find a part of the path "C:\Users\\AppData\Local\Temp\7z..." When draging mods from 7z
This still seems to happen with certain mods, though an alternative is to use the “Add Archive…” command or drag and drop the entire archive file. Just note that it is not smart enough to detect a skin nested inside a “Mods/” folder, at least not yet.

#### The short reason for why this happens:

There seems to be some odd behavior between WinUI/WinAppSdk frameworks and the way 7zip extracts files. Currently I don’t have a quick fix.

#### Long Version:

For some reason when you drag and drop 7z contents into, let’s say file explorer, the archive is first extracted to a “C:\Users\\AppData\Local\Temp\7z…” folder. The folder is then moved/copied to where you dropped the files. The temp 7z folder is deleted once this process finishes. At least that’s how I understand it when it comes to drag and drop into file explorer. The exact details might be a little different.

This process is similar for this application. The problem is that for some reason 7z seems to delete the extracted contents in “/Temp/7z…” before JASM can copy/move the files to the mod’s directory. This took quite a while to debug and a lot of trial and error until I got to something that worked (during development?).

I believe it has something to do with IPC (Inter Process Communication) between 7z process and JASM. I’ve read some posts on Github of others having similar problems with drag and drop.

At the moment I don’t know what makes it sometimes fail, but I know it happens sometimes. I would really like to fix this, I just don’t how yet.


