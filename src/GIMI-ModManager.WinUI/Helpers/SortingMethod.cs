namespace GIMI_ModManager.WinUI.Helpers;

public abstract class SortingMethod<T> where T : class
{
    public string SortingMethodType => _sorter.SortingMethodType;

    private readonly Sorter<T> _sorter;

    private readonly T[] _lastItems;
    private readonly T? _firstItem;

    public SortingMethod(Sorter<T> sortingMethodType, T? firstItem = null,
        IEnumerable<T>? lastItems = null)
    {
        _sorter = sortingMethodType;
        _lastItems = lastItems?.ToArray() ?? [];
        _firstItem = firstItem;
    }

    public T? FirstItem => _firstItem;

    public IEnumerable<T> LastItems => _lastItems;

    public IEnumerable<T> Sort(IEnumerable<T> items, bool isDescending)
    {
        var sortedItems = _sorter.Sort(items, isDescending);


        if (_firstItem is null && _lastItems.Length == 0)
            return sortedItems;

        var sortedList = sortedItems.ToList();

        var modifiableItems = new List<T>(sortedList);


        foreach (var characterGridItemModel in modifiableItems.Intersect(_lastItems))
        {
            sortedList.Remove(characterGridItemModel);
            sortedList.Add(characterGridItemModel);
        }

        if (_firstItem is not null)
        {
            sortedList.Remove(_firstItem);
            sortedList.Insert(0, _firstItem);
        }

        PostSortAction(sortedList);


        return sortedList;
    }

    protected virtual void PostSortAction(List<T> sortedList)
    {
    }

    public override string ToString()
    {
        return SortingMethodType;
    }
}