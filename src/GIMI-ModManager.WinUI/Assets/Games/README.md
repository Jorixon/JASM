# Game data in JASM
JASM stores character and other game related data in json files. These are read upon starting JASM. JASM originally only supported Genshin and support for other games were added later. 

Each folder in the `Games` contains game data for a specific game. Inside of a game folder you'll see a json files and the folders `Images` and `Languages`. The `Images` folder as the name would suggest contains game specific images, like character portraits. In the `Languages` folder language json overrides can be added to support localization of game data.

JASM has the concept of mod categories. Each mod category has it's own json file. Some games may have more or less. JASM originally only supported characters and support for other categories came later.

Cateogry json files:

- `characters.json`

- `npcs.json`

- `objects.json`

- `weapons.json`

There are also some supporting json files like:

- `elements.json`

- `regions.json`

- `weaponClasses.json`

The data defined in these can be connected with the other category json files. I'll go over this more later.

Last file is the `game.json`. This just contains some general data about the game like a short name and a long name.




## Adding a character

Adding a character is simple. The category json files are a list of objects, adding a character is as easy as copying an existing character and changing some text fields. JASM will automatically read the new character when starting up.

Character object overview:

```json
[
    {
        // Used as additional search terms in different search boxes
        "Keys": [ 
        "paimon",
        "emergency",
        "food"
        ],
        "ReleaseDate": "2020-09-28T00:00:00",
        // JASM uses this name to find the image in the images folder
        // Name can techinally be anything but Genshin character images are prefixed Character_ and suffix _Thumb. 
        "Image": "Character_Paimon_Thumb.png",
        // Used for rarity sorting
        "Rarity": 0, 

    // Used for the element filters, should map to an internal name in elements.json
        "Element": "None", 

    // Not used for anything at the moment, could be used for filtering in the future, should map to an internal name in weaponClasses.json
        "Class": "",

    // Not used for anything at the moment, could be used for filtering in the future, should map to an internal name in regions.json
        "Region": [],
    // JASM can in some cases try and determine what character a mod is for. When doing this it uses the ModFilesName text to try and find a mod file with the same name. Example file match: PaimonHeadLightMap.dds
        "ModFilesName": "Paimon",

        // This is the internal Id of a character. JASM uses this value to identify characters. It's also used as the mod folder name for a character. This has to be unique or JASM will not start!
        "InternalName": "Paimon",

        // Name that is shown in the ui. Also used in searching
        "DisplayName": "Paimon",

        // Additional skins for a character
        // Lets 'pretend' Paimon has an additional ingame skin
        "InGameSkins": [
            {
                // Extra skins have a different game model
                "ModFilesName": "PaimonCN",
                // Looks for image in AltCharacterSkins
                "Image": "Character_Skin_PaimonCN_Thumb.png",
                // Internal identifier. Also used as mod folder name suffix if
                // the setting 'CharacterSkins as characeters' is enabled
                "InternalName": "PaimonCN",
                "DisplayName": "PaimonCN"
            }
        ],
        // Possible to add IsMultiMod, defaults to false if missing
        // If set tot true JASM won't show a warning if multiple mods are enabled for the character. Used for some npcs
        // "IsMultiMod": false
    },
    // ... other characters
    // {
    // ... another character
    // },
]

```

Some values can be ignored like region and Element. The other categories have less properties, npcs for example, does not have support for ReleaseDate or InGameSkins.


### Localization

When JASM parses a character it also looks in the language folder for an override for the current language selected. Let's say your starting Genshin and have the lanuage code ru-ru selected. JASM will then load the `Languages/ru-ru/characters.json` and override properties based on it. Not all properties can be overridden.
