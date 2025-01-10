using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public abstract partial class Form : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private bool _isInitialized;

    public bool IsValid => IsInitialized && Fields.All(f => f.IsValid);

    [ObservableProperty] private bool _anyFieldDirty;

    public List<BaseInputField> Fields { get; } = [];


    public Form()
    {
        var fields = GetType()
            .GetProperties().Where(p => p.PropertyType.IsAssignableTo(typeof(BaseInputField)))
            .Select(p => new
            {
                Property = (BaseInputField)p.GetValue(this)!,
                PropertyName = p.Name
            });


        foreach (var field in fields)
        {
            field.Property.FieldName = field.PropertyName;
            field.Property.PropertyChanged += (sender, _) => OnValueChanged((BaseInputField)sender!);
            Fields.Add(field.Property);
        }
    }

    public virtual void OnValueChanged(BaseInputField field)
    {
        if (!IsInitialized) return;
        var oldValidValue = IsValid;

        AnyFieldDirty = Fields.Any(f => f.IsDirty);
        field.Validate(this);


        if (oldValidValue != IsValid)
            OnPropertyChanged(nameof(IsValid));
    }
}