using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class OverviewDockPanelVM : ObservableRecipient
{
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<OverviewDockPanelVM>();
    private readonly IGameService _gameService = App.GetService<IGameService>();

    public event EventHandler<FilterElementSelectedArgs>? FilterElementSelected;
    public ObservableCollection<ElementIcon> Elements { get; set; } = new();

    public ObservableCollection<ElementIcon> SelectedElements { get; set; } = new();


    public void Initialize()
    {
        _logger.Debug("Initializing OverviewDockPanelVM");
        var elements = _gameService.GetElements().Where(e => !e.InternalNameEquals("None")).Reverse();

        foreach (var element in elements)
        {
            Elements.Add(new ElementIcon(element.DisplayName, element.ImageUri!.ToString(), element.InternalName));
        }
    }


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
            new FilterElementSelectedArgs(SelectedElements.Select(e => e.InternalElementName)));
    }
}

public sealed class FilterElementSelectedArgs : EventArgs
{
    public string[] InternalElementNames { get; }

    public FilterElementSelectedArgs(IEnumerable<string> internalElementName)
    {
        InternalElementNames = internalElementName.ToArray();
    }
}

[DebuggerDisplay("{Element}|{ImageUri}")]
public partial class ElementIcon : ObservableObject
{
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string InternalElementName { get; set; }

    [ObservableProperty] private bool _isSelected;

    public ElementIcon(string name, string imageUri, string internalElementName)
    {
        Name = name;
        ImageUri = imageUri;
        InternalElementName = internalElementName;
    }
}