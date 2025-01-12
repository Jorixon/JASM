import json
import os


charactersJson = json.load(open('characters.json'))

for character in charactersJson:
	character['Image'] = character['Image'].replace("Character_", "").replace("_Thumb", "")
	if (character['InGameSkins']):
		for skin in character['InGameSkins']:
			skin['Image'] = skin['Image'].replace("Character_", "").replace("_Thumb", "").replace("Skin_", "")

json.dump(charactersJson, open('characters.json', 'w'), indent=2)


# Update image files in Images folder
images_folder = os.path.abspath(os.path.join(os.path.dirname(__file__), 'Images', 'Characters'))

for filename in os.listdir(images_folder):
	new_filename = filename.replace("Character_", "").replace("_Thumb", "")
	os.rename(os.path.join(images_folder, filename), os.path.join(images_folder, new_filename))


images_folder = os.path.abspath(os.path.join(os.path.dirname(__file__), 'Images', 'AltCharacterSkins'))

for filename in os.listdir(images_folder):
	new_filename = filename.replace("Character_Skin_", "").replace("_Thumb", "").replace("Skin_", "")
	os.rename(os.path.join(images_folder, filename), os.path.join(images_folder, new_filename))


weaponsJson = json.load(open('weapons.json'))

for weapon in weaponsJson:
	if (weapon['Image']):
		weapon['Image'] = weapon['Image'].replace("Weapon_", "")

json.dump(weaponsJson, open('weapons.json', 'w'), indent=2)

images_folder = os.path.abspath(os.path.join(os.path.dirname(__file__), 'Images', 'Weapons'))

for filename in os.listdir(images_folder):
	new_filename = filename.replace("Weapon_", "")
	os.rename(os.path.join(images_folder, filename), os.path.join(images_folder, new_filename))