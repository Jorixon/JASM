using System.Diagnostics;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.Helpers.Xaml;

public static class FrameworkElementExtensions
{
    public static async Task AwaitUiElementLoaded(this FrameworkElement element, TimeSpan timeout)
    {
        if (element.IsLoaded)
            return;

        var tcs = new TaskCompletionSource<bool>();


        element.Loaded += OnLoaded;

        var delayTask = Task.Delay(timeout);
        var resultTask = await Task.WhenAny(tcs.Task, delayTask);

        if (resultTask == delayTask)
        {
            try
            {
                element.Loaded -= OnLoaded;
            }
            catch (Exception e)
            {
                // ignored
            }

            Debugger.Break();
        }

        return;

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            element.Loaded -= OnLoaded;
            tcs.TrySetResult(true);
        }
    }

    private const int PollingTime = 100;

    public static async Task AwaitItemsSourceLoaded(this DataGrid dataGrid, TimeSpan timeout)
    {
        if (dataGrid.ItemsSource is not null)
            return;

        var startTime = DateTime.Now;

        do
        {
            await Task.Delay(PollingTime);
            if (startTime.Add(timeout) > DateTime.Now)
                return;
        } while (dataGrid.ItemsSource is null);
    }

    public static async Task AwaitItemsSourceLoaded(this DataGrid dataGrid, CancellationToken cancellationToken)
    {
        if (dataGrid.ItemsSource is not null)
            return;

        do
        {
            await Task.Delay(PollingTime, cancellationToken);
        } while (dataGrid.ItemsSource is null);
    }
}