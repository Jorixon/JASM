using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class OverviewDockPanelVM : ObservableRecipient
{
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<OverviewDockPanelVM>();
    private readonly IGenshinService _genshinService = App.GetService<IGenshinService>();

    public event EventHandler<FilterElementSelectedArgs>? FilterElementSelected;
    public ObservableCollection<ElementIcon> Elements { get; set; } = new();

    public ObservableCollection<ElementIcon> SelectedElements { get; set; } = new();


    public void Initialize()
    {
        _logger.Debug("Initializing OverviewDockPanelVM");
        var elements = _genshinService.GetElements().Where(e => e != Core.Entities.Genshin.Elements.None).Reverse();

        foreach (var element in elements)
        {
            Elements.Add(new ElementIcon(element.ToString(), GetElementIconPath(element), element));
            Debug.Assert(File.Exists(GetElementIconPath(element)) || element == Core.Entities.Genshin.Elements.None,
                $"{GetElementIconPath(element)}");
        }
    }

    private string GetElementIconPath(Elements element) =>
        Path.Combine(App.ROOT_DIR, "Assets", "Images", "Elements", $"Element_{element}.svg");


    public void ElementSelectionChanged(IEnumerable<ElementIcon> newItems, IEnumerable<ElementIcon> removedItems)
    {
        foreach (var elementIcon in newItems)
        {
            SelectedElements.Add(elementIcon);
        }

        foreach (var elementIcon in removedItems)
        {
            SelectedElements.Remove(elementIcon);
        }

        FilterElementSelected?.Invoke(this,
            new FilterElementSelectedArgs(SelectedElements.Select(e => e.Element).ToArray()));
    }
}

public sealed class FilterElementSelectedArgs : EventArgs
{
    public Elements[] Element { get; }

    public FilterElementSelectedArgs(Elements[] element)
    {
        Element = element;
    }
}

[DebuggerDisplay("{Element}|{ImageUri}")]
public partial class ElementIcon : ObservableObject
{
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public Elements Element { get; set; }

    [ObservableProperty] private bool _isSelected;

    public ElementIcon(string name, string imageUri, Elements element)
    {
        Name = name;
        ImageUri = imageUri;
        Element = element;
    }
}