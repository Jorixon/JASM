using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.Core.Entities;

public class CharacterSkinEntry : IEqualityComparer<CharacterSkinEntry>
{
    internal CharacterSkinEntry(ISkinMod mod, ICharacterModList modList, bool isEnabled)
    {
        Mod = mod;
        ModList = modList;
        IsEnabled = isEnabled;
    }

    public Guid Id => Mod.Id;
    public ISkinMod Mod { get; }
    public ICharacterModList ModList { get; }
    public bool IsEnabled { get; internal set; }

    public bool Equals(CharacterSkinEntry? x, CharacterSkinEntry? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id.Equals(y.Id);
    }

    public int GetHashCode(CharacterSkinEntry obj) => obj.Id.GetHashCode();

    public override string ToString() => "CharacterSkinEntry: " + Mod.Name + " (" + Id + ")";
}