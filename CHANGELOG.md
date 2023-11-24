# Changelog

## [1.8.0](https://github.com/Jorixon/JASM/compare/v1.7.0...v1.8.0) (2023-11-24)


### Features

* Mod install helper ([#89](https://github.com/Jorixon/JASM/issues/89)) ([7db7253](https://github.com/Jorixon/JASM/commit/7db725343b9cbe1021ec5984c822d8a7f974a3d8))


### Bug Fixes

* Bug where update notification was connected to character not the mod ([368ef77](https://github.com/Jorixon/JASM/commit/368ef77faa8732a06d0dc80c3470983bc0f0162e))


### Miscellaneous

* Added a simple mods overview page ([ee277e0](https://github.com/Jorixon/JASM/commit/ee277e0ba81bdb2c9fed306b0424f5e4b49505e3))
* Added ModNotifications cleanup ([368ef77](https://github.com/Jorixon/JASM/commit/368ef77faa8732a06d0dc80c3470983bc0f0162e))
* Better handling of invalid jasmConfig, invalid is renamed and new one is created ([7db7253](https://github.com/Jorixon/JASM/commit/7db725343b9cbe1021ec5984c822d8a7f974a3d8))
* More redundant handling of Id in jasm_modconfig ([#88](https://github.com/Jorixon/JASM/issues/88)) ([368ef77](https://github.com/Jorixon/JASM/commit/368ef77faa8732a06d0dc80c3470983bc0f0162e))


### Code Refactoring

* Redid Mod update checker ([#86](https://github.com/Jorixon/JASM/issues/86)) ([ee277e0](https://github.com/Jorixon/JASM/commit/ee277e0ba81bdb2c9fed306b0424f5e4b49505e3))

## [1.7.0](https://github.com/Jorixon/JASM/compare/v1.6.3...v1.7.0) (2023-11-15)


### Features

* Honkai Star Rail support added ([#83](https://github.com/Jorixon/JASM/issues/83)) ([05c4d86](https://github.com/Jorixon/JASM/commit/05c4d862e1e1c70d1b9234dc9c05786314feff8f))


### Bug Fixes

* Unable to restart app when switching game ([a8d59e2](https://github.com/Jorixon/JASM/commit/a8d59e2dd77fa7f115c0f7e62e1d6d1e7978c150))

## [1.6.3](https://github.com/Jorixon/JASM/compare/v1.6.2...v1.6.3) (2023-11-11)


### Bug Fixes

* JASM window being permanently hidden if closed while it was minimized ([ed7fb6c](https://github.com/Jorixon/JASM/commit/ed7fb6ce941c3989f609865fc2ebbc023bf2d0b8))


### Miscellaneous

* JASM will now check if there are other JASM instances running before starting ([ed7fb6c](https://github.com/Jorixon/JASM/commit/ed7fb6ce941c3989f609865fc2ebbc023bf2d0b8))


### Continuous Integration

* Calculate checksum for archive during build ([#81](https://github.com/Jorixon/JASM/issues/81)) ([735d86e](https://github.com/Jorixon/JASM/commit/735d86e19cf5057e8959b8ba3808f38e816368d6))

## [1.6.2](https://github.com/Jorixon/JASM/compare/v1.6.1...v1.6.2) (2023-11-11)


### Bug Fixes

* Automatic reorganization of mods was bugged. This led to (almost) all mods being placed in the "Others" folder ([bb2b0df](https://github.com/Jorixon/JASM/commit/bb2b0dfa6931b2b10e118665287bdeb2f2fdcb93))


### Miscellaneous

* Ability to use mouse 4 and mouse 5 to navigate backward and forward ([d3647d4](https://github.com/Jorixon/JASM/commit/d3647d4b427293fdb2a5420626ab7a2d3f3f4ddd))
* JASM now remembers its last window posistion and if maximized ([aa09b3c](https://github.com/Jorixon/JASM/commit/aa09b3c74f6be157fbd704621440a4da712fe945))

## [1.6.1](https://github.com/Jorixon/JASM/compare/v1.6.0...v1.6.1) (2023-11-10)


### Bug Fixes

* Auto Updater failing, due to being unable to delete WebView2 files ([34d0587](https://github.com/Jorixon/JASM/commit/34d0587c86deb48db566f2c8a78a2856753b2c43))

## [1.6.0](https://github.com/Jorixon/JASM/compare/v1.5.0...v1.6.0) (2023-11-10)


### Features

* JASM will now auto detect image in mod folder, looks for images in this order 1. ".jasm_cover" 2. "preview" 3. "cover" ([f05043c](https://github.com/Jorixon/JASM/commit/f05043c9de954064f5bebc9306e1ca548f9ad496))
* JASM will now check gamebanana urls for new mod files. It does this by checking if there are any new mods since the last check. ([#78](https://github.com/Jorixon/JASM/issues/78)) ([f05043c](https://github.com/Jorixon/JASM/commit/f05043c9de954064f5bebc9306e1ca548f9ad496))


### Bug Fixes

* Unable to search for deactivated characters in the character manager page ([1f3ff34](https://github.com/Jorixon/JASM/commit/1f3ff34010ba345e0b3a3bb323ba3b11cf82deb0))


### Miscellaneous

* Added easter egg because idk ([f05043c](https://github.com/Jorixon/JASM/commit/f05043c9de954064f5bebc9306e1ca548f9ad496))
* Reduced the number of loose files in JASM folder ([f05043c](https://github.com/Jorixon/JASM/commit/f05043c9de954064f5bebc9306e1ca548f9ad496))

## [1.5.0](https://github.com/Jorixon/JASM/compare/v1.4.6...v1.5.0) (2023-10-31)


### Features

* Ability to change display name of characters and disable characters ([#66](https://github.com/Jorixon/JASM/issues/66)) ([691baa9](https://github.com/Jorixon/JASM/commit/691baa9ef1ea750d40815ecad11ee9dee757fab6))


### Miscellaneous

* Auto Updater now checks for windows system folders in the jasm directory before updating ([3fa8758](https://github.com/Jorixon/JASM/commit/3fa875861e7dafc85abcbbbba22de113674ae5b1))
* Laid the foundation for HSR support and localization of game related text like character names ([691baa9](https://github.com/Jorixon/JASM/commit/691baa9ef1ea750d40815ecad11ee9dee757fab6))
* Renamed Travelers to their respective canon names and changed their image ([691baa9](https://github.com/Jorixon/JASM/commit/691baa9ef1ea750d40815ecad11ee9dee757fab6))

## [1.4.6](https://github.com/Jorixon/JASM/compare/v1.4.5...v1.4.6) (2023-10-22)


### Miscellaneous

* Added Wriothesley Character ([6a7943f](https://github.com/Jorixon/JASM/commit/6a7943fe0b5c518cd42a0e61df005f24a77cd694))
* Changed Auto Updater .NET version from 6 to 7 ([ce53022](https://github.com/Jorixon/JASM/commit/ce530223228b3e133218d93a50868775cb2223c2))

## [1.4.5](https://github.com/Jorixon/JASM/compare/v1.4.4...v1.4.5) (2023-10-22)


### Bug Fixes

* KeySwaps not loading when the mod's filepath changed ([#69](https://github.com/Jorixon/JASM/issues/69)) ([b69e24c](https://github.com/Jorixon/JASM/commit/b69e24ce7904ea6070559ec735a71651f74b3dc3))


### Miscellaneous

* Updated WinAppSDK to Version 1.4.2 (1.4.231008000) ([b69e24c](https://github.com/Jorixon/JASM/commit/b69e24ce7904ea6070559ec735a71651f74b3dc3))

## [1.4.4](https://github.com/Jorixon/JASM/compare/v1.4.3...v1.4.4) (2023-10-09)


### Bug Fixes

* JASM will no longer crash if you move 3Dmigoto folder without changing it in the settings ([b334e97](https://github.com/Jorixon/JASM/commit/b334e970eda8ecf9328056186365c5703694a92a))


### Tweaks

* Improved key relevance in character search ([b334e97](https://github.com/Jorixon/JASM/commit/b334e970eda8ecf9328056186365c5703694a92a))


### Miscellaneous

* Ability to disable all mods as a part of first time startup ([b334e97](https://github.com/Jorixon/JASM/commit/b334e970eda8ecf9328056186365c5703694a92a))
* An error window is now shown on crash/exceptions ([b334e97](https://github.com/Jorixon/JASM/commit/b334e970eda8ecf9328056186365c5703694a92a))


### Code Refactoring

* Refactored large parts of the code related to SkinMod ([#63](https://github.com/Jorixon/JASM/issues/63)) ([b334e97](https://github.com/Jorixon/JASM/commit/b334e970eda8ecf9328056186365c5703694a92a))

## [1.4.3](https://github.com/Jorixon/JASM/compare/v1.4.2...v1.4.3) (2023-10-04)


### Bug Fixes

* Multiple mod's active warning shown even if character skin was overridden for the mod ([e52b307](https://github.com/Jorixon/JASM/commit/e52b307bb3963780584bb1621535efe2232aea7f))


### Tweaks

* Added more filtering options to character overview ([fe8dd68](https://github.com/Jorixon/JASM/commit/fe8dd68570265e50053108b0e75edd7dbb04aed1))
* Minor QOL improvements to ModGrid sorting ([#52](https://github.com/Jorixon/JASM/issues/52)) ([fe8dd68](https://github.com/Jorixon/JASM/commit/fe8dd68570265e50053108b0e75edd7dbb04aed1))


### Miscellaneous

* Added JASM auto updater ([#55](https://github.com/Jorixon/JASM/issues/55)) ([e52b307](https://github.com/Jorixon/JASM/commit/e52b307bb3963780584bb1621535efe2232aea7f))
* Added Neuvillette ([c42c7f4](https://github.com/Jorixon/JASM/commit/c42c7f416d9d81731974066e46dab3755d5206bf))
* Added some simplified Chinese translations to Startup page and Settings page. This is mostly a proof of concept and was translated trough chatGPT. Language can be changed on the settings page. ([fe8dd68](https://github.com/Jorixon/JASM/commit/fe8dd68570265e50053108b0e75edd7dbb04aed1))


### Code Refactoring

* Major refactoring of code related to Character Overview sorting and filtering. ([fe8dd68](https://github.com/Jorixon/JASM/commit/fe8dd68570265e50053108b0e75edd7dbb04aed1))

## [1.4.2](https://github.com/Jorixon/JASM/compare/v1.4.1...v1.4.2) (2023-09-30)


### Bug Fixes

* Image failing to load after disabling/enabling mod ([#48](https://github.com/Jorixon/JASM/issues/48)) ([352de20](https://github.com/Jorixon/JASM/commit/352de20a839d4724e621d52ba1dd8ca4df41b3bf))
* Issue where the delete button on the flyout was not clickable if it was infront of the window titlebar ([#50](https://github.com/Jorixon/JASM/issues/50)) ([1fd5495](https://github.com/Jorixon/JASM/commit/1fd54951794d3cdd0dce86bf222c42670d109598))

## [1.4.1](https://github.com/Jorixon/JASM/compare/v1.4.0...v1.4.1) (2023-09-25)


### Miscellaneous

* Added a warning popup if JASM is running with administrator privileges, can be turned off ([#46](https://github.com/Jorixon/JASM/issues/46)) ([93e7a08](https://github.com/Jorixon/JASM/commit/93e7a0850f89b1b049c9d26022c346a7537cc3a9))
* Added missing skin for Klee, Ayaka and Kaeya ([#44](https://github.com/Jorixon/JASM/issues/44)) ([dff8ec0](https://github.com/Jorixon/JASM/commit/dff8ec05861171c252c7c143647e2d3e1bf4821a))
* **Dependencies:** Updated WinAppSDK and WinUIEx ([93e7a08](https://github.com/Jorixon/JASM/commit/93e7a0850f89b1b049c9d26022c346a7537cc3a9))

## [1.4.0](https://github.com/Jorixon/JASM/compare/v1.3.0...v1.4.0) (2023-09-24)


### Features

* Recently added mods are marked with an icon to make it easier to see what mod was just added ([be0947b](https://github.com/Jorixon/JASM/commit/be0947b9cad897da9c123633b0a14664f44793b2))
* Support for handling mods for different ingame skins for characters ([#41](https://github.com/Jorixon/JASM/issues/41)) ([be0947b](https://github.com/Jorixon/JASM/commit/be0947b9cad897da9c123633b0a14664f44793b2))


### Bug Fixes

* Character overview not showing multiple mods active warning when "Only show characters with mods" was enabled ([be0947b](https://github.com/Jorixon/JASM/commit/be0947b9cad897da9c123633b0a14664f44793b2))
* Crash when adding duplicate mod, and better handling of duplicate folder names ([a47fa80](https://github.com/Jorixon/JASM/commit/a47fa8021d94667fbfe557c23e037bf9d497e04b))
* Export progress ring not showing progress if exporting too many mods ([be0947b](https://github.com/Jorixon/JASM/commit/be0947b9cad897da9c123633b0a14664f44793b2))


### Tweaks

* Made the duplicate folder name checker a bit more robust ([be0947b](https://github.com/Jorixon/JASM/commit/be0947b9cad897da9c123633b0a14664f44793b2))
* Reduced number of releases retrieved from GitHub Api when checking for updates ([a47fa80](https://github.com/Jorixon/JASM/commit/a47fa8021d94667fbfe557c23e037bf9d497e04b))


### Miscellaneous

* Bundled 7zip with JASM ([#39](https://github.com/Jorixon/JASM/issues/39)) ([a47fa80](https://github.com/Jorixon/JASM/commit/a47fa8021d94667fbfe557c23e037bf9d497e04b))

## [1.3.0](https://github.com/Jorixon/JASM/compare/v1.2.0...v1.3.0) (2023-09-16)


### Features

* Drag and drop support in character overview ([#35](https://github.com/Jorixon/JASM/issues/35)) ([c443f08](https://github.com/Jorixon/JASM/commit/c443f08b61ce6f9b53e84c64d7c7d4bd7b3ad168))
* Mod Image now has a right click context menu with Paste/Copy/Clear options ([1964f3b](https://github.com/Jorixon/JASM/commit/1964f3b2fcc828d0a4a0afca2497a37ef0db8ec6))
* Possible to add a back key or forward key if it was missing from merged.ini ([46710a3](https://github.com/Jorixon/JASM/commit/46710a3fd13df1852e2168b4ef10c8d4660b3e34))
* Possible to set custom name for mods. ([#32](https://github.com/Jorixon/JASM/issues/32)) ([1964f3b](https://github.com/Jorixon/JASM/commit/1964f3b2fcc828d0a4a0afca2497a37ef0db8ec6))


### Bug Fixes

* Unsetting all keys for a character removes the key section row in JASM ([#30](https://github.com/Jorixon/JASM/issues/30)) ([46710a3](https://github.com/Jorixon/JASM/commit/46710a3fd13df1852e2168b4ef10c8d4660b3e34))


### Tweaks

* On the Delete mods confirmation popup, the Delete button is now the primary button. So pressing Enter will immediately delete the mods, while pressing space will toggle the Recycle checkbox ([c443f08](https://github.com/Jorixon/JASM/commit/c443f08b61ce6f9b53e84c64d7c7d4bd7b3ad168))


### Miscellaneous

* Added Weapons as its own character ([3c906cd](https://github.com/Jorixon/JASM/commit/3c906cdae2aa9e26ecff7279c332469141a0907f))
* Better error message for when "Run as administrator" property is set on 3DMigoto exe ([c443f08](https://github.com/Jorixon/JASM/commit/c443f08b61ce6f9b53e84c64d7c7d4bd7b3ad168))
* Delete key can be used to delete selected mods in ([c443f08](https://github.com/Jorixon/JASM/commit/c443f08b61ce6f9b53e84c64d7c7d4bd7b3ad168))
* The current path is now shown as a tooltip for Genshin- and 3DMigoto launch buttons ([c443f08](https://github.com/Jorixon/JASM/commit/c443f08b61ce6f9b53e84c64d7c7d4bd7b3ad168))

## [1.2.0](https://github.com/Jorixon/JASM/compare/v1.1.1...v1.2.0) (2023-09-11)


### Continuous Integration

* Better release pipeline ([abc3c1d](https://github.com/Jorixon/JASM/commit/abc3c1da409cb5fa885fe7e1dfefdf80398d9f44))
* Simple characters.json tests and automatic builds ([4bfa960](https://github.com/Jorixon/JASM/commit/4bfa9608610f19f01483a87a66e93384bca59707))


### Miscellaneous

* Added Freminet ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))
* Added Gamebanana shortcut to Character overview for easy access ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))
* Improved Startup screen text ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))


### Features

* Ability to customize merged.ini keys and add link to mod ([#22](https://github.com/Jorixon/JASM/issues/22)) ([c3485cd](https://github.com/Jorixon/JASM/commit/c3485cd562a901268835c1ef6600c63c23b7700b))
* Export Mods function to export all mods managed by JASM to a specified folder ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))
* Mod thumbnail that can be added to mod via drag and drop or file selector ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))
* Warning ' ! ' icon shown on Character overview when multiple mods are active for character ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))


### Bug Fixes

* Temporary folder cleanup on application exit ([0ff9ad1](https://github.com/Jorixon/JASM/commit/0ff9ad1339e8b8d2a198cb6148c0f6d99160670c))


### Tweaks

* Improved character search, especially for characters with longer names ([#19](https://github.com/Jorixon/JASM/issues/19)) ([00b7914](https://github.com/Jorixon/JASM/commit/00b79145c7db0b591229ec010235d3990eda533b))

## [1.1.1](https://github.com/Jorixon/JASM/compare/v1.1.0...v1.1.1) (2023-09-04)


### Bug Fixes

* Zhongli,Navia and Paimon,Yun Jin having duplicate ids ([#16](https://github.com/Jorixon/JASM/issues/16)) ([5740b05](https://github.com/Jorixon/JASM/commit/5740b05c1ec412b5500908b6028c6e876e9d360c))

## [1.1.0](https://github.com/Jorixon/JASM/compare/v1.0.0...v1.1.0) (2023-09-03)


### Features

* Added Paimon, Gliders and some characters from Fontaine. ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
* Added Paimon, Gliders and some characters from Fontaine. ([#13](https://github.com/Jorixon/JASM/issues/13)) ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
* Qol, when selected character for moving mods the move button will recieve focus ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
* Small badge shown when a new JASM release is available ([#10](https://github.com/Jorixon/JASM/issues/10)) ([69eb509](https://github.com/Jorixon/JASM/commit/69eb5098e36c121e248e7240fab423bcc223831a))


### Bug Fixes

* Better description of reorganize mods ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
* Closing JASM will now NOT close Migoto or Genshin if they were started trough it.... ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
* Crash when pressing enter without selecting a charater when moving mods. ([6234d01](https://github.com/Jorixon/JASM/commit/6234d01a8b5a8f53036b879b36b4b21168a7f9b6))
* On character details overview, flyout autmatically focuses on text box on open. ([8a1463e](https://github.com/Jorixon/JASM/commit/8a1463e625227d3afaf71588786d9b92e757e82f))
* please-release test ([fc740a2](https://github.com/Jorixon/JASM/commit/fc740a24886397956e90e199a8ac32544d886e72))
* release pleasev2 ([8a0f08b](https://github.com/Jorixon/JASM/commit/8a0f08bbc80b66ae89d6fb14d6d4e999cfb779e5))
* Removed unecesery code ([d6a68c4](https://github.com/Jorixon/JASM/commit/d6a68c4ada8e8145f3f8f81130afd318a3880277))
* test ([af170ef](https://github.com/Jorixon/JASM/commit/af170ef23e63094550d87baa2b3b4332729523b5))
* That some mod names had an underscore shown  with their name (_ModName) when enabled. ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
* Typo ([ff0edd9](https://github.com/Jorixon/JASM/commit/ff0edd9c858d5757378dd5b5bd5815ec597395c8))
* Typo ([b88fa95](https://github.com/Jorixon/JASM/commit/b88fa95cf70daa14810f9e5034596da37d0aa7d8))
* Typo ([403d269](https://github.com/Jorixon/JASM/commit/403d26960c910624911d9ae8202e92724974582e))
* When navigating to a charcter detailed overview focus is set on grid and not the back button ([b6ceb06](https://github.com/Jorixon/JASM/commit/b6ceb06b93148724c28dccc559d25c84a5dd4e51))
