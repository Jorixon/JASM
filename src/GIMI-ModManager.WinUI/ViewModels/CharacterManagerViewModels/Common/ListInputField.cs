using System.Collections.ObjectModel;
using GIMI_ModManager.WinUI.Helpers;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public sealed partial class ListInputField<TItem> : BaseInputField
{
    public ListInputField(IEnumerable<TItem>? values = null)
    {
        DefaultValue = (values ?? []).ToList();
        Items.AddRange(DefaultValue);
        Items.CollectionChanged += (_, _) => IsDirty = !Items.SequenceEqual(DefaultValue);
    }

    public List<TItem> DefaultValue { get; private set; }
    public ObservableCollection<TItem> Items { get; } = new();

    public List<Func<ValidationContext<ReadOnlyCollection<TItem>, Form>, ValidationResult?>> ValidationRules { get; } = new();


    public override void Validate(Form form)
    {
        ValidationResults.Clear();

        foreach (var rule in ValidationRules)
        {
            if (rule(new ValidationContext<ReadOnlyCollection<TItem>, Form>(form, this, Items.AsReadOnly())) is { } result)
            {
                AddValidationResult(result);
            }
        }
    }

    public void ReInitializeInput(IEnumerable<TItem> items, IEnumerable<TItem> defaultValue)
    {
        Items.Clear();
        Items.AddRange(items);
        DefaultValue = defaultValue.ToList();
        ValidationResults.Clear();
        IsDirty = false;
    }

    public void ReInitializeInput(ICollection<TItem> items) => ReInitializeInput(items, items);

    public override void Reset(Form form)
    {
        Items.Clear();
        Items.AddRange(DefaultValue);
        Validate(form);
        IsDirty = false;
    }
}