using System;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;

namespace CommunityToolkitWrapper;

// We need this wrapper because the CommunityToolkit 7.* and 8.* both provide this extension method.
// This causes an ambiguous reference error.
public static class CommunityToolkitWrapper
{
    public static Task EnqueueAsync(this DispatcherQueue dispatcher, Func<Task> function,
        DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return DispatcherQueueExtensions.EnqueueAsync(dispatcher, function, priority);
    }

    public static Task<T> EnqueueAsync<T>(this DispatcherQueue dispatcher, Func<T> function,
        DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return DispatcherQueueExtensions.EnqueueAsync(dispatcher, function, priority);
    }

    public static Task<T> EnqueueAsync<T>(this DispatcherQueue dispatcher, Func<Task<T>> function,
        DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return DispatcherQueueExtensions.EnqueueAsync(dispatcher, function, priority);
    }
}