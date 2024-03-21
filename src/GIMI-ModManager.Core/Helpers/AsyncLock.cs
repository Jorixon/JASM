namespace GIMI_ModManager.Core.Helpers;

public sealed class AsyncLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<LockReleaser> LockAsync(int timeout = -1, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new LockReleaser(Release);
    }

    private void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}

public readonly struct LockReleaser : IDisposable
{
    private readonly Action _release;

    internal LockReleaser(Action release)
    {
        _release = release;
    }

    public void Dispose() => _release();
    public void Release() => _release();
}