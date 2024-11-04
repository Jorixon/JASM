namespace GIMI_ModManager.WinUI.Helpers;

public abstract class Sorter<T>
{
    public string SortingMethodType { get; }
    private readonly SortFunc _firstSortFunc;

    private readonly AdditionalSortFunc? _secondSortFunc;

    private readonly AdditionalSortFunc? _thirdSortFunc;


    protected delegate IOrderedEnumerable<T> SortFunc(IEnumerable<T> characters,
        bool isDescending);

    protected delegate IOrderedEnumerable<T> AdditionalSortFunc(
        IOrderedEnumerable<T> characters, bool isDescending);

    protected Sorter(string sortingMethodType, SortFunc firstSortFunc, AdditionalSortFunc? secondSortFunc = null,
        AdditionalSortFunc? thirdSortFunc = null)
    {
        SortingMethodType = sortingMethodType;
        _firstSortFunc = firstSortFunc;
        _secondSortFunc = secondSortFunc;
        _thirdSortFunc = thirdSortFunc;
    }

    public IEnumerable<T> Sort(IEnumerable<T> characters, bool isDescending)
    {
        var sorted = _firstSortFunc(characters, isDescending);

        if (_secondSortFunc is not null)
            sorted = _secondSortFunc(sorted, isDescending);

        if (_thirdSortFunc is not null)
            sorted = _thirdSortFunc(sorted, isDescending);

        return sorted;
    }
}