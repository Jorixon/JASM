# JASM - Just Another Skin Manager

JASM is a skin manager for a certain game. Made using WinUI 3 with WinAppSDK. 
I made this for fun, for myself and to learn WinUI, but it kinda took off over time.

There may be some "GIMI-ModManager" references throughout the app, that is what I originally called this project. I'll change them eventually.


Download link and images are also available over at GameBanana

**This is still in an early stage of development. Make backups and use at your own risk ⚠️** 

Unhandled exceptions are written to the Logs file as well. Debug logging can be enabled in appsettings.json



## Features
- Pretty UI 👀
- Drag And Drop files directly into the app
- Automatically sort unsorted mods into respective character's folders
- Move Mods betwee characters
- Start a 3Dmigto launcher and/or a certain game straight from the app
- Refresh mods using F10 or the refresh button in app. (Requires an elevated side process, see description bellow)
- The app watches character folders and automaticly updates if skins are added or removed from folders.

## Hotkeys
- "SPACE" - In character view, toggles selected mods on/off
- "F10" - Refresh Mods in the game, if the elevator process and a certain game is running
- "F5" - In character view, refresh the character's mods from disk
- "CTRL + F" - In character overview, focus on the search bar

## Download
Latest version can be downloaded either from GameBanana or from the [Releases]() page. To start the app run ```GIMI-ModManager.WinUI.exe``` in ```JASM/``` folder, I suggest creating a shortcut to it.

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
- Drag and drop is really finicky for some reason. It works but it's not perfect.
- Changing the app's theme causes instability and usually it crashes when navigating to another page. This means it needs to restart after changing the theme
- I made everything in dark mode theme, so light mode does not look good.
- Currently the newest characters have not been added. I plan to add them, as well as creating a way to add new custom characters.
- App settings are stored here ```C:\Users\<username>\AppData\Local\JASM\ApplicationData```

### Contributing
If you want to contribute to this project, feel free to do so. I am not a professional developer when it comes to WinUI and I am still actively  learning.

**Just be aware that the code is not super clean...**
