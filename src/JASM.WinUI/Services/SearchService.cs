using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.Services;

public partial class SearchService<T> : ObservableObject
{
    private List<T> _items;

    [ObservableProperty] private ObservableCollection<T> _searchResults = new();

    private List<Func<T, string, bool>> _queryFuncs = new();

    private T? _noResultItem = default;

    public SearchService(List<T>? items = null)
    {
        _items = items ?? new List<T>();
    }

    public void SetItems(IEnumerable<T> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }

    private void ResetSuggestions()
    {
        SearchResults.Clear();
    }

    public void Reset()
    {
        ResetSuggestions();
        _queryFuncs.Clear();
    }

    public void AddNoResultItem(T item)
    {
        _noResultItem = item;
    }


    public void AddQueryFunc(Func<T, string, bool> func)
    {
        _queryFuncs.Add(func);
    }


    public ICollection<T> Search(string query)
    {
        if (!_queryFuncs.Any()) throw new InvalidOperationException("No query functions were added.");
        ResetSuggestions();

        if (string.IsNullOrWhiteSpace(query))
        {
            ResetSuggestions();
            return Array.Empty<T>();
        }

        var results = _items.Where(item => _queryFuncs.All(func => func(item, query))).ToArray();

        if (results.Length == 0 && _noResultItem is not null)
        {
            SearchResults.Add(_noResultItem);
            return SearchResults;
        }

        foreach (var result in results)
            SearchResults.Add(result);

        return results;
    }
}