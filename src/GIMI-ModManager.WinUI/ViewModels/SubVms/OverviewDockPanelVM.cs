using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class OverviewDockPanelVM : ObservableRecipient
{
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<OverviewDockPanelVM>();
    private readonly IGenshinService _genshinService = App.GetService<IGenshinService>();

    public ObservableCollection<ElementIcon> Elements { get; } = new();
    [ObservableProperty] private ElementIcon? _selectedElement;

    public OverviewDockPanelVM()
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

    private string GetElementIconPath(Elements element)
    {
        return Path.Combine(App.ROOT_DIR, "Assets", "Images", "Elements", $"Element_{element}.svg");
    }

    [RelayCommand]
    private void ElementSelected(ElementIcon element)
    {
        if (element is null) return;

        if (SelectedElement?.Element == element.Element)
        {
            SelectedElement = null;
            return;
        }

        _logger.Debug("ElementSelected: {Element}", element.Element);
        SelectedElement = element;
    }
}

[DebuggerDisplay("{Element}|{ImageUri}")]
public sealed class ElementIcon
{
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public Elements Element { get; set; }

    public ElementIcon(string name, string imageUri, Elements element)
    {
        Name = name;
        ImageUri = imageUri;
        Element = element;
    }
}