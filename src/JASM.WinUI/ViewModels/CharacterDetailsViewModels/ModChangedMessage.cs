﻿using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public record ModChangedMessage(object sender, CharacterSkinEntry SkinEntry, ModSettings? Settings);