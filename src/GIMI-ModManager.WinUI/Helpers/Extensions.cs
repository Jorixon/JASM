using System.Collections.ObjectModel;

namespace GIMI_ModManager.WinUI.Helpers;

public static class Extensions
{
    public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}