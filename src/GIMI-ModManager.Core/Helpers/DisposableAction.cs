namespace GIMI_ModManager.Core.Helpers;

public readonly struct DisposableAction : IDisposable
{
    private readonly Action _action;
    public DisposableAction(Action action) => _action = action;
    public void Dispose() => _action();
}

public readonly struct DisposableActionAsync : IAsyncDisposable
{
    private readonly Func<Task> _asyncAction;
    public DisposableActionAsync(Func<Task> asyncAction) => _asyncAction = asyncAction;

    public async ValueTask DisposeAsync() => await _asyncAction().ConfigureAwait(false);
}