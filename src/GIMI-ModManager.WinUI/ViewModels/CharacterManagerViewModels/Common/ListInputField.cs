using System.Collections.ObjectModel;
using System.Diagnostics;
using GIMI_ModManager.WinUI.Helpers;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed partial class ListInputField<TItem> : BaseInputField
{
    public ListInputField(IEnumerable<TItem>? values = null)
    {
        DefaultItems = (values ?? []).ToList();
        Items.AddRange(DefaultItems);
        Items.CollectionChanged += (_, _) => IsDirty = !Items.SequenceEqual(DefaultItems);
    }

    public List<TItem> DefaultItems { get; private set; }

    public ObservableCollection<TItem> Items { get; } = new();

    public FieldValidators<IReadOnlyCollection<TItem>> ValidationRules { get; } = new();


    public override void Validate(Form form)
    {
        ValidationResults.Clear();

        foreach (var rule in ValidationRules)
        {
            if (rule(new ValidationContext<IReadOnlyCollection<TItem>, Form>(form, this, Items.AsReadOnly())) is { } result)
            {
                AddValidationResult(result);
            }
        }
    }

    public void ReInitializeInput(IEnumerable<TItem> items, IEnumerable<TItem> defaultValue)
    {
        Items.Clear();
        Items.AddRange(items);
        DefaultItems = defaultValue.ToList();
        ValidationResults.Clear();
        IsDirty = false;
    }

    public void ReInitializeInput(ICollection<TItem> items) => ReInitializeInput(items, items);

    public override void Reset(Form form)
    {
        Items.Clear();
        Items.AddRange(DefaultItems);
        Validate(form);
        IsDirty = false;
    }

    private string DebuggerDisplay =>
        $"{nameof(Items)}: {Items.Count} | {nameof(DefaultItems)}.Count: {DefaultItems.Count} | {nameof(IsDirty)}: {IsDirty} | {nameof(IsValid)}: {IsValid} | {nameof(ValidationRules)}.Count: {ValidationRules.Count}";
}