# Changelog

## [2.13.3](https://github.com/Jorixon/JASM/compare/v2.13.2...v2.13.3) (2024-08-04)


### Miscellaneous

* Now possible to toggle whether JASM remembers window size and window position ([#229](https://github.com/Jorixon/JASM/issues/229)) ([79f901f](https://github.com/Jorixon/JASM/commit/79f901f31b44bda2c8be468adfe94a474894d3ca))

## [2.13.2](https://github.com/Jorixon/JASM/compare/v2.13.1...v2.13.2) (2024-08-02)


### Miscellaneous

* Added more error handling to Auto Updater application ([05b3339](https://github.com/Jorixon/JASM/commit/05b3339447c8f0c72b18e117e4247d6144d7346f))

## [2.13.1](https://github.com/Jorixon/JASM/compare/v2.13.0...v2.13.1) (2024-08-01)


### Miscellaneous

* Add characters for Genshin (4.8 - 5.0) and HSR (2.4 - 2.5) ([#224](https://github.com/Jorixon/JASM/issues/224)) Thanks [@jeffvli](https://github.com/jeffvli) ([39b5dd7](https://github.com/Jorixon/JASM/commit/39b5dd7e86f0cefdbe9fc97127dd7007eaee01e6))
* Update Genshin Game Localization(zh-cn) to 4.8 ([#223](https://github.com/Jorixon/JASM/issues/223)) Thanks [@kanonmelodis](https://github.com/kanonmelodis) ([3ff9e30](https://github.com/Jorixon/JASM/commit/3ff9e305f5dc9439e27178eb4c01556bd82eddd2))

## [2.13.0](https://github.com/Jorixon/JASM/compare/v2.12.2...v2.13.0) (2024-07-25)


### Features

* Add sort options to character gallery view ([#215](https://github.com/Jorixon/JASM/issues/215)) Thanks [@jeffvli](https://github.com/jeffvli) ([c4c02f3](https://github.com/Jorixon/JASM/commit/c4c02f3f273458d8dae2c35c4d771c1eb1a4c802))
* Support deleting mod in gallery view ([#213](https://github.com/Jorixon/JASM/issues/213)) Thanks [@yuukidach](https://github.com/yuukidach) ([4a1744e](https://github.com/Jorixon/JASM/commit/4a1744ec8705290c55d6aa81336b4a63c5551c55))

## [2.12.2](https://github.com/Jorixon/JASM/compare/v2.12.1...v2.12.2) (2024-07-23)


### Miscellaneous

* Added all supported games to the quick switch menu (https://github.com/Jorixon/JASM/issues/210) Thanks [@jeffvli](https://github.com/jeffvli) ([afcd8a8](https://github.com/Jorixon/JASM/commit/afcd8a8ea850373477ed67394aed05d73e4832e4))


### Code Refactoring

* Limited the number of active tasks queued at the same time in ModUpdateAvailableChecker. This should improve performance when checking for mod updates with a large number of mods. ([#214](https://github.com/Jorixon/JASM/issues/214)) ([8364204](https://github.com/Jorixon/JASM/commit/8364204fd2fc873f8eb96c05584760325ac3bc1e))

## [2.12.1](https://github.com/Jorixon/JASM/compare/v2.12.0...v2.12.1) (2024-07-23)


### Miscellaneous

* Added Russian translation to Genshin and Honkai game related text ([d7f7751](https://github.com/Jorixon/JASM/commit/d7f77512a74105911ee1ad249c8134ec27b8eccc))

## [2.12.0](https://github.com/Jorixon/JASM/compare/v2.11.0...v2.12.0) (2024-07-11)


### Features

* Added ZZZ support ([#205](https://github.com/Jorixon/JASM/issues/205)) Thanks @Pyrageis ([41b2497](https://github.com/Jorixon/JASM/commit/41b24979f8e86a4a245f45a699101ce582d26403))


### Bug Fixes

* Mod update notification would always be shown for first update check for new mod ([#205](https://github.com/Jorixon/JASM/issues/205)) ([86a96e5](https://github.com/Jorixon/JASM/commit/86a96e5214b6d828d9306e5e0940b96f84c8c094))


### Miscellaneous

* Added logging to auto updater ([86f0697](https://github.com/Jorixon/JASM/commit/86f06975a0303cfabf61e079f627ea503391cda4))
* Changed validation check for model import loader exe name ([#205](https://github.com/Jorixon/JASM/issues/205)) ([86a96e5](https://github.com/Jorixon/JASM/commit/86a96e5214b6d828d9306e5e0940b96f84c8c094))

## [2.11.0](https://github.com/Jorixon/JASM/compare/v2.10.1...v2.11.0) (2024-07-04)


### Features

* Non-fatal exceptions no longer close the main window ([#203](https://github.com/Jorixon/JASM/issues/203)) ([8c7bd16](https://github.com/Jorixon/JASM/commit/8c7bd164cf582740ca7b3d08e1fc1e63df6f6369))


### Miscellaneous

* Added some Russian translations. Thanks for the help Haosy ([8c7bd16](https://github.com/Jorixon/JASM/commit/8c7bd164cf582740ca7b3d08e1fc1e63df6f6369))
* Updated most app packages including WinAppSdk ([8c7bd16](https://github.com/Jorixon/JASM/commit/8c7bd164cf582740ca7b3d08e1fc1e63df6f6369))


### Code Refactoring

* Removed old code that was used to make api calls and check for mod updates ([8c7bd16](https://github.com/Jorixon/JASM/commit/8c7bd164cf582740ca7b3d08e1fc1e63df6f6369))

## [2.10.1](https://github.com/Jorixon/JASM/compare/v2.10.0...v2.10.1) (2024-06-06)


### Miscellaneous

* Updated "reorganize mods" tooltip on startup page ([4288708](https://github.com/Jorixon/JASM/commit/42887087cba0ca8f164b1664a78f58de7469cba9))

## [2.10.0](https://github.com/Jorixon/JASM/compare/v2.9.1...v2.10.0) (2024-06-05)


### Features

* Wuthering Waves support ([#192](https://github.com/Jorixon/JASM/issues/192)) ([697ccf7](https://github.com/Jorixon/JASM/commit/697ccf745434b402e022f2ccc9d442ae0b41fdef))

## [2.9.1](https://github.com/Jorixon/JASM/compare/v2.9.0...v2.9.1) (2024-06-03)


### Miscellaneous

* **Genshin:** Moved Clorinde and Sigewinne to characters, added Sethos character and added missing weapons ([#190](https://github.com/Jorixon/JASM/issues/190)) ([1c11e31](https://github.com/Jorixon/JASM/commit/1c11e31ff650276ee501ae9d65313c8dcc102764))
* Updated WinAppSdk to 1.5.3 ([b8c8d61](https://github.com/Jorixon/JASM/commit/b8c8d61754ce60645ee42bb10ed53b9348bbb004))

## [2.9.0](https://github.com/Jorixon/JASM/compare/v2.8.0...v2.9.0) (2024-05-05)


### Features

* Added first iteration of the mod gallery view ([#180](https://github.com/Jorixon/JASM/issues/180)) ([461a91f](https://github.com/Jorixon/JASM/commit/461a91fa3caf97877673cfa15c9b69e0eff03229))


### Miscellaneous

* Added HSR 2.2-2.3 characters ([#182](https://github.com/Jorixon/JASM/issues/182)) ([3c519cc](https://github.com/Jorixon/JASM/commit/3c519ccff7d59f91b826a2fab7df66e2cbdefb5d))
* **ModInstaller:** "Enable only this mod" checkbox defaults to off for multi mod characters ([9aa90a9](https://github.com/Jorixon/JASM/commit/9aa90a9fb1217029051599b3e00146e293bc7ccd))

## [2.8.0](https://github.com/Jorixon/JASM/compare/v2.7.0...v2.8.0) (2024-04-21)


### Features

* Now possible to download mods directly in the "Update available" / "New mod files" window ([#177](https://github.com/Jorixon/JASM/issues/177)) ([8c7ed5f](https://github.com/Jorixon/JASM/commit/8c7ed5f3fe81f347d6da03c29906f28430b15cac))

## [2.7.0](https://github.com/Jorixon/JASM/compare/v2.6.3...v2.7.0) (2024-04-20)


### Features

* Now possible to quickly switch presets from the characters overview page ([#176](https://github.com/Jorixon/JASM/issues/176)) ([9731655](https://github.com/Jorixon/JASM/commit/9731655d86ae70b079d0c24b87c8beb490541983))


### Miscellaneous

* Updated WinAppSdk and a few other packages ([#174](https://github.com/Jorixon/JASM/issues/174)) ([4289cda](https://github.com/Jorixon/JASM/commit/4289cda936d9c4d88fabb4d2f9caf981a1e5f360))


### Continuous Integration

* Added Self Contained build to releases ([9731655](https://github.com/Jorixon/JASM/commit/9731655d86ae70b079d0c24b87c8beb490541983))

## [2.6.3](https://github.com/Jorixon/JASM/compare/v2.6.2...v2.6.3) (2024-04-01)


### Miscellaneous

* Improved mod enabling logic during mod install ([#167](https://github.com/Jorixon/JASM/issues/167)) ([7498afb](https://github.com/Jorixon/JASM/commit/7498afb83948ad4967c50266b3d354637909c601)) Thanks @Davoleo 

## [2.6.2](https://github.com/Jorixon/JASM/compare/v2.6.1...v2.6.2) (2024-03-31)


### Miscellaneous

* Added Waverider and Xingqiu skin Bamboo Rain ([#168](https://github.com/Jorixon/JASM/issues/168)) ([c978d06](https://github.com/Jorixon/JASM/commit/c978d069434a837c487164fc387370f93b875eb4))
* Changed NPC and Weapon icons ([2176bfb](https://github.com/Jorixon/JASM/commit/2176bfbcf16f6fb1bb5bab33933fdcc97c3869ba))

## [2.6.1](https://github.com/Jorixon/JASM/compare/v2.6.0...v2.6.1) (2024-03-29)


### Bug Fixes

* Pasting image from clipboard was saved as .bitmap when .png format was available ([81eb571](https://github.com/Jorixon/JASM/commit/81eb571c68a24fd8ffb180974538776b9c3837f7))


### Miscellaneous

* Added presets overview ([#162](https://github.com/Jorixon/JASM/issues/162)) ([442a164](https://github.com/Jorixon/JASM/commit/442a16470478e35bcf1cff999cfed41a5d31a39b))
* Possible set preset as Read Only ([442a164](https://github.com/Jorixon/JASM/commit/442a16470478e35bcf1cff999cfed41a5d31a39b))
* Possible to manually retrieve/refresh mod info when installing a mod ([668883c](https://github.com/Jorixon/JASM/commit/668883c607847c45125c1529253d2c33e1f4e9b6))

## [2.6.0](https://github.com/Jorixon/JASM/compare/v2.5.0...v2.6.0) (2024-03-26)


### Features

* Mod Presets and Persisting of Mod Preferences ([#160](https://github.com/Jorixon/JASM/issues/160)) ([2b0bc5e](https://github.com/Jorixon/JASM/commit/2b0bc5e930987f290b59a8e708f4fc138fd1138c))


### Miscellaneous

* Detect Script.ini files ([78cd6f5](https://github.com/Jorixon/JASM/commit/78cd6f5048762de8fadedd1f536733707ccaae3f))

## [2.5.0](https://github.com/Jorixon/JASM/compare/v2.4.0...v2.5.0) (2024-03-23)


### Features

* Possible to pick, copy and paste mod image during mod install ([#157](https://github.com/Jorixon/JASM/issues/157)) ([b143296](https://github.com/Jorixon/JASM/commit/b14329630a6208fcb736f73f8cdcf218a62e7747))


### Bug Fixes

* Potential fix for NullReferenceException when navigating to character ([8cc90c2](https://github.com/Jorixon/JASM/commit/8cc90c2b840c447748e1adb6d4c70288ce4b2e4b))


### Miscellaneous

* Possible to set mod installer to always on top ([b143296](https://github.com/Jorixon/JASM/commit/b14329630a6208fcb736f73f8cdcf218a62e7747))
* Updated WinAppSDK and .NET ([#159](https://github.com/Jorixon/JASM/issues/159)) ([9bbd739](https://github.com/Jorixon/JASM/commit/9bbd739a0a404ad21f31a091a6de9a8944237d3a))

## [2.4.0](https://github.com/Jorixon/JASM/compare/v2.3.0...v2.4.0) (2024-03-23)


### Features

* Now possible to enable Character skins as separate characters ([#153](https://github.com/Jorixon/JASM/issues/153)) ([491f4bb](https://github.com/Jorixon/JASM/commit/491f4bb10aa6bdf3ea19bad416c8fa1dd8bedacf))


### Bug Fixes

* Check if WebView2 is available before using it ([#135](https://github.com/Jorixon/JASM/issues/135)) ([1bba6e6](https://github.com/Jorixon/JASM/commit/1bba6e6e77a8586678708e962b4e14a89608eac0))
* Not being able to set character override for mods ([#156](https://github.com/Jorixon/JASM/issues/156)) ([de28cca](https://github.com/Jorixon/JASM/commit/de28cca00fd8cdc5de203dbe20b497391bc6d456))


### Miscellaneous

* Added Arlecchino and various npcs ([#149](https://github.com/Jorixon/JASM/issues/149)) ([9b882e2](https://github.com/Jorixon/JASM/commit/9b882e229f0a144b604582f47f484758090db400))
* Added Verdict weapon ([#154](https://github.com/Jorixon/JASM/issues/154)) ([0be089e](https://github.com/Jorixon/JASM/commit/0be089e68ad7ce89208f7555f8be01b3f5c699be))

## [2.3.0](https://github.com/Jorixon/JASM/compare/v2.2.0...v2.3.0) (2024-03-13)


### Features

* Quick switch button added for switching between games ([#138](https://github.com/Jorixon/JASM/issues/138)) ([ec8adc2](https://github.com/Jorixon/JASM/commit/ec8adc2db04f51f4288ae952a92b832d257956ac))
* When navigating back from a character page to the character overview, it will now scroll that character into view ([ec8adc2](https://github.com/Jorixon/JASM/commit/ec8adc2db04f51f4288ae952a92b832d257956ac))


### Bug Fixes

* Potential fix for crash when navigating to character after mod install ([ec8adc2](https://github.com/Jorixon/JASM/commit/ec8adc2db04f51f4288ae952a92b832d257956ac))


### Miscellaneous

* Added Ganyu and Shenhe skins ([1499042](https://github.com/Jorixon/JASM/commit/1499042f3a138992f794421310d0e7285ea21e80))
* Redid Date Added sorting logic ([ec8adc2](https://github.com/Jorixon/JASM/commit/ec8adc2db04f51f4288ae952a92b832d257956ac))
* Reworked application cleanup and exit process ([#141](https://github.com/Jorixon/JASM/issues/141)) ([da9e65f](https://github.com/Jorixon/JASM/commit/da9e65f895e25fb8167ff178c5cc4fb9f6c0bf37))

## [2.2.0](https://github.com/Jorixon/JASM/compare/v2.1.2...v2.2.0) (2024-03-10)


### Reverts

* No longer publish as single file due to new (WinAppSDK?) bug ([#136](https://github.com/Jorixon/JASM/issues/136)) ([634692a](https://github.com/Jorixon/JASM/commit/634692a1b9d26f84fb2792b53f1fda5585359c67))


### Features

* Ability to choose .ini file for mods or to ignore it ([#126](https://github.com/Jorixon/JASM/issues/126)) ([8401d7d](https://github.com/Jorixon/JASM/commit/8401d7d41d712e57c3e1fe684aad92031561698b))


### Miscellaneous

* Added Chiori, hsr 2.1 characters, hsr character info, typo fixes ([#134](https://github.com/Jorixon/JASM/issues/134)) ([6f05ee6](https://github.com/Jorixon/JASM/commit/6f05ee672987f4079fb61254f658e06e37bef136)) Thanks @Pyrageis 
* Introduce Penacony and its characters ([#132](https://github.com/Jorixon/JASM/issues/132)) ([b59e3d9](https://github.com/Jorixon/JASM/commit/b59e3d92ebb4fc8ad4ebe5c4d1cbdbb1d7d35a33)) Thanks @EffortlessFury 
* Updated WinAppSdk to 1.5 and som other packages ([ae8947e](https://github.com/Jorixon/JASM/commit/ae8947e7a79cd2c204d7dd60e27eb33c7e082e9a))

## [2.1.2](https://github.com/Jorixon/JASM/compare/v2.1.1...v2.1.2) (2024-01-31)


### Miscellaneous

* Added characters Gaming and Xianyun ([0a41481](https://github.com/Jorixon/JASM/commit/0a41481b430584f4dd10a0a9241b5cd632b31ebb))

## [2.1.1](https://github.com/Jorixon/JASM/compare/v2.1.0...v2.1.1) (2024-01-28)


### Miscellaneous

* Added aditional error handling for mod update background checker ([74f3cc7](https://github.com/Jorixon/JASM/commit/74f3cc77e5a0522a4fdfba712cbb2874fb75aa6c))
* Added some additional error handling when picking 3dmigoto/genshin process ([a940dc7](https://github.com/Jorixon/JASM/commit/a940dc75276bd45d3c9709fb42ea355c527745fb))
* Updated readme and adjusted build settings ([b754ec2](https://github.com/Jorixon/JASM/commit/b754ec28d2b49fbd62bc513930b6457212077095))
* Updated WinAppSDK ([39086ab](https://github.com/Jorixon/JASM/commit/39086ab23bc67f7eadf4b5e39cfcf2f64323644d))

## [2.1.0](https://github.com/Jorixon/JASM/compare/v2.0.0...v2.1.0) (2024-01-08)


### Features

* Now possible to disable all other mods while activating the new mod when installing a new mod ([#116](https://github.com/Jorixon/JASM/issues/116))  ([9130f0c](https://github.com/Jorixon/JASM/commit/9130f0c15b75ac93bc96b0544ab9dfb24960b22e))


### Bug Fixes

* Potential fix for crash when JASM looks for other running instances of itself ([#118](https://github.com/Jorixon/JASM/issues/118)) ([20fafc1](https://github.com/Jorixon/JASM/commit/20fafc1b5480a7df39dce81bd99710da7e8ededd))
* Potential fix for deleting mods freezing the app ([9130f0c](https://github.com/Jorixon/JASM/commit/9130f0c15b75ac93bc96b0544ab9dfb24960b22e))


### Miscellaneous

* Changed restart logic to use winappsdk to restart app. Should hopefully make it more stable ([#114](https://github.com/Jorixon/JASM/issues/114)) ([d7044dd](https://github.com/Jorixon/JASM/commit/d7044ddeb0fdff49762bc2e5ee3bd047c2b9e88e))


### Code Refactoring

* Fixed typo in App Management in folder name / namespace ([d7044dd](https://github.com/Jorixon/JASM/commit/d7044ddeb0fdff49762bc2e5ee3bd047c2b9e88e))
* Redid notifications and updated namespaces ([9130f0c](https://github.com/Jorixon/JASM/commit/9130f0c15b75ac93bc96b0544ab9dfb24960b22e))

## [2.0.0](https://github.com/Jorixon/JASM/compare/v1.9.2...v2.0.0) (2024-01-06)


### ⚠ BREAKING CHANGES

* Redid Folder structure ([#109](https://github.com/Jorixon/JASM/issues/109))

### Features

* Added Mod counter on overview and sort by mod count ([62622a6](https://github.com/Jorixon/JASM/commit/62622a6399dd3d595d6880e2902fdfb2945ee8b2))
* Character/ModObject folders are now created on demand ([62622a6](https://github.com/Jorixon/JASM/commit/62622a6399dd3d595d6880e2902fdfb2945ee8b2))
* Redid Folder structure ([#109](https://github.com/Jorixon/JASM/issues/109)) ([62622a6](https://github.com/Jorixon/JASM/commit/62622a6399dd3d595d6880e2902fdfb2945ee8b2))


### Miscellaneous

* Added "Date Added" to grid in CharacterDetails page ([62622a6](https://github.com/Jorixon/JASM/commit/62622a6399dd3d595d6880e2902fdfb2945ee8b2))
* Added Chevreuse ([#110](https://github.com/Jorixon/JASM/issues/110)) ([4ee26d6](https://github.com/Jorixon/JASM/commit/4ee26d61ad622ba9e508b3c3396e13ffc39c2904))


### Code Refactoring

* Background tasks now use the LongRunning option ([4ee26d6](https://github.com/Jorixon/JASM/commit/4ee26d61ad622ba9e508b3c3396e13ffc39c2904))

## [1.9.2](https://github.com/Jorixon/JASM/compare/v1.9.1...v1.9.2) (2023-12-06)


### Bug Fixes

* Crash window showing on shutdown ([88ab01b](https://github.com/Jorixon/JASM/commit/88ab01b7f52bc9903caae645deaa9a3669a15d9a))

## [1.9.1](https://github.com/Jorixon/JASM/compare/v1.9.0...v1.9.1) (2023-12-02)


### Bug Fixes

* Possible fix for mod folder names containing " ä " or similar characters, causing mod preview image to fail to load ([d184123](https://github.com/Jorixon/JASM/commit/d184123bb158dbd5c805d31818657e5ff7817bbf))


### Tweaks

* Made automatic mod resorting a bit stricter when checking folder name and internal name ([d184123](https://github.com/Jorixon/JASM/commit/d184123bb158dbd5c805d31818657e5ff7817bbf))


### Miscellaneous

* Author now visibly in mod grid ([d184123](https://github.com/Jorixon/JASM/commit/d184123bb158dbd5c805d31818657e5ff7817bbf))
* More npcs and images ([d184123](https://github.com/Jorixon/JASM/commit/d184123bb158dbd5c805d31818657e5ff7817bbf))
* Updated WinAppSDK and a few other packages ([#102](https://github.com/Jorixon/JASM/issues/102)) ([d184123](https://github.com/Jorixon/JASM/commit/d184123bb158dbd5c805d31818657e5ff7817bbf))

## [1.9.0](https://github.com/Jorixon/JASM/compare/v1.8.1...v1.9.0) (2023-11-29)


### Features

* Added Weapons category([#95](https://github.com/Jorixon/JASM/issues/95)) ([6c55bf3](https://github.com/Jorixon/JASM/commit/6c55bf36b1d2492537ff4661b8a76b1d85497547))
* Category support. Added empty objects and minimal npcs categories. ([#93](https://github.com/Jorixon/JASM/issues/93)) ([7349b41](https://github.com/Jorixon/JASM/commit/7349b41347ddc1d9c843afdabed4f71ddfd26035))
* The Elevator process will now automatically refresh mods in game when enabling/disabling mods in JASM ([6c55bf3](https://github.com/Jorixon/JASM/commit/6c55bf36b1d2492537ff4661b8a76b1d85497547))


### Bug Fixes

* Honkai star rail 3DMigotoLoader not starting as admin. Now checking the "run this program as an administrator" on the file "3DMigotoLoader.exe" should start it as admin, this worked for me at least ([7349b41](https://github.com/Jorixon/JASM/commit/7349b41347ddc1d9c843afdabed4f71ddfd26035))


### Tweaks

* Added some more info to the "mod added" notification and "mod moved" notification. ([7349b41](https://github.com/Jorixon/JASM/commit/7349b41347ddc1d9c843afdabed4f71ddfd26035))


### Miscellaneous

* Added more tooltips around the app and some minor text changes ([6c55bf3](https://github.com/Jorixon/JASM/commit/6c55bf36b1d2492537ff4661b8a76b1d85497547))
* Minor improvements to the underlying code of the Mod installer helper ([7349b41](https://github.com/Jorixon/JASM/commit/7349b41347ddc1d9c843afdabed4f71ddfd26035))

## [1.8.1](https://github.com/Jorixon/JASM/compare/v1.8.0...v1.8.1) (2023-11-24)


### Bug Fixes

* JASM crashing on first time startup ([c3048b4](https://github.com/Jorixon/JASM/commit/c3048b40faaf3cd2984884e44fcfd54392f7ee06))

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
