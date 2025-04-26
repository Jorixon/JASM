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
- The app watches character folders and automatically updates if skins are added or removed from folders.
- Edit merged.ini keys
- Export (copy) all mods managed by JASM to a user specified folder
- Refresh mods using F10 or the refresh button in app. (Requires an elevated side process, see description bellow)


## Hotkeys
- "SPACE" - In character view, toggles selected mods on/off
- "F10" - Refresh Mods in the game, if the elevator process and a certain game is running
- "F5" - In character view, refresh the character's mods from disk
- "CTRL + F" - In character overview, focus on the search bar
- "ESCAPE" - In character view, Go back to character overview
- "F1" - In character view, opens selectable in-game skins

## Download
Latest release can be downloaded either from GameBanana or from the [Releases](https://github.com/Jorixon/JASM/releases) page. To start the app run ```JASM - Just Another Skin Manager.exe``` in ```JASM/``` folder, I suggest creating a shortcut to it.

Latest development release can be downloaded from [Actions Tab](https://github.com/Jorixon/JASM/actions/workflows/dotnet-desktop.yml?query=branch%3Amain+is%3Asuccess) these are automatically built from the main branch and are '''usually stable''' but not necessarily ⚠️
1. Link to the latest successful [build](https://github.com/Jorixon/JASM/actions/workflows/dotnet-desktop.yml?query=branch%3Amain+is%3Asuccess)
2. Select the latest build (top of the list)
3. Scroll down to the "Artifacts" section and click the "Upload JASM" link to download.

## Requirements
- Windows 10, version 1809 or higher ([supposedly](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/))
- [NET Desktop Runtime](https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch=x64&rid=win10-x64&apphost_version=9.0.0&gui=true)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)
- [Webp Image Extension](https://apps.microsoft.com/detail/9pg2dk419drg?hl=en-US&gl=US) (Windows 10 only)

If you don't have these downloaded the application will prompt you to download necessary dependencies and provide links.
 

### Elevator process
The elevator process is a small program that can be started as an elevated process from the app. It is completely optional and is kind of a niche feature.
It is used to send the F10 key to the game to refresh the mods. Enabling and disabling mods in JASM will also automatically refresh the mods. This is done through named pipes. 
The process does not listen for keybinds it only waits for a simple "1" command from the app. This makes it send the F10 key to the game.

The [H.InputSimulator](https://github.com/HavenDV/H.InputSimulator) library is used to send keyboard input.


### Limitations and Acknowledgements
- The Json file that stores the the game characters is a modified and stripped down version of this [genshin-impact-tools ](https://github.com/tokafew420/genshin-impact-tools/blob/3d10e411a411b8ed532356ccb45fcd80b6b2383b/data/characters.json) and some of the images are also from the same repository.
- The Json file that stores the weapons is a modified and stripped down version of this [genshin-impact-tools ](https://github.com/tokafew420/genshin-impact-tools/blob/94d55e8b88d5580d84e6b0991ce82e2798220d44/data/weapons.json) and the weapon images are also from the same repository.
- 7-Zip is bundled with the app, 7-Zip is licensed under the GNU Lesser General Public License (LGPL). You can find the full license details and source code at [www.7-zip.org](https://www.7-zip.org/).
- SharpCompress is used if the bundled 7-zip is not found. SharpCompress is licensed under the MIT license. You can find the full license details and source code at [SharpCompress's GitHub Page](https://github.com/adamhathcock/sharpcompress)
- WinUI3Localizer is used for localization. WinUI3Localizer is licensed under the MIT license. You can find the full license details and source code at [WinUI3Localizer's GitHub Page](https://github.com/AndrewKeepCoding/WinUI3Localizer)
- I have tested this app mostly on two machines running Windows 11. I have tested it on Windows 10 in a virtual machine.
- There are definitely bugs. I have not tested this app enough and there are not tests written for it.
- Drag and drop is really finicky for some reason. It works but it's not perfect. So the code for it is still quite messy and it involved time consuming trial and error until it worked.
- Changing the app's theme causes instability and usually it crashes when navigating to another page. This means it needs to restart after changing the theme
- I made everything in dark mode theme, so light mode does not look good.
- App settings are stored here ```C:\Users\<username>\AppData\Local\JASM\ApplicationData```
- Mod specific settings are stored within the mod folder themselves and are prefixed with ```.JASM_```. When exporting mods, these files can be ignored.

### Contributing
If you want to contribute to this project, feel free to do so. I am not a professional developer when it comes to WinUI and I am still actively learning. Contributing [CONTRIBUTING.md](https://github.com/Jorixon/JASM/blob/main/CONTRIBUTING.md)

The code has progressively gotten more spaghettified over time ;_;

**So be aware that the code is not super clean...**


### Building from source
- I suggest following the [Install tools for the Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment?tabs=cs-vs-community%2Ccpp-vs-community%2Cvs-2022-17-1-a%2Cvs-2022-17-1-b) tutorial.
- From then on it should be a simple git clone https://github.com/Jorixon/JASM
- Then inside the cloned folder, click on JASM\src\GIMI-ModManager.sln or open it trough Visual Studio.
- Then right click GIMI-ModManager.WinUI project in the sidebar and then click publish and click publish again.
- In the target location the application should have been built.

## FAQ

### JASM does not start anymore

I believe this is due to some oddity with WinAppSdk not installing correctly. I do not know what causes this. A temporary (permanent?) solution is to use the self contained version of JASM that does not require WinAppSdk or .NET. See the releases page [SelfContainted_JASM_vx.x.x.7z](https://github.com/Jorixon/JASM/releases). Ref [#72](https://github.com/Jorixon/JASM/issues/72) and [#171](https://github.com/Jorixon/JASM/issues/171)

Another potential fix if JASM used to work, is to delete the JASM user settings folder. This will wipe your settings i.e. presets, folder paths etc. However, your mods will be untouched as well as the mod settings like custom display name and images. JASM settings are stored here: `%localappdata%\JASM` / `C:\Users\<username>\AppData\Local\JASM`. You can start by deleting each game settings folder to see if it helps, alternativly just delete the entire folder. Presets are stored inside the preset folder. Might be a good idea make a backup first.

### XXMI compatbility
As of right now JASM isn't fully compatible, until then make a blank file named 3dmigoto loader.exe in the folder you have have set for the MI in XXMI

Or if you know what your doing, and want to be able to launch the game with XXMI through JASM, make a symlink to a shortcut. (drop menu next to start in XXMI to make the shortcut for a specific game)


### Missing Images
You are most likely using windows 10 and missing the [Webp Image Extension](https://apps.microsoft.com/detail/9pg2dk419drg?hl=en-US&gl=US)


### Command line support

JASM has basic command line support. As of now the only supported functionality is to start directly into a selected game. If you would like to see more command line options, feel free to open an issue with your suggested use case.

See --help for more information.

Powershell:
```powershell
.\'JASM - Just Another Skin Manager.exe' --help
# Example: Close the current instance if it is running and start JASM with the selected game
.\'JASM - Just Another Skin Manager.exe' --switch --game genshin
```

### Memory usage is high

For each page navigated a lot of memory is allocated and not released. This causes the app to quickly use more than 1GB of memory by quickly navigating between pages. This isn't a quick fix. I suggest restarting the app if you notice it getting slow.

From my research WinUI seems to maybe have a memory leak when navigating pages. I am not sure if this is the case or if I am doing something wrong. Most of the memory is unmanaged memory which means a memory profiler won't help much. 


### Elevator download link

As the Elevator  gets flagged as malware you'll need to download it manually from the [Releases Page](https://github.com/Jorixon/JASM/releases/tag/v2.14.3)
