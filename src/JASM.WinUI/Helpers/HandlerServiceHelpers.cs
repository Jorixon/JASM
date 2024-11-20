using System.Runtime.CompilerServices;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.Helpers;

public static class HandlerServiceHelpers
{
    public static async Task<Result<T>> CommandWrapperAsync<T>(Func<Task<Result<T>>> command, bool catchException = true,
        [CallerMemberName] string commandName = "Unknown command", Func<Result<T>>? customErrorHandler = null)
    {
        try
        {
            return await command();
        }
        catch (Exception ex)
        {
#if DEBUG
            throw;
#endif

            if (!catchException)
                throw;

            return customErrorHandler?.Invoke() ??
                   Result<T>.Error(ex, new SimpleNotification($"An error occured error while executing command '{commandName}'",
                       ex.ToString(),
                       TimeSpan.FromSeconds(6)));
        }
    }
}